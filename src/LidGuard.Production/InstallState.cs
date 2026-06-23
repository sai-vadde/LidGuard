using System;
using System.Collections.Generic;

namespace LidGuard.Production;

internal sealed class InstallState
{
    public AppInstallScope InstallScope { get; set; }

    public AppInstallScope? StartupScope { get; set; }

    public bool StartupRegistered { get; set; }

    public string? ActiveSchemeId { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; } =
        DateTimeOffset.UtcNow;

    public List<PowerSchemeState> PowerSchemes { get; set; } = [];
}

internal sealed class PowerSchemeState
{
    public required string SchemeId { get; init; }

    public required uint AcLidAction { get; init; }

    public required uint DcLidAction { get; init; }
}
