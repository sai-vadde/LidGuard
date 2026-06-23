using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LidGuard.Production;

internal static class InstallStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true
    };

    internal static InstallState? Load(AppInstallScope scope)
    {
        string path = ProductionPaths.GetInstallStatePath(scope);

        if (!File.Exists(path))
        {
            return null;
        }

        string json = File.ReadAllText(path);

        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<InstallState>(json, JsonOptions);
    }

    internal static void Save(AppInstallScope scope, InstallState state)
    {
        state.UpdatedAtUtc = DateTimeOffset.UtcNow;

        string path = ProductionPaths.GetInstallStatePath(scope);
        string folder = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(folder);

        string json = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(path, json);
    }

    internal static void Delete(AppInstallScope scope)
    {
        string path = ProductionPaths.GetInstallStatePath(scope);

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
