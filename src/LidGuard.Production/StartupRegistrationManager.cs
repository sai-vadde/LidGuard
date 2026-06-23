using System;
using Microsoft.Win32;

namespace LidGuard.Production;

internal static class StartupRegistrationManager
{
    private const string RunKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "LidGuard";

    internal static bool IsRegistered(AppInstallScope scope)
    {
        using RegistryKey? runKey = OpenRunKey(scope, writable: false);
        object? value = runKey?.GetValue(ValueName);

        return value is string registrationValue &&
               !string.IsNullOrWhiteSpace(registrationValue);
    }

    internal static void RegisterCurrentExecutable(AppInstallScope scope)
    {
        RegisterExecutable(scope, ProductionPaths.GetCurrentExecutablePath());
    }

    internal static void RegisterExecutable(
        AppInstallScope scope,
        string executablePath
    )
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new InvalidOperationException(
                "The executable path could not be determined."
            );
        }

        using RegistryKey runKey = OpenRunKey(scope, writable: true) ??
                                   throw new InvalidOperationException(
                                       "The Windows Run key could not be opened."
                                   );

        runKey.SetValue(ValueName, Quote(executablePath));
    }

    internal static void SetEnabled(
        AppInstallScope scope,
        bool enabled
    )
    {
        if (enabled)
        {
            RegisterCurrentExecutable(scope);
            return;
        }

        Unregister(scope);
    }

    internal static void Unregister(AppInstallScope scope)
    {
        using RegistryKey? runKey = OpenRunKey(scope, writable: true);
        runKey?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private static RegistryKey? OpenRunKey(
        AppInstallScope scope,
        bool writable
    )
    {
        return scope switch
        {
            AppInstallScope.AllUsers => writable
                ? Registry.LocalMachine.CreateSubKey(RunKeyPath)
                : Registry.LocalMachine.OpenSubKey(RunKeyPath, writable: false),
            _ => writable
                ? Registry.CurrentUser.CreateSubKey(RunKeyPath)
                : Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false)
        };
    }

    private static string Quote(string path)
    {
        return string.Concat('"', path, '"');
    }
}
