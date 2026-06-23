using System.IO;
using System.Text.Json;

namespace LidGuard;

public static class SettingsManager
{
    private static readonly object SyncRoot = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static string SettingsPath => AppEnvironment.SettingsPath;

    public static LidGuardSettings Load()
    {
        lock (SyncRoot)
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    return new LidGuardSettings();
                }

                string json = File.ReadAllText(SettingsPath);

                if (string.IsNullOrWhiteSpace(json))
                {
                    return new LidGuardSettings();
                }

                LidGuardSettings settings =
                    JsonSerializer.Deserialize<LidGuardSettings>(
                        json,
                        JsonOptions
                    ) ??
                    new LidGuardSettings();

                Normalize(settings);
                return settings;
            }
            catch (Exception exception)
            {
                AppLogger.LogError(
                    "settings",
                    "Failed to load settings. Default settings will be used.",
                    exception
                );

                return new LidGuardSettings();
            }
        }
    }

    public static void Save(LidGuardSettings settings)
    {
        lock (SyncRoot)
        {
            try
            {
                Normalize(settings);

                string folder = Path.GetDirectoryName(SettingsPath)!;
                Directory.CreateDirectory(folder);

                string json = JsonSerializer.Serialize(settings, JsonOptions);
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception exception)
            {
                AppLogger.LogError(
                    "settings",
                    "Failed to save settings.",
                    exception
                );
            }
        }
    }

    private static void Normalize(LidGuardSettings settings)
    {
        if (settings.LastProjectionMode is not null &&
            settings.LastProjectionMode is not ProjectionMode.Duplicate &&
            settings.LastProjectionMode is not ProjectionMode.Extend)
        {
            settings.LastProjectionMode = ProjectionMode.Duplicate;
        }

        if (settings.DefaultRestoreProjectionMode is not ProjectionMode.Duplicate &&
            settings.DefaultRestoreProjectionMode is not ProjectionMode.Extend)
        {
            settings.DefaultRestoreProjectionMode = ProjectionMode.Duplicate;
        }
    }
}
