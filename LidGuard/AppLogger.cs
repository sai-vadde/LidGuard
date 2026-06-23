using System;
using System.Collections.Generic;
using System.IO;

namespace LidGuard;

internal static class AppLogger
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromDays(1);
    private static readonly TimeSpan DuplicateSuppressionWindow =
        TimeSpan.FromSeconds(30);
    private static readonly string ErrorLogFileName = "errors.txt";
    private static readonly TimeSpan LogRetentionPeriod =
        TimeSpan.FromDays(7);
    private static readonly string[] LogFiles =
    [
        "startup.txt",
        "lid.txt",
        "display.txt",
        "hibernate.txt",
        ErrorLogFileName
    ];
    private static readonly Dictionary<string, LogSuppressionState>
        SuppressionStates = [];
    private static readonly object SyncRoot = new();
    private static bool _initialized;
    private static DateTime _lastCleanupUtc = DateTime.MinValue;

    internal static void Initialize()
    {
        EnsureInitialized();
    }

    internal static void LogStartup(string message)
    {
        Write("startup.txt", "startup", message);
    }

    internal static void LogLid(string message)
    {
        Write("lid.txt", "lid", message);
    }

    internal static void LogDisplay(string message)
    {
        Write("display.txt", "display", message);
    }

    internal static void LogHibernate(string message)
    {
        Write("hibernate.txt", "hibernate", message);
    }

    internal static void LogError(
        string subsystem,
        string context,
        Exception exception
    )
    {
        try
        {
            EnsureInitialized();

            string logPath = Path.Combine(
                AppEnvironment.LogRoot,
                ErrorLogFileName
            );

            string entry =
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] " +
                $"[error] [{subsystem}] {context}{Environment.NewLine}" +
                $"{exception}{Environment.NewLine}" +
                $"{new string('-', 80)}{Environment.NewLine}";

            lock (SyncRoot)
            {
                File.AppendAllText(logPath, entry);
            }

            RunCleanupIfDue(force: false);
        }
        catch
        {
            // Logging must never crash the app.
        }
    }

    private static void Write(
        string fileName,
        string subsystem,
        string message
    )
    {
        try
        {
            EnsureInitialized();

            string logPath = Path.Combine(AppEnvironment.LogRoot, fileName);
            string entryPrefix =
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{subsystem}] ";

            lock (SyncRoot)
            {
                FlushSuppressedMessageIfNeeded(
                    fileName,
                    logPath,
                    entryPrefix,
                    message
                );

                if (ShouldSuppressDuplicate(fileName, message))
                {
                    return;
                }

                File.AppendAllText(
                    logPath,
                    $"{entryPrefix}{message}{Environment.NewLine}"
                );
            }

            RunCleanupIfDue(force: false);
        }
        catch
        {
            // Logging must never crash the app.
        }
    }

    private static void EnsureInitialized()
    {
        try
        {
            lock (SyncRoot)
            {
                if (_initialized)
                {
                    return;
                }

                Directory.CreateDirectory(AppEnvironment.LogRoot);

                foreach (string logFile in LogFiles)
                {
                    string logPath = Path.Combine(
                        AppEnvironment.LogRoot,
                        logFile
                    );

                    if (!File.Exists(logPath))
                    {
                        using FileStream _ = File.Create(logPath);
                    }
                }

                _initialized = true;
            }

            RunCleanupIfDue(force: true);
        }
        catch
        {
            // Logging must never crash the app.
        }
    }

    private static void FlushSuppressedMessageIfNeeded(
        string fileName,
        string logPath,
        string entryPrefix,
        string newMessage
    )
    {
        if (!SuppressionStates.TryGetValue(
                fileName,
                out LogSuppressionState? state
            ) ||
            state.SuppressedCount == 0 ||
            string.Equals(
                state.Message,
                newMessage,
                StringComparison.Ordinal
            ))
        {
            return;
        }

        File.AppendAllText(
            logPath,
            $"{entryPrefix}Previous message repeated {state.SuppressedCount} additional times.{Environment.NewLine}"
        );

        state.SuppressedCount = 0;
    }

    private static void RunCleanupIfDue(bool force)
    {
        try
        {
            if (!AppEnvironment.EnableLogRetention)
            {
                return;
            }

            DateTime now = DateTime.UtcNow;

            lock (SyncRoot)
            {
                if (!force &&
                    _lastCleanupUtc != DateTime.MinValue &&
                    now - _lastCleanupUtc < CleanupInterval)
                {
                    return;
                }

                if (!Directory.Exists(AppEnvironment.LogRoot))
                {
                    _lastCleanupUtc = now;
                    return;
                }

                foreach (string filePath in Directory.GetFiles(
                             AppEnvironment.LogRoot,
                             "*.txt"
                         ))
                {
                    DateTime lastWriteUtc =
                        File.GetLastWriteTimeUtc(filePath);

                    if (now - lastWriteUtc > LogRetentionPeriod)
                    {
                        File.Delete(filePath);
                    }
                }

                _lastCleanupUtc = now;
            }
        }
        catch
        {
            // Cleanup must never crash the app.
        }
    }

    private static bool ShouldSuppressDuplicate(
        string fileName,
        string message
    )
    {
        DateTime now = DateTime.UtcNow;

        if (!SuppressionStates.TryGetValue(
                fileName,
                out LogSuppressionState? state
            ))
        {
            SuppressionStates[fileName] = new LogSuppressionState
            {
                LastWrittenUtc = now,
                Message = message
            };

            return false;
        }

        if (string.Equals(state.Message, message, StringComparison.Ordinal) &&
            now - state.LastWrittenUtc <= DuplicateSuppressionWindow)
        {
            state.SuppressedCount++;
            return true;
        }

        state.Message = message;
        state.LastWrittenUtc = now;
        state.SuppressedCount = 0;
        return false;
    }

    private sealed class LogSuppressionState
    {
        internal DateTime LastWrittenUtc { get; set; }

        internal string Message { get; set; } = string.Empty;

        internal int SuppressedCount { get; set; }
    }
}
