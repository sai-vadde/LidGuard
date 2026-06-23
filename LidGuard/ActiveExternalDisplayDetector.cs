using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace LidGuard;

internal static class ActiveExternalDisplayDetector
{
    private const int ErrorSuccess = 0;
    private const int ErrorInsufficientBuffer = 122;

    internal readonly record struct ActiveExternalDisplay(
        string SourceDeviceName,
        string MonitorDevicePath,
        string MonitorFriendlyDeviceName
    );

    internal static IReadOnlyList<ActiveExternalDisplay> GetActiveExternalDisplays()
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

            List<ActiveExternalDisplay> displays = [];

            for (int index = 0; index < pathCount; index++)
            {
                NativeMethods.DisplayConfigPathInfo path = paths[index];

                if (IsInternalDisplay(path.TargetInfo.OutputTechnology))
                {
                    continue;
                }

                NativeMethods.DisplayConfigTargetDeviceName targetName =
                    new()
                    {
                        Header = new NativeMethods.DisplayConfigDeviceInfoHeader
                        {
                            Type = NativeMethods.DisplayConfigDeviceInfoType.GetTargetName,
                            Size = (uint)Marshal.SizeOf<
                                NativeMethods.DisplayConfigTargetDeviceName>(),
                            AdapterId = path.TargetInfo.AdapterId,
                            Id = path.TargetInfo.Id
                        }
                    };

                if (NativeMethods.DisplayConfigGetDeviceInfo(ref targetName) !=
                    ErrorSuccess)
                {
                    continue;
                }

                NativeMethods.DisplayConfigSourceDeviceName sourceName =
                    new()
                    {
                        Header = new NativeMethods.DisplayConfigDeviceInfoHeader
                        {
                            Type = NativeMethods.DisplayConfigDeviceInfoType.GetSourceName,
                            Size = (uint)Marshal.SizeOf<
                                NativeMethods.DisplayConfigSourceDeviceName>(),
                            AdapterId = path.SourceInfo.AdapterId,
                            Id = path.SourceInfo.Id
                        }
                    };

                if (NativeMethods.DisplayConfigGetDeviceInfo(ref sourceName) !=
                    ErrorSuccess)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(sourceName.ViewGdiDeviceName))
                {
                    continue;
                }

                displays.Add(
                    new ActiveExternalDisplay(
                        sourceName.ViewGdiDeviceName,
                        targetName.MonitorDevicePath,
                        targetName.MonitorFriendlyDeviceName
                    )
                );
            }

            return displays;
        }

        throw new InvalidOperationException(
            "The display topology changed repeatedly while it was being queried."
        );
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
