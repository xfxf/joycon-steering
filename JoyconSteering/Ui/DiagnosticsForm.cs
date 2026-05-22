using System.Drawing;
using System.Windows.Forms;
using JoyconSteering.Config;

namespace JoyconSteering.Ui;

internal sealed class DiagnosticsForm : Form
{
    public event Action? QuitRequested;
    public event Action? SettingsRequested;

    private readonly Label _statusLabel;
    private readonly Label _angleLabel;
    private readonly Label _steerLabel;
    private readonly Label _stickYLabel;
    private readonly Label _batteryLabel;
    private readonly Label _errorLabel;
    private readonly Label _pedalStatusLabel;
    private readonly Label _pedalValuesLabel;
    private readonly Label _throttleLabel;
    private readonly Label _brakeLabel;
    private readonly PositionBar _steerBar;
    private readonly PositionBar _throttleBar;
    private readonly PositionBar _brakeBar;
    private readonly SteeringWorker _worker;
    private readonly System.Windows.Forms.Timer _timer;
    private bool _confirmedQuit;
    private AppConfig _config;

    public DiagnosticsForm(SteeringWorker worker, AppConfig config)
    {
        _worker = worker;
        _config = config;
        Text = "JoyconSteering";
        Width = 520;
        Height = 480;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(500, 460);
        MinimizeBox = true;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;
        Font = new Font("Segoe UI", 10);

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _statusLabel  = AddLabelRow(grid, "Status:");
        _angleLabel   = AddLabelRow(grid, "Wheel angle:");
        _steerLabel   = AddLabelRow(grid, "Steer axis:");

        _steerBar = new PositionBar(bipolar: true, Color.FromArgb(35, 132, 213)) { Dock = DockStyle.Fill, Height = 22 };
        AddControlRow(grid, "Steering bar:", _steerBar);

        _throttleLabel = AddLabelRow(grid, "Throttle:");
        _throttleBar = new PositionBar(bipolar: false, Color.FromArgb(67, 160, 71)) { Dock = DockStyle.Fill, Height = 18 };
        AddControlRow(grid, "Throttle bar:", _throttleBar);

        _brakeLabel = AddLabelRow(grid, "Brake:");
        _brakeBar = new PositionBar(bipolar: false, Color.FromArgb(229, 57, 53)) { Dock = DockStyle.Fill, Height = 18 };
        AddControlRow(grid, "Brake bar:", _brakeBar);

        _stickYLabel      = AddLabelRow(grid, "Stick Y:");
        _batteryLabel     = AddLabelRow(grid, "Battery:");
        _pedalStatusLabel = AddLabelRow(grid, "Pedal JoyCon:");
        _pedalValuesLabel = AddLabelRow(grid, "  T/B/tilt:");
        _errorLabel       = AddLabelRow(grid, "Error:");
        _errorLabel.ForeColor = Color.Firebrick;

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            Dock = DockStyle.Fill,
            Height = 38,
            AutoSize = true,
        };
        var recenter = new Button { Text = "Recenter", Width = 100 };
        var settings = new Button { Text = "Settings…", Width = 100 };
        recenter.Click += (s, e) => { _worker.Recenter(); Logger.Info("Recenter requested via UI"); };
        settings.Click += (s, e) => SettingsRequested?.Invoke();
        buttons.Controls.Add(recenter);
        buttons.Controls.Add(settings);
        AddControlRow(grid, "", buttons);

        Controls.Add(grid);

        _timer = new System.Windows.Forms.Timer { Interval = 50 };
        _timer.Tick += (s, e) => RefreshStatus();
        _timer.Start();
        Logger.Info("DiagnosticsForm shown");
    }

    public void UpdateConfig(AppConfig newConfig)
    {
        _config = newConfig;
        ApplyDeadzoneVisuals();
    }

    private void ApplyDeadzoneVisuals()
    {
        // Steering deadzone in fraction of half-range
        double halfRange = Math.Max(1, _config.RangeDegrees);
        _steerBar.Deadzone = Math.Clamp(_config.DeadzoneDegrees / halfRange, 0, 1);

        // Throttle/brake deadzone visual
        switch (_config.ThrottleBrake)
        {
            case ThrottleBrakeMode.Stick:
                _throttleBar.Deadzone = _config.StickDeadzone;
                _brakeBar.Deadzone    = _config.StickDeadzone;
                break;
            case ThrottleBrakeMode.PedalTilt:
            {
                double tiltRange = Math.Max(1, _config.PedalTiltRangeDegrees);
                double dz = Math.Clamp(_config.PedalTiltDeadzoneDegrees / tiltRange, 0, 1);
                _throttleBar.Deadzone = dz;
                _brakeBar.Deadzone    = dz;
                break;
            }
            default:
                _throttleBar.Deadzone = 0;
                _brakeBar.Deadzone    = 0;
                break;
        }
    }

    private static Label AddLabelRow(TableLayoutPanel grid, string label)
    {
        int row = grid.RowCount;
        grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var key = new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoSize = false, Height = 22 };
        var val = new Label { Text = "—", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoSize = false, Height = 22, Font = new Font("Consolas", 10) };
        grid.Controls.Add(key, 0, row);
        grid.Controls.Add(val, 1, row);
        return val;
    }

    private static void AddControlRow(TableLayoutPanel grid, string label, Control control)
    {
        int row = grid.RowCount;
        grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var key = new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoSize = false, Height = 26 };
        control.Margin = new Padding(0, 3, 0, 3);
        grid.Controls.Add(key, 0, row);
        grid.Controls.Add(control, 1, row);
    }

    private void RefreshStatus()
    {
        var s = _worker.Status;
        _statusLabel.Text = s.Running ? "Running" : (s.ErrorMessage is null ? "Stopped" : "Stopped (error)");
        _statusLabel.ForeColor = s.ErrorMessage is not null ? Color.Firebrick
                              : s.Running ? Color.ForestGreen : Color.DimGray;
        _angleLabel.Text = $"{s.AngleDeg,7:F1}°";
        _steerLabel.Text = $"{s.Steer,+6:F2}" + (s.GyroSaturated ? "  ⚠ SAT" : "");
        _steerLabel.ForeColor = s.GyroSaturated ? Color.OrangeRed : Color.Black;
        _steerBar.Value = Math.Clamp(s.Steer, -1, 1);
        _stickYLabel.Text = $"{s.StickY,+5:F2}";
        _batteryLabel.Text = $"{s.BatteryPercent}%{(s.Charging ? " (charging)" : "")}";

        var p = _worker.Pedals.Status;
        // Throttle / brake values come from either the pedal worker (when enabled) or
        // are computed from the steering joycon's stick — we don't have a clean live
        // readout for the stick path yet, so show the pedal-worker values when active,
        // and show "via stick" placeholders otherwise.
        double throttle = 0, brake = 0;
        if (PedalsConfigHelper.RequiresPedalJoyCon(_config.ThrottleBrake))
        {
            throttle = p.Throttle;
            brake    = p.Brake;
        }
        else if (_config.ThrottleBrake == ThrottleBrakeMode.Stick)
        {
            // Stick Y: positive → throttle, negative → brake (after deadzone).
            double y = s.StickY;
            double dz = _config.StickDeadzone;
            if (y > dz)        throttle = Math.Clamp((y - dz) / (1 - dz), 0, 1);
            else if (y < -dz)  brake    = Math.Clamp((-y - dz) / (1 - dz), 0, 1);
        }
        _throttleLabel.Text = $"{throttle:F2}";
        _brakeLabel.Text    = $"{brake:F2}";
        _throttleBar.Value  = throttle;
        _brakeBar.Value     = brake;

        if (!p.Running) {
            _pedalStatusLabel.Text = "not in use";
            _pedalStatusLabel.ForeColor = Color.DimGray;
            _pedalValuesLabel.Text = "—";
        } else if (p.ErrorMessage is not null || !p.Connected) {
            _pedalStatusLabel.Text = "waiting…";
            _pedalStatusLabel.ForeColor = Color.DarkOrange;
            _pedalValuesLabel.Text = p.ErrorMessage ?? "disconnected";
        } else {
            _pedalStatusLabel.Text = $"Connected  bat {p.BatteryPercent}%";
            _pedalStatusLabel.ForeColor = Color.ForestGreen;
            _pedalValuesLabel.Text = $"tilt={p.TiltAngleDeg:+0.0;-0.0;0.0}°";
        }

        _errorLabel.Text = s.ErrorMessage ?? "";
    }

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
            Hide();
            Logger.Info("Window minimized to tray");
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ApplyDeadzoneVisuals();
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
            if (result != DialogResult.Yes) { e.Cancel = true; return; }
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
