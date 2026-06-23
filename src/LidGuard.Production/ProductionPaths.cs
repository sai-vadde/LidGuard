using System;
using System.IO;

namespace LidGuard.Production;

internal static class ProductionPaths
{
    private const string AppFolderName = "LidGuard";

    internal static string RuntimeRoot =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppFolderName
        );

    internal static string LogRoot => Path.Combine(RuntimeRoot, "logs");

    internal static string SettingsPath =>
        Path.Combine(RuntimeRoot, "settings.json");

    internal static void EnsureRuntimeDirectories()
    {
        Directory.CreateDirectory(RuntimeRoot);
        Directory.CreateDirectory(LogRoot);
    }

    internal static string GetInstallStatePath(AppInstallScope scope)
    {
        string root = scope switch
        {
            AppInstallScope.AllUsers => Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData
            ),
            _ => Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData
            )
        };

        return Path.Combine(root, AppFolderName, "install-state.json");
    }

    internal static string GetCurrentExecutablePath()
    {
        return Environment.ProcessPath
               ?? throw new InvalidOperationException(
                   "The current executable path could not be determined."
               );
    }

    internal static AppInstallScope ResolveRuntimeScope()
    {
        string executablePath = GetCurrentExecutablePath();

        if (IsUnderProgramFiles(executablePath))
        {
            return AppInstallScope.AllUsers;
        }

        if (File.Exists(GetInstallStatePath(AppInstallScope.CurrentUser)))
        {
            return AppInstallScope.CurrentUser;
        }

        if (File.Exists(GetInstallStatePath(AppInstallScope.AllUsers)))
        {
            return AppInstallScope.AllUsers;
        }

        return AppInstallScope.CurrentUser;
    }

    private static bool IsUnderProgramFiles(string executablePath)
    {
        string fullPath = Path.GetFullPath(executablePath);
        string programFiles = Path.GetFullPath(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
        );

        return fullPath.StartsWith(
            programFiles,
            StringComparison.OrdinalIgnoreCase
        );
    }
}
