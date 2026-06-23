using LidGuard;

namespace LidGuard.Production;

internal sealed class ProductionSettingsState
{
    public required ProjectionMode DefaultRestoreProjectionMode { get; init; }

    public required RecoveryMode RecoveryMode { get; init; }

    public required bool RunAtLoginEnabled { get; init; }

    public required AppInstallScope StartupScope { get; init; }
}
