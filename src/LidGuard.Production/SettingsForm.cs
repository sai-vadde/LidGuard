using System;
using System.Drawing;
using System.Windows.Forms;
using LidGuard;

namespace LidGuard.Production;

internal sealed class SettingsForm : Form
{
    private readonly Button _applyButton;
    private readonly Button _closeButton;
    private readonly ComboBox _defaultRestoreModeComboBox;
    private readonly Label _statusLabel;
    private readonly ComboBox _recoveryModeComboBox;
    private readonly CheckBox _runAtLoginCheckBox;
    private readonly Label _startupScopeValueLabel;
    private readonly Button _viewLogsButton;
    private bool _allowClose;

    internal SettingsForm()
    {
        Font = new Font("Segoe UI", 9F);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Text = "LidGuard Settings";
        ClientSize = new Size(460, 300);

        var layout = new TableLayoutPanel
        {
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            RowCount = 6
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));

        _runAtLoginCheckBox = new CheckBox
        {
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            Text = "Enable"
        };

        _startupScopeValueLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _recoveryModeComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _recoveryModeComboBox.Items.AddRange(
            [RecoveryMode.Duplicate, RecoveryMode.Internal]
        );

        _defaultRestoreModeComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _defaultRestoreModeComboBox.Items.AddRange(
            [ProjectionMode.Duplicate, ProjectionMode.Extend]
        );

        _viewLogsButton = new Button
        {
            AutoSize = true,
            Text = "View Logs"
        };
        _viewLogsButton.Click += (_, _) => ViewLogsRequested?.Invoke();

        _statusLabel = new Label
        {
            AutoEllipsis = true,
            AutoSize = true,
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(36, 99, 235),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _applyButton = new Button
        {
            AutoSize = true,
            Text = "Apply"
        };
        _applyButton.Click += (_, _) => ApplyRequested?.Invoke(BuildState());

        _closeButton = new Button
        {
            AutoSize = true,
            Text = "Close"
        };
        _closeButton.Click += (_, _) => Hide();

        AddRow(layout, 0, "Run at login", _runAtLoginCheckBox);
        AddRow(layout, 1, "Startup scope", _startupScopeValueLabel);
        AddRow(layout, 2, "Recovery mode", _recoveryModeComboBox);
        AddRow(
            layout,
            3,
            "Monitor wake fallback",
            _defaultRestoreModeComboBox
        );
        AddRow(layout, 4, "Diagnostics", _viewLogsButton);

        var footerPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        footerPanel.Controls.Add(_closeButton);
        footerPanel.Controls.Add(_applyButton);

        layout.Controls.Add(_statusLabel, 0, 5);
        layout.Controls.Add(footerPanel, 1, 5);

        Controls.Add(layout);
    }

    internal event Action<ProductionSettingsState>? ApplyRequested;

    internal event Action? ViewLogsRequested;

    internal void ForceClose()
    {
        _allowClose = true;
        Close();
    }

    internal void LoadState(ProductionSettingsState state)
    {
        _runAtLoginCheckBox.Checked = state.RunAtLoginEnabled;
        _startupScopeValueLabel.Text = state.StartupScope switch
        {
            AppInstallScope.AllUsers => "All users",
            _ => "Current user"
        };
        _recoveryModeComboBox.SelectedItem = state.RecoveryMode;
        _defaultRestoreModeComboBox.SelectedItem =
            state.DefaultRestoreProjectionMode;
        _statusLabel.Text = string.Empty;
    }

    internal void SetStatusMessage(
        string message,
        bool isError
    )
    {
        _statusLabel.ForeColor = isError
            ? Color.Firebrick
            : Color.FromArgb(36, 99, 235);
        _statusLabel.Text = message;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowClose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnFormClosing(e);
    }

    private ProductionSettingsState BuildState()
    {
        return new ProductionSettingsState
        {
            DefaultRestoreProjectionMode =
                (ProjectionMode)_defaultRestoreModeComboBox.SelectedItem!,
            RecoveryMode = (RecoveryMode)_recoveryModeComboBox.SelectedItem!,
            RunAtLoginEnabled = _runAtLoginCheckBox.Checked,
            StartupScope = string.Equals(
                _startupScopeValueLabel.Text,
                "All users",
                StringComparison.Ordinal
            )
                ? AppInstallScope.AllUsers
                : AppInstallScope.CurrentUser
        };
    }

    private static void AddRow(
        TableLayoutPanel layout,
        int rowIndex,
        string labelText,
        Control control
    )
    {
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));

        var label = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Text = labelText,
            TextAlign = ContentAlignment.MiddleLeft
        };

        control.Anchor = AnchorStyles.Left | AnchorStyles.Right;

        layout.Controls.Add(label, 0, rowIndex);
        layout.Controls.Add(control, 1, rowIndex);
    }
}
