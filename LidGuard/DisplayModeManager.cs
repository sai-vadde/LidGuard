using System;
using System.Diagnostics;

namespace LidGuard;

internal static class DisplayModeManager
{
    internal static void ForceConfiguredRecoveryMode()
    {
        LidGuardSettings settings = SettingsManager.Load();

        if (!settings.DuplicateModeWasForced)
        {
            ProjectionMode currentMode =
                ProjectionModeDetector.GetCurrentMode();

            if (currentMode != ProjectionMode.Duplicate)
            {
                settings.LastProjectionMode = currentMode;
            }
            else if (settings.LastProjectionMode is null)
            {
                settings.LastProjectionMode = ProjectionMode.Duplicate;
            }

            settings.DuplicateModeWasForced = true;
            SettingsManager.Save(settings);

            AppLogger.LogDisplay(
                $"Saved projection mode '{settings.LastProjectionMode}' and marked the recovery mode as temporarily forced."
            );
        }

        RecoveryMode recoveryMode = settings.RecoveryMode;

        AppLogger.LogDisplay(
            $"Forcing recovery mode '{recoveryMode}'."
        );

        switch (recoveryMode)
        {
            case RecoveryMode.Internal:
                SwitchToInternalDisplay();
                break;
            default:
                SwitchProjection(ProjectionMode.Duplicate);
                break;
        }
    }

    internal static void RestorePreviousProjection()
    {
        LidGuardSettings settings = SettingsManager.Load();

        ProjectionMode modeToRestore =
            settings.LastProjectionMode ??
            settings.DefaultRestoreProjectionMode;

        AppLogger.LogDisplay(
            $"Restoring projection mode '{modeToRestore}'."
        );

        SwitchProjection(modeToRestore);

        settings.DuplicateModeWasForced = false;
        SettingsManager.Save(settings);
    }

    internal static void SwitchProjection(ProjectionMode mode)
    {
        string argument = mode switch
        {
            ProjectionMode.Duplicate => "/clone",
            _ => "/extend"
        };

        SwitchDisplay(argument, mode.ToString());
    }

    private static void SwitchDisplay(string argument, string modeLabel)
    {
        AppLogger.LogDisplay(
            $"Starting DisplaySwitch.exe with {argument} for mode '{modeLabel}'."
        );

        using Process? process = Process.Start(
                new ProcessStartInfo
                {
                    FileName = "DisplaySwitch.exe",
                    Arguments = argument,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            );

        if (process is null)
        {
            AppLogger.LogDisplay(
                "DisplaySwitch.exe could not be started."
            );

            throw new InvalidOperationException(
                "Windows could not start DisplaySwitch.exe."
            );
        }

        AppLogger.LogDisplay(
            "DisplaySwitch.exe started successfully."
        );
    }

    private static void SwitchToInternalDisplay()
    {
        SwitchDisplay("/internal", "Internal");
    }
}
