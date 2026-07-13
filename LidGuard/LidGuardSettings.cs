using System.Text.Json.Serialization;

namespace LidGuard;

public sealed class LidGuardSettings
{
    public ProjectionMode? LastProjectionMode { get; set; }

    public ProjectionMode DefaultRestoreProjectionMode { get; set; } =
        ProjectionMode.Duplicate;

    public RecoveryMode RecoveryMode { get; set; } = RecoveryMode.Duplicate;

    public bool DuplicateModeWasForced { get; set; }

    /// <summary>
    /// When true (default), LidGuard skips hibernation on lid close while a
    /// cooperating product (e.g. AgentKeep) has published an active
    /// no-forced-suspend wake intent. See <see cref="WakeIntentRegistry"/>.
    /// </summary>
    public bool RespectAgentHolds { get; set; } = true;

    [JsonPropertyName("InternalModeWasForced")]
    public bool LegacyInternalModeWasForced
    {
        get => DuplicateModeWasForced;
        set => DuplicateModeWasForced = value;
    }
}
