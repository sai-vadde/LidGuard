using System.IO;
using System.Text.Json;

namespace LidGuard;

/// <summary>
/// Reads the vendor-neutral wake-intent registry (schema <c>power-intent/1</c>)
/// so LidGuard can skip hibernation while another product — e.g. AgentKeep —
/// has active work that must survive a lid close. This is advisory: LidGuard
/// decides whether to honor it (see <see cref="LidGuardSettings.RespectAgentHolds"/>).
///
/// The registry is a shared folder of one JSON file per active hold. LidGuard
/// only consults the strong <c>no-forced-suspend</c> intent, never the weak
/// idle-sleep layer, so ordinary apps (browsers, media) can never block a
/// deliberate lid-close hibernate. See the power-intent protocol in the
/// AgentKeep repo (docs/power-intent-protocol.md).
/// </summary>
internal static class WakeIntentRegistry
{
    private const string NoForcedSuspend = "no-forced-suspend";

    internal static string RegistryDirectory
    {
        get
        {
            string? overrideDir =
                Environment.GetEnvironmentVariable("POWER_INTENTS_DIR");

            if (!string.IsNullOrWhiteSpace(overrideDir))
            {
                return overrideDir;
            }

            string localAppData = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData
            );

            return Path.Combine(localAppData, "power-intents");
        }
    }

    /// <summary>
    /// True when at least one non-expired <c>no-forced-suspend</c> intent
    /// exists. Never throws: a missing folder, or any unreadable/corrupt/locked
    /// file, is treated as "no hold" and scanning continues.
    /// </summary>
    internal static bool HasActiveSuspendHold(out string? reason)
    {
        reason = null;
        string directory = RegistryDirectory;

        if (!Directory.Exists(directory))
        {
            return false;
        }

        // expires_at is epoch seconds (may be fractional); compare as double.
        double nowUnix =
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

        string[] files;
        try
        {
            files = Directory.GetFiles(directory, "*.json");
        }
        catch (Exception)
        {
            return false;
        }

        foreach (string file in files)
        {
            try
            {
                using JsonDocument document =
                    JsonDocument.Parse(File.ReadAllText(file));
                JsonElement root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!root.TryGetProperty("intent", out JsonElement intentElement) ||
                    intentElement.GetString() != NoForcedSuspend)
                {
                    continue;
                }

                if (!root.TryGetProperty("expires_at", out JsonElement expiresElement) ||
                    expiresElement.ValueKind != JsonValueKind.Number ||
                    !expiresElement.TryGetDouble(out double expiresAt) ||
                    expiresAt <= nowUnix)
                {
                    continue;
                }

                reason = ReadReason(root);
                return true;
            }
            catch (Exception)
            {
                // Corrupt/locked/partial file — ignore and keep scanning.
            }
        }

        return false;
    }

    private static string ReadReason(JsonElement root)
    {
        if (root.TryGetProperty("reason", out JsonElement reasonElement) &&
            reasonElement.GetString() is { Length: > 0 } reason)
        {
            return reason;
        }

        if (root.TryGetProperty("source", out JsonElement sourceElement) &&
            sourceElement.GetString() is { Length: > 0 } source)
        {
            return source;
        }

        return "an active wake intent";
    }
}
