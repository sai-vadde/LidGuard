using System;
using System.Windows.Forms;

namespace LidGuard;

public sealed class LidGuardRuntimeOptions
{
    public string? ActivationSignalName { get; init; }

    public required string AppVersion { get; init; }

    public Func<ApplicationContext>? CreateApplicationContext { get; init; }

    public bool EnableLogRetention { get; init; }

    public required string LogRoot { get; init; }

    public required AppRuntimeMode Mode { get; init; }

    public required string MutexName { get; init; }

    public required string SettingsPath { get; init; }
}
