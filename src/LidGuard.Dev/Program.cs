using System;
using System.IO;
using System.Reflection;
using LidGuard;

namespace LidGuard.Dev;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        string repoRoot = ResolveRepositoryRoot();
        string appDataRoot = Path.Combine(repoRoot, ".dev-data");
        string logRoot = Path.Combine(repoRoot, "logs");

        Directory.CreateDirectory(appDataRoot);
        Directory.CreateDirectory(logRoot);

        return LidGuardApplication.Run(
            new LidGuardRuntimeOptions
            {
                AppVersion = ResolveVersion(),
                EnableLogRetention = false,
                LogRoot = logRoot,
                Mode = AppRuntimeMode.Development,
                MutexName = @"Local\LidGuard.Dev",
                SettingsPath = Path.Combine(appDataRoot, "settings.json")
            }
        );
    }

    private static string ResolveVersion()
    {
        return Assembly.GetExecutingAssembly()
                   .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                   ?.InformationalVersion ?? "dev";
    }

    private static string ResolveRepositoryRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current.FullName;
            }

            string nestedRepoCandidate = Path.Combine(current.FullName, "LidGuard");

            if (Directory.Exists(Path.Combine(nestedRepoCandidate, ".git")))
            {
                return nestedRepoCandidate;
            }

            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
