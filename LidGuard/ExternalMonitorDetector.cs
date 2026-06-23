using System;

namespace LidGuard;

internal static class ExternalMonitorDetector
{
    private static readonly object SyncRoot = new();
    private static DateTime _firstConsecutiveFailureAtUtc = DateTime.MinValue;
    private static ExternalMonitorState _lastConfirmedState =
        ExternalMonitorState.Unknown;

    internal static ExternalMonitorState GetState()
    {
        var activeExternalDisplays =
            ActiveExternalDisplayDetector.GetActiveExternalDisplays();

        if (activeExternalDisplays.Count == 0)
        {
            lock (SyncRoot)
            {
                _firstConsecutiveFailureAtUtc = DateTime.MinValue;
                _lastConfirmedState = ExternalMonitorState.None;
            }

            AppLogger.LogDisplay(
                "No active external display paths were found."
            );

            return ExternalMonitorState.None;
        }

        ExternalMonitorState ddcState =
            MonitorPowerDetector.GetState(activeExternalDisplays);

        if (ddcState is ExternalMonitorState.On or ExternalMonitorState.Off)
        {
            lock (SyncRoot)
            {
                _firstConsecutiveFailureAtUtc = DateTime.MinValue;
                _lastConfirmedState = ddcState;
            }

            AppLogger.LogDisplay(
                $"Unified external monitor state resolved to {ddcState}."
            );

            return ddcState;
        }

        lock (SyncRoot)
        {
            DateTime now = DateTime.UtcNow;

            if (_firstConsecutiveFailureAtUtc == DateTime.MinValue)
            {
                _firstConsecutiveFailureAtUtc = now;
            }

            TimeSpan failureDuration = now - _firstConsecutiveFailureAtUtc;

            if (_lastConfirmedState is ExternalMonitorState.On or
                ExternalMonitorState.Off &&
                failureDuration < TimeSpan.FromSeconds(10))
            {
                AppLogger.LogDisplay(
                    "DDC/CI monitor power queries failed, so the last confirmed state " +
                    $"({_lastConfirmedState}) will be reused during the 10 second grace period."
                );

                return _lastConfirmedState;
            }

            if (failureDuration >= TimeSpan.FromSeconds(10))
            {
                AppLogger.LogDisplay(
                    "DDC/CI monitor power queries have failed for at least 10 seconds. " +
                    "The external monitor will be treated as unavailable."
                );

                _lastConfirmedState = ExternalMonitorState.Off;
                return ExternalMonitorState.Off;
            }
        }

        AppLogger.LogDisplay(
            "DDC/CI monitor power queries are currently unavailable and there is no " +
            "confirmed monitor power state yet."
        );

        return ExternalMonitorState.Unknown;
    }
}
