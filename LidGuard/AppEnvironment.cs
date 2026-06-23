namespace LidGuard;

internal static class AppEnvironment
{
    private static bool _initialized;

    internal static bool EnableLogRetention { get; private set; }

    internal static string LogRoot { get; private set; } = string.Empty;

    internal static AppRuntimeMode Mode { get; private set; }

    internal static string SettingsPath { get; private set; } = string.Empty;

    internal static string Version { get; private set; } = "unknown";

    internal static void Initialize(LidGuardRuntimeOptions options)
    {
        if (_initialized)
        {
            return;
        }

        Mode = options.Mode;
        LogRoot = options.LogRoot;
        SettingsPath = options.SettingsPath;
        EnableLogRetention = options.EnableLogRetention;
        Version = options.AppVersion;
        _initialized = true;
    }
}
