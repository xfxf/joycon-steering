using System.Drawing;
using System.Windows.Forms;

namespace JoyconSteering.Ui;

internal sealed class DiagnosticsForm : Form
{
    /// <summary>Raised when the user confirms quit from the close-button dialog.</summary>
    public event Action? QuitRequested;

    private readonly Label _statusLabel;
    private readonly Label _angleLabel;
    private readonly Label _steerLabel;
    private readonly Label _stickYLabel;
    private readonly Label _batteryLabel;
    private readonly Label _errorLabel;
    private readonly ProgressBar _steerBar;
    private readonly SteeringWorker _worker;
    private readonly System.Windows.Forms.Timer _timer;
    private bool _confirmedQuit;

    public DiagnosticsForm(SteeringWorker worker)
    {
        _worker = worker;
        Text = "JoyconSteering";
        Width = 500;
        Height = 360;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = true;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;
        Font = new Font("Segoe UI", 10);

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 8,
            Padding = new Padding(12),
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < grid.RowCount; i++) grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _statusLabel = AddRow(grid, 0, "Status:");
        _angleLabel = AddRow(grid, 1, "Angle:");
        _steerLabel = AddRow(grid, 2, "Steer axis:");

        grid.Controls.Add(new Label { Text = "Steer bar:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 3);
        _steerBar = new ProgressBar
        {
            Dock = DockStyle.Fill,
            Minimum = 0,
            Maximum = 1000,
            Value = 500,
            Style = ProgressBarStyle.Continuous,
            Height = 24,
        };
        grid.Controls.Add(_steerBar, 1, 3);

        _stickYLabel = AddRow(grid, 4, "Stick Y:");
        _batteryLabel = AddRow(grid, 5, "Battery:");
        _errorLabel = AddRow(grid, 6, "Error:");
        _errorLabel.ForeColor = Color.Firebrick;

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Height = 38,
        };
        var recenter = new Button { Text = "Recenter", Width = 100 };
        var settings = new Button { Text = "Settings…", Width = 100 };
        recenter.Click += (s, e) => { _worker.Recenter(); Logger.Info("Recenter requested via UI"); };
        settings.Click += (s, e) => SettingsRequested?.Invoke();
        buttons.Controls.Add(recenter);
        buttons.Controls.Add(settings);
        grid.Controls.Add(buttons, 1, 7);

        Controls.Add(grid);

        _timer = new System.Windows.Forms.Timer { Interval = 50 };
        _timer.Tick += (s, e) => RefreshStatus();
        _timer.Start();
        Logger.Info("DiagnosticsForm shown");
    }

    public event Action? SettingsRequested;

    private static Label AddRow(TableLayoutPanel grid, int row, string label)
    {
        var key = new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        var val = new Label { Text = "—", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoSize = false, Height = 22, Font = new Font("Consolas", 10) };
        grid.Controls.Add(key, 0, row);
        grid.Controls.Add(val, 1, row);
        return val;
    }

    private void RefreshStatus()
    {
        var s = _worker.Status;
        _statusLabel.Text = s.Running ? "Running" : (s.ErrorMessage is null ? "Stopped" : "Stopped (error)");
        _statusLabel.ForeColor = s.ErrorMessage is not null ? Color.Firebrick
                              : s.Running ? Color.ForestGreen : Color.DimGray;
        _angleLabel.Text = $"{s.AngleDeg,7:F1}°";
        _steerLabel.Text = $"{s.Steer,+6:F2}";
        _steerBar.Value = (int)Math.Round((Math.Clamp(s.Steer, -1, 1) + 1) * 500);
        _stickYLabel.Text = $"{s.StickY,+5:F2}";
        _batteryLabel.Text = s.Battery.ToString();
        _errorLabel.Text = s.ErrorMessage ?? "";
    }

    /// <summary>Show window from tray (also restores from minimized).</summary>
    public void ShowFromTray()
    {
        if (!Visible) Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        BringToFront();
        Activate();
        Logger.Info("Window restored from tray");
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Minimized)
        {
            Hide(); // sends to tray
            Logger.Info("Window minimized to tray");
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_confirmedQuit) { base.OnFormClosing(e); return; }

        if (e.CloseReason == CloseReason.UserClosing)
        {
            var result = MessageBox.Show(this,
                "Quit JoyconSteering?\n\nThe app will stop sending wheel input to games.",
                "Confirm Quit",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            if (result != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }
            Logger.Info("User confirmed quit");
            _confirmedQuit = true;
            QuitRequested?.Invoke();
        }
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timer.Stop();
        _timer.Dispose();
        base.OnFormClosed(e);
    }
}
