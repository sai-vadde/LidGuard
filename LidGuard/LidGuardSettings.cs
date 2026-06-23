using System.Text.Json.Serialization;

namespace LidGuard;

public sealed class LidGuardSettings
{
    public ProjectionMode? LastProjectionMode { get; set; }

    public ProjectionMode DefaultRestoreProjectionMode { get; set; } =
        ProjectionMode.Duplicate;

    public RecoveryMode RecoveryMode { get; set; } = RecoveryMode.Duplicate;

    public bool DuplicateModeWasForced { get; set; }

    [JsonPropertyName("InternalModeWasForced")]
    public bool LegacyInternalModeWasForced
    {
        get => DuplicateModeWasForced;
        set => DuplicateModeWasForced = value;
    }
}
