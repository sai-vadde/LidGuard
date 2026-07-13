using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LidGuard;

public sealed class LidListenerContext : ApplicationContext
{
    private readonly LidMessageWindow _window;

    public LidListenerContext()
    {
        _window = new LidMessageWindow();
        AppLogger.LogStartup("Listener context created.");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _window.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed class LidMessageWindow : NativeWindow, IDisposable
    {
        private IntPtr _notificationHandle;
        private bool _lidOpen = true;
        private bool _handlingClose;
        private bool _handlingMonitorEvaluation;
        private readonly System.Windows.Forms.Timer _monitorPollTimer;
        private ExternalMonitorState _lastExternalMonitorState =
            ExternalMonitorState.Unknown;
        private DateTime _lastProjectionSwitch = DateTime.MinValue;

        internal LidMessageWindow()
        {
            CreateHandle(new CreateParams
            {
                Caption = "LidGuardMessageWindow"
            });

            AppLogger.LogStartup("Hidden message window created.");

            Guid lidGuid = NativeMethods.LidSwitchStateChangeGuid;

            _notificationHandle =
                NativeMethods.RegisterPowerSettingNotification(
                    Handle,
                    ref lidGuid,
                    NativeMethods.DEVICE_NOTIFY_WINDOW_HANDLE
                );

            if (_notificationHandle == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                DestroyHandle();
                AppLogger.LogStartup(
                    $"Failed to register lid power notification. Windows error: {error}."
                );

                throw new InvalidOperationException(
                    $"Could not register the lid switch notification. Windows error: {error}"
                );
            }

            AppLogger.LogLid("Registered for lid switch power notifications.");

            _monitorPollTimer = new System.Windows.Forms.Timer
            {
                Interval = 5000
            };

            _monitorPollTimer.Tick += (_, _) =>
            {
                _ = EvaluateExternalMonitorStateAsync(
                    "timer fallback",
                    0
                );
            };

            _monitorPollTimer.Start();
            AppLogger.LogDisplay("Started the 5 second monitor-state fallback timer.");
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == NativeMethods.WM_POWERBROADCAST &&
                message.WParam.ToInt32() ==
                    NativeMethods.PBT_POWERSETTINGCHANGE)
            {
                AppLogger.LogLid(
                    "Received WM_POWERBROADCAST power setting change notification."
                );
                ProcessPowerSettingChange(message.LParam);
            }

            if (message.Msg == NativeMethods.WM_DISPLAYCHANGE)
            {
                AppLogger.LogDisplay(
                    "Received WM_DISPLAYCHANGE notification."
                );
                _ = EvaluateExternalMonitorStateAsync(
                    "display-change notification",
                    1500
                );
            }

            base.WndProc(ref message);
        }

        private void ProcessPowerSettingChange(IntPtr parameter)
        {
            if (parameter == IntPtr.Zero)
            {
                AppLogger.LogLid(
                    "Power setting change notification had no payload."
                );
                return;
            }

            var setting =
                Marshal.PtrToStructure<NativeMethods.PowerBroadcastSetting>(
                    parameter
                );

            if (setting.PowerSetting !=
                NativeMethods.LidSwitchStateChangeGuid)
            {
                return;
            }

            int dataOffset =
                Marshal.SizeOf<NativeMethods.PowerBroadcastSetting>();

            byte lidState = Marshal.ReadByte(parameter, dataOffset);

            const byte LidClosed = 0;
            const byte LidOpen = 1;

            if (lidState == LidClosed)
            {
                _lidOpen = false;
                AppLogger.LogLid("Lid state changed to closed.");
                _ = HandleLidClosedAsync();
            }
            else if (lidState == LidOpen)
            {
                _lidOpen = true;
                AppLogger.LogLid("Lid state changed to open.");
                _ = EvaluateExternalMonitorStateAsync("lid open", 1500);
            }
        }

        private async Task EvaluateExternalMonitorStateAsync(
            string trigger,
            int delayMilliseconds
        )
        {
            if (!_lidOpen)
            {
                AppLogger.LogDisplay(
                    $"Open-lid monitor-state evaluation for {trigger} was skipped because the lid is closed."
                );
                return;
            }

            if (_handlingMonitorEvaluation)
            {
                AppLogger.LogDisplay(
                    $"Skipping monitor-state evaluation for {trigger} because a previous evaluation is still running."
                );
                return;
            }

            _handlingMonitorEvaluation = true;
            AppLogger.LogDisplay(
                $"Starting monitor-state evaluation for {trigger}."
            );

            try
            {
                if (delayMilliseconds > 0)
                {
                    await Task.Delay(delayMilliseconds);
                }

                ExternalMonitorState state =
                    ExternalMonitorDetector.GetState();

                AppLogger.LogDisplay(
                    $"Unified external monitor state is {state} while the lid is " +
                    (_lidOpen ? "open." : "closed.")
                );

                await HandleOpenLidStateAsync(state);
                _lastExternalMonitorState = state;
            }
            catch (Exception exception)
            {
                AppLogger.LogError(
                    "monitor",
                    $"Monitor-state evaluation failed for {trigger}.",
                    exception
                );
            }
            finally
            {
                _handlingMonitorEvaluation = false;
                AppLogger.LogDisplay(
                    $"Monitor-state evaluation finished for {trigger}."
                );
            }
        }

        private async Task HandleLidClosedAsync()
        {
            if (_handlingClose)
            {
                AppLogger.LogLid(
                    "Skipping lid-close handling because a previous close verification is still running."
                );
                return;
            }

            _handlingClose = true;

            AppLogger.LogLid(
                "Starting rapid lid-close monitor verification."
            );

            try
            {
                await Task.Delay(500);

                ExternalMonitorState state =
                    await VerifyExternalMonitorForLidCloseAsync();

                if (state == ExternalMonitorState.On)
                {
                    AppLogger.LogLid(
                        "An external monitor is confirmed on, so hibernation will be skipped."
                    );
                    return;
                }

                AppLogger.LogLid(
                    $"External monitor state is {state}. Hibernation will be requested."
                );

                LidGuardSettings settings = SettingsManager.Load();

                if (settings.RespectAgentHolds &&
                    WakeIntentRegistry.HasActiveSuspendHold(out string? holdReason))
                {
                    AppLogger.LogLid(
                        "Hibernation skipped: a cooperating product has an active " +
                        $"keep-awake hold ({holdReason})."
                    );
                    return;
                }

                await Task.Yield();
                HibernateManager.Hibernate();
            }
            catch (Exception exception)
            {
                AppLogger.LogError(
                    "lid",
                    "Rapid lid-close handling failed.",
                    exception
                );
            }
            finally
            {
                _handlingClose = false;
                AppLogger.LogLid(
                    "Rapid lid-close monitor verification finished."
                );
            }
        }

        private async Task HandleOpenLidStateAsync(
            ExternalMonitorState state
        )
        {
            if (_lastExternalMonitorState != ExternalMonitorState.On &&
                state == ExternalMonitorState.On)
            {
                LidGuardSettings settings = SettingsManager.Load();

                if (settings.DuplicateModeWasForced)
                {
                    AppLogger.LogDisplay(
                        "An external monitor became available again after LidGuard had forced Duplicate mode. Restoring the saved projection mode."
                    );

                    await Task.Yield();
                    DisplayModeManager.RestorePreviousProjection();
                }
            }

            if (state == ExternalMonitorState.On)
            {
                AppLogger.LogDisplay(
                    "An external monitor is on. The current display mode will be kept."
                );
                return;
            }

            if (state == ExternalMonitorState.Unknown)
            {
                AppLogger.LogDisplay(
                    "External monitor power state is unknown, so no display mode change will be made."
                );
                return;
            }

            bool becameUnavailable =
                _lastExternalMonitorState == ExternalMonitorState.On ||
                _lastExternalMonitorState == ExternalMonitorState.Unknown;

            if (!becameUnavailable && _lastExternalMonitorState == state)
            {
                AppLogger.LogDisplay(
                    "The external monitor is still unavailable and the state has not changed."
                );
                return;
            }

            if (DateTime.UtcNow - _lastProjectionSwitch <
                TimeSpan.FromSeconds(5))
            {
                AppLogger.LogDisplay(
                    "Projection mode change skipped because the last switch happened too recently."
                );
                return;
            }

            _lastProjectionSwitch = DateTime.UtcNow;

            AppLogger.LogDisplay(
                $"External monitor state is {state}. Switching to Duplicate mode."
            );

            await Task.Yield();
            DisplayModeManager.ForceConfiguredRecoveryMode();
        }

        private static async Task<ExternalMonitorState>
            VerifyExternalMonitorForLidCloseAsync()
        {
            const int attempts = 3;
            const int delayMilliseconds = 400;

            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                var activeExternalDisplays =
                    ActiveExternalDisplayDetector.GetActiveExternalDisplays();

                if (activeExternalDisplays.Count == 0)
                {
                    AppLogger.LogLid(
                        "Lid-close verification found no active external display paths."
                    );

                    return ExternalMonitorState.None;
                }

                ExternalMonitorState state =
                    MonitorPowerDetector.GetState(activeExternalDisplays);

                AppLogger.LogLid(
                    $"Lid-close DDC/CI verification attempt {attempt}/{attempts}: {state}."
                );

                if (state is ExternalMonitorState.On or
                    ExternalMonitorState.Off or
                    ExternalMonitorState.None)
                {
                    return state;
                }

                if (attempt < attempts)
                {
                    await Task.Delay(delayMilliseconds);
                }
            }

            AppLogger.LogLid(
                "All rapid DDC/CI lid-close verification attempts returned Unknown. " +
                "The external monitor will be treated as unavailable."
            );

            return ExternalMonitorState.Off;
        }

        public void Dispose()
        {
            _monitorPollTimer.Stop();
            _monitorPollTimer.Dispose();
            AppLogger.LogDisplay("Stopped the monitor-state fallback timer.");

            if (_notificationHandle != IntPtr.Zero)
            {
                AppLogger.LogLid(
                    "Unregistering lid switch power notifications."
                );
                NativeMethods.UnregisterPowerSettingNotification(
                    _notificationHandle
                );

                _notificationHandle = IntPtr.Zero;
            }

            DestroyHandle();
            AppLogger.LogStartup("Hidden message window destroyed.");
        }
    }
}
