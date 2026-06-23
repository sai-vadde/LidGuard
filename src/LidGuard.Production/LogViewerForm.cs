using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace LidGuard.Production;

internal sealed class LogViewerForm : Form
{
    private static readonly IReadOnlyList<KeyValuePair<string, string>> Logs =
    [
        new("Startup", "startup.txt"),
        new("Lid", "lid.txt"),
        new("Display", "display.txt"),
        new("Hibernate", "hibernate.txt"),
        new("Errors", "errors.txt")
    ];

    private readonly Button _copyButton;
    private readonly Button _findNextButton;
    private readonly RichTextBox _logTextBox;
    private readonly ComboBox _logTypeComboBox;
    private readonly TextBox _searchTextBox;
    private bool _allowClose;
    private int _lastSearchIndex;

    internal LogViewerForm()
    {
        Font = new Font("Segoe UI", 9F);
        StartPosition = FormStartPosition.CenterScreen;
        Text = "LidGuard Logs";
        ClientSize = new Size(900, 560);

        var headerPanel = new TableLayoutPanel
        {
            ColumnCount = 6,
            Dock = DockStyle.Top,
            Height = 42,
            Padding = new Padding(8)
        };
        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180F));
        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _logTypeComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _logTypeComboBox.DisplayMember = "Key";
        _logTypeComboBox.ValueMember = "Value";

        foreach (KeyValuePair<string, string> log in Logs)
        {
            _logTypeComboBox.Items.Add(log);
        }

        _logTypeComboBox.SelectedIndexChanged += (_, _) =>
        {
            _lastSearchIndex = 0;
            RefreshCurrentLog();
        };

        var refreshButton = new Button
        {
            AutoSize = true,
            Text = "Refresh"
        };
        refreshButton.Click += (_, _) => RefreshCurrentLog();

        _searchTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "Search log text"
        };
        _searchTextBox.TextChanged += (_, _) => _lastSearchIndex = 0;

        _logTextBox = new RichTextBox
        {
            BorderStyle = BorderStyle.FixedSingle,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9F),
            ReadOnly = true,
            WordWrap = false
        };

        _findNextButton = new Button
        {
            AutoSize = true,
            Text = "Find Next"
        };
        _findNextButton.Click += (_, _) => FindNext();

        _copyButton = new Button
        {
            AutoSize = true,
            Text = "Copy All"
        };
        _copyButton.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_logTextBox.Text))
            {
                try
                {
                    Clipboard.SetText(_logTextBox.Text);
                }
                catch (Exception exception)
                {
                    MessageBox.Show(
                        exception.Message,
                        "LidGuard",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }
            }
        };

        headerPanel.Controls.Add(
            new Label
            {
                AutoSize = true,
                Text = "Log",
                TextAlign = ContentAlignment.MiddleLeft
            },
            0,
            0
        );
        headerPanel.Controls.Add(_logTypeComboBox, 1, 0);
        headerPanel.Controls.Add(refreshButton, 2, 0);
        headerPanel.Controls.Add(_searchTextBox, 3, 0);
        headerPanel.Controls.Add(_findNextButton, 4, 0);
        headerPanel.Controls.Add(_copyButton, 5, 0);

        Controls.Add(_logTextBox);
        Controls.Add(headerPanel);

        _logTypeComboBox.SelectedIndex = 0;
    }

    internal void ForceClose()
    {
        _allowClose = true;
        Close();
    }

    internal void RefreshCurrentLog()
    {
        if (_logTypeComboBox.SelectedItem is not KeyValuePair<string, string> log)
        {
            return;
        }

        string logFileName = log.Value ?? string.Empty;
        string logPath = Path.Combine(ProductionPaths.LogRoot, logFileName);

        _logTextBox.Text = File.Exists(logPath)
            ? File.ReadAllText(logPath)
            : "No log entries are available yet.";
        _logTextBox.SelectionLength = 0;
        _logTextBox.SelectionStart = 0;
        _lastSearchIndex = 0;
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

    private void FindNext()
    {
        string searchText = _searchTextBox.Text;

        if (string.IsNullOrWhiteSpace(searchText) ||
            string.IsNullOrWhiteSpace(_logTextBox.Text))
        {
            return;
        }

        int index = _logTextBox.Text.IndexOf(
            searchText,
            _lastSearchIndex,
            StringComparison.OrdinalIgnoreCase
        );

        if (index < 0 && _lastSearchIndex > 0)
        {
            _lastSearchIndex = 0;
            index = _logTextBox.Text.IndexOf(
                searchText,
                0,
                StringComparison.OrdinalIgnoreCase
            );
        }

        if (index < 0)
        {
            return;
        }

        _logTextBox.Focus();
        _logTextBox.SelectionStart = index;
        _logTextBox.SelectionLength = searchText.Length;
        _logTextBox.ScrollToCaret();

        _lastSearchIndex = index + searchText.Length;
    }
}
