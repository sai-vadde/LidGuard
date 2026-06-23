using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace LidGuard;

internal static class ProjectionModeDetector
{
    private const int ErrorSuccess = 0;
    private const int ErrorInsufficientBuffer = 122;

    internal static ProjectionMode GetCurrentMode()
    {
        const int maximumAttempts = 3;

        for (int attempt = 0; attempt < maximumAttempts; attempt++)
        {
            int result = NativeMethods.GetDisplayConfigBufferSizes(
                NativeMethods.QDC_ONLY_ACTIVE_PATHS,
                out uint pathCount,
                out uint modeCount
            );

            if (result != ErrorSuccess)
            {
                throw new Win32Exception(
                    result,
                    "Windows could not determine the display buffer sizes."
                );
            }

            var paths = new NativeMethods.DisplayConfigPathInfo[pathCount];
            var modes = new NativeMethods.DisplayConfigModeInfo[modeCount];

            result = NativeMethods.QueryDisplayConfig(
                NativeMethods.QDC_ONLY_ACTIVE_PATHS,
                ref pathCount,
                paths,
                ref modeCount,
                modes,
                IntPtr.Zero
            );

            if (result == ErrorInsufficientBuffer)
            {
                continue;
            }

            if (result != ErrorSuccess)
            {
                throw new Win32Exception(
                    result,
                    "Windows could not retrieve the active display topology."
                );
            }

            return ClassifyProjectionMode(paths, pathCount);
        }

        throw new InvalidOperationException(
            "The display topology changed repeatedly while it was being queried."
        );
    }

    private static ProjectionMode ClassifyProjectionMode(
        NativeMethods.DisplayConfigPathInfo[] paths,
        uint pathCount
    )
    {
        bool hasExternalDisplay = false;
        HashSet<string> uniqueSources = [];

        for (int index = 0; index < pathCount; index++)
        {
            NativeMethods.DisplayConfigPathInfo path = paths[index];

            if (IsInternalDisplay(path.TargetInfo.OutputTechnology))
            {
                // Keep scanning source topology; internal presence is not a saved mode by itself.
            }
            else
            {
                hasExternalDisplay = true;
            }

            string sourceKey =
                $"{path.SourceInfo.AdapterId.LowPart}:{path.SourceInfo.AdapterId.HighPart}:{path.SourceInfo.Id}";

            uniqueSources.Add(sourceKey);
        }

        if (!hasExternalDisplay)
        {
            AppLogger.LogDisplay(
                "Projection mode detection found no active external display, so Duplicate will be used as the default saved mode."
            );

            return ProjectionMode.Duplicate;
        }

        if (uniqueSources.Count <= 1 && pathCount > 1)
        {
            AppLogger.LogDisplay(
                "Projection mode detection classified the current mode as Duplicate."
            );

            return ProjectionMode.Duplicate;
        }

        AppLogger.LogDisplay(
            "Projection mode detection classified the current mode as Extend."
        );

        return ProjectionMode.Extend;
    }

    private static bool IsInternalDisplay(
        NativeMethods.DisplayConfigVideoOutputTechnology technology
    )
    {
        return technology is
            NativeMethods.DisplayConfigVideoOutputTechnology.Internal or
            NativeMethods.DisplayConfigVideoOutputTechnology.Lvds or
            NativeMethods.DisplayConfigVideoOutputTechnology.DisplayPortEmbedded or
            NativeMethods.DisplayConfigVideoOutputTechnology.UdiEmbedded;
    }
}
