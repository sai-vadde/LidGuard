using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using LidGuard;

namespace LidGuard.Production;

internal sealed class ProductionApplicationContext : ApplicationContext
{
    private readonly EventWaitHandle _activationEvent;
    private readonly Thread _activationListenerThread;
    private readonly Control _dispatcher;
    private readonly LidListenerContext _listenerContext;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _runAtLoginMenuItem;
    private bool _isDisposing;
    private LogViewerForm? _logViewerForm;
    private SettingsForm? _settingsForm;
    private readonly AppInstallScope _startupScope;

    internal ProductionApplicationContext(string activationSignalName)
    {
        _startupScope = ProductionPaths.ResolveRuntimeScope();
        _dispatcher = new Control();
        _dispatcher.CreateControl();

        _listenerContext = new LidListenerContext();

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(
            new ToolStripMenuItem("Open Settings", null, (_, _) => OpenSettingsWindow())
        );
        contextMenu.Items.Add(
            new ToolStripMenuItem("View Logs", null, (_, _) => OpenLogViewer())
        );

        _runAtLoginMenuItem = new ToolStripMenuItem("Run at login")
        {
            Checked = StartupRegistrationManager.IsRegistered(_startupScope),
            CheckOnClick = true
        };
        _runAtLoginMenuItem.Click += (_, _) => ToggleRunAtLoginFromTray();
        contextMenu.Items.Add(_runAtLoginMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(
            new ToolStripMenuItem("Exit", null, (_, _) => ExitThread())
        );

        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = contextMenu,
            Icon = SystemIcons.Application,
            Text = "LidGuard",
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => OpenSettingsWindow();

        _activationEvent = new EventWaitHandle(
            initialState: false,
            mode: EventResetMode.AutoReset,
            name: activationSignalName
        );

        _activationListenerThread = new Thread(ListenForActivation)
        {
            IsBackground = true,
            Name = "LidGuardActivationListener"
        };
        _activationListenerThread.Start();
    }

    protected override void ExitThreadCore()
    {
        _isDisposing = true;

        _activationEvent.Set();
        _activationListenerThread.Join(millisecondsTimeout: 1000);
        _activationEvent.Dispose();

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();

        _settingsForm?.ForceClose();
        _settingsForm?.Dispose();
        _logViewerForm?.ForceClose();
        _logViewerForm?.Dispose();

        _listenerContext.Dispose();
        _dispatcher.Dispose();

        base.ExitThreadCore();
    }

    private ProductionSettingsState BuildSettingsState()
    {
        LidGuardSettings settings = SettingsManager.Load();

        return new ProductionSettingsState
        {
            DefaultRestoreProjectionMode =
                settings.DefaultRestoreProjectionMode,
            RecoveryMode = settings.RecoveryMode,
            RunAtLoginEnabled =
                StartupRegistrationManager.IsRegistered(_startupScope),
            StartupScope = _startupScope
        };
    }

    private void ListenForActivation()
    {
        while (true)
        {
            _activationEvent.WaitOne();

            if (_isDisposing)
            {
                return;
            }

            if (_dispatcher.IsHandleCreated)
            {
                _dispatcher.BeginInvoke((Action)OpenSettingsWindow);
            }
        }
    }

    private void OpenLogViewer()
    {
        if (_logViewerForm is null || _logViewerForm.IsDisposed)
        {
            _logViewerForm = new LogViewerForm();
        }

        _logViewerForm.RefreshCurrentLog();
        _logViewerForm.Show();
        _logViewerForm.WindowState = FormWindowState.Normal;
        _logViewerForm.BringToFront();
        _logViewerForm.Activate();
    }

    private void OpenSettingsWindow()
    {
        if (_settingsForm is null || _settingsForm.IsDisposed)
        {
            _settingsForm = new SettingsForm();
            _settingsForm.ApplyRequested += ApplySettingsState;
            _settingsForm.ViewLogsRequested += OpenLogViewer;
        }

        _settingsForm.LoadState(BuildSettingsState());
        _settingsForm.Show();
        _settingsForm.WindowState = FormWindowState.Normal;
        _settingsForm.BringToFront();
        _settingsForm.Activate();
    }

    private void ApplySettingsState(ProductionSettingsState state)
    {
        try
        {
            LidGuardSettings settings = SettingsManager.Load();
            settings.RecoveryMode = state.RecoveryMode;
            settings.DefaultRestoreProjectionMode =
                state.DefaultRestoreProjectionMode;
            SettingsManager.Save(settings);

            StartupRegistrationManager.SetEnabled(
                _startupScope,
                state.RunAtLoginEnabled
            );

            _runAtLoginMenuItem.Checked = state.RunAtLoginEnabled;
            _settingsForm?.SetStatusMessage(
                "Settings were saved successfully.",
                isError: false
            );
        }
        catch (Exception exception)
        {
            _runAtLoginMenuItem.Checked = StartupRegistrationManager.IsRegistered(
                _startupScope
            );

            _settingsForm?.SetStatusMessage(
                exception.Message,
                isError: true
            );

            MessageBox.Show(
                exception.Message,
                "LidGuard",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private void ToggleRunAtLoginFromTray()
    {
        bool requestedState = _runAtLoginMenuItem.Checked;

        try
        {
            StartupRegistrationManager.SetEnabled(
                _startupScope,
                requestedState
            );

            if (_settingsForm is not null && !_settingsForm.IsDisposed)
            {
                _settingsForm.LoadState(BuildSettingsState());
                _settingsForm.SetStatusMessage(
                    "Startup behavior was updated.",
                    isError: false
                );
            }
        }
        catch (Exception exception)
        {
            _runAtLoginMenuItem.Checked = !requestedState;

            if (_settingsForm is not null && !_settingsForm.IsDisposed)
            {
                _settingsForm.SetStatusMessage(
                    exception.Message,
                    isError: true
                );
            }

            MessageBox.Show(
                exception.Message,
                "LidGuard",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }
}
