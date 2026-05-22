using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using JoyconSteering.Config;

namespace JoyconSteering.Ui;

/// <summary>
/// Edits all AppConfig fields. On Save & Apply, writes through IniWriter
/// (comment-preserving) and raises <see cref="Saved"/> so the host can hot-reload.
/// </summary>
internal sealed class SettingsForm : Form
{
    private readonly string _iniPath;
    public event Action? Saved;

    private readonly ComboBox _side = new();
    private readonly NumericUpDown _vjoyId = NewNum(1, 16, 0);
    private readonly ComboBox _axis = new();
    private readonly NumericUpDown _range = NewNum(30, 720, 1);
    private readonly NumericUpDown _deadzone = NewNum(0, 20, 1);
    private readonly NumericUpDown _smoothing = NewNum(0, 200, 0);
    private readonly CheckBox _invert = new() { Text = "Invert steering direction" };
    private readonly ComboBox _tbMode = new();
    private readonly NumericUpDown _stickDead = NewNum(0, 0.9m, 2);
    private readonly ComboBox _recenter = new();
    private readonly NumericUpDown _autoRecenter = NewNum(0, 30, 1);
    private readonly NumericUpDown _beta = NewNum(0.01m, 0.5m, 3);

    private static readonly string[] ButtonNames =
        { "up", "down", "left", "right", "l", "zl", "minus", "stick", "sl", "sr", "capture" };
    private readonly Dictionary<string, NumericUpDown> _buttonInputs = new();

    public SettingsForm(string iniPath, AppConfig config)
    {
        _iniPath = iniPath;
        Text = "JoyconSteering — Settings";
        Width = 560;
        Height = 560;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9);

        // Populate combo box options
        _side.Items.AddRange(new object[] { "left", "right" });
        _axis.Items.AddRange(new object[] { "auto", "wheel", "roll", "pitch", "yaw" });
        _tbMode.Items.AddRange(new object[] { "stick", "buttons", "none" });
        _recenter.Items.AddRange(ButtonNames.Cast<object>().Append("none").ToArray());
        foreach (var combo in new[] { _side, _axis, _tbMode, _recenter })
            combo.DropDownStyle = ComboBoxStyle.DropDownList;

        // Per-button inputs
        foreach (var name in ButtonNames)
            _buttonInputs[name] = NewNum(0, 128, 0);

        BuildLayout();
        LoadFrom(config);
    }

    private static NumericUpDown NewNum(decimal min, decimal max, int decimals)
        => new() { Minimum = min, Maximum = max, DecimalPlaces = decimals, Increment = decimals > 0 ? 0.01m : 1m, Width = 90 };

    private void BuildLayout()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };

        tabs.TabPages.Add(BuildSteeringTab());
        tabs.TabPages.Add(BuildPedalsTab());
        tabs.TabPages.Add(BuildButtonsTab());
        tabs.TabPages.Add(BuildAdvancedTab());

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 44,
            Padding = new Padding(8),
        };
        var save = new Button { Text = "Save && Apply", Width = 110, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", Width = 90, DialogResult = DialogResult.Cancel };
        save.Click += (s, e) => OnSave();
        cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        AcceptButton = save;
        CancelButton = cancel;

        Controls.Add(tabs);
        Controls.Add(buttons);
    }

    private TabPage BuildSteeringTab()
    {
        var tab = new TabPage("Steering");
        var grid = NewFormGrid();
        AddPair(grid, "Joy-Con side:", _side);
        AddPair(grid, "vJoy device:", _vjoyId);
        AddPair(grid, "Steering axis:", _axis);
        AddPair(grid, "Full-lock tilt (deg/side):", _range);
        AddPair(grid, "Deadzone (degrees):", _deadzone);
        AddPair(grid, "Smoothing (ms):", _smoothing);
        AddPair(grid, "", _invert);
        AddPair(grid, "Recenter button:", _recenter);
        AddPair(grid, "Auto-recenter idle (sec):", _autoRecenter);
        tab.Controls.Add(grid);
        return tab;
    }

    private TabPage BuildPedalsTab()
    {
        var tab = new TabPage("Pedals");
        var grid = NewFormGrid();
        AddPair(grid, "Throttle/Brake mode:", _tbMode);
        AddPair(grid, "Stick deadzone (0-1):", _stickDead);
        var help = new Label
        {
            Text = "stick   — left analog stick Y (up = throttle, down = brake)\n" +
                   "buttons — L = throttle, ZL = brake (digital)\n" +
                   "none    — disable; bind throttle/brake to keyboard/other device",
            Dock = DockStyle.Fill,
            AutoSize = false,
            ForeColor = Color.Gray,
            Padding = new Padding(0, 16, 0, 0),
        };
        var holder = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        holder.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        holder.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        holder.Controls.Add(grid, 0, 0);
        holder.Controls.Add(help, 0, 1);
        tab.Controls.Add(holder);
        return tab;
    }

    private TabPage BuildButtonsTab()
    {
        var tab = new TabPage("Buttons");
        var grid = NewFormGrid();
        var header = new Label { Text = "Map each Joy-Con button to a vJoy button number (1-128, 0 = disabled).",
                                  ForeColor = Color.Gray, Dock = DockStyle.Fill, AutoSize = false, Height = 36 };
        grid.Controls.Add(header, 0, 0);
        grid.SetColumnSpan(header, 2);

        int row = 1;
        foreach (var name in ButtonNames)
        {
            AddPairAtRow(grid, row++, $"{name}:", _buttonInputs[name]);
        }
        tab.Controls.Add(grid);
        return tab;
    }

    private TabPage BuildAdvancedTab()
    {
        var tab = new TabPage("Advanced");
        var grid = NewFormGrid();
        AddPair(grid, "Madgwick beta:", _beta);
        var help = new Label
        {
            Text = "Higher beta = trusts accelerometer more (faster drift correction, less smooth).\n" +
                   "Range 0.04 - 0.10 is useful. Increase if you see drift between races.",
            Dock = DockStyle.Fill,
            AutoSize = false,
            ForeColor = Color.Gray,
            Padding = new Padding(0, 12, 0, 0),
        };
        var holder = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        holder.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        holder.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        holder.Controls.Add(grid, 0, 0);
        holder.Controls.Add(help, 0, 1);
        tab.Controls.Add(holder);
        return tab;
    }

    private static TableLayoutPanel NewFormGrid()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12),
            AutoSize = false,
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return grid;
    }

    private static void AddPair(TableLayoutPanel grid, string label, Control control)
    {
        int row = grid.RowCount;
        grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        AddPairAtRow(grid, row, label, control);
    }

    private static void AddPairAtRow(TableLayoutPanel grid, int row, string label, Control control)
    {
        var lbl = new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoSize = false, Height = 28 };
        control.Margin = new Padding(0, 4, 0, 4);
        grid.Controls.Add(lbl, 0, row);
        grid.Controls.Add(control, 1, row);
    }

    private void LoadFrom(AppConfig cfg)
    {
        _side.SelectedItem = cfg.Side.ToString().ToLowerInvariant();
        _vjoyId.Value = cfg.VJoyDeviceId;
        _axis.SelectedItem = cfg.Axis.ToString().ToLowerInvariant();
        _range.Value = (decimal)cfg.RangeDegrees;
        _deadzone.Value = (decimal)cfg.DeadzoneDegrees;
        _smoothing.Value = (decimal)cfg.SmoothingMs;
        _invert.Checked = cfg.Invert;
        _tbMode.SelectedItem = cfg.ThrottleBrake.ToString().ToLowerInvariant();
        _stickDead.Value = (decimal)cfg.StickDeadzone;
        _recenter.SelectedItem = cfg.RecenterButton;
        _autoRecenter.Value = (decimal)cfg.AutoRecenterIdleSeconds;
        _beta.Value = (decimal)cfg.MadgwickBeta;
        foreach (var name in ButtonNames)
            _buttonInputs[name].Value = cfg.ButtonMap.TryGetValue(name, out var v) ? v : 0;
    }

    private void OnSave()
    {
        try
        {
            var updates = new Dictionary<(string, string), string>
            {
                [("device", "joycon_side")] = _side.SelectedItem?.ToString() ?? "left",
                [("device", "vjoy_device_id")] = ((int)_vjoyId.Value).ToString(CultureInfo.InvariantCulture),
                [("steering", "axis")] = _axis.SelectedItem?.ToString() ?? "auto",
                [("steering", "range_degrees")] = _range.Value.ToString(CultureInfo.InvariantCulture),
                [("steering", "deadzone_degrees")] = _deadzone.Value.ToString(CultureInfo.InvariantCulture),
                [("steering", "smoothing_ms")] = _smoothing.Value.ToString(CultureInfo.InvariantCulture),
                [("steering", "invert")] = _invert.Checked ? "true" : "false",
                [("throttle_brake", "mode")] = _tbMode.SelectedItem?.ToString() ?? "stick",
                [("throttle_brake", "stick_deadzone")] = _stickDead.Value.ToString(CultureInfo.InvariantCulture),
                [("recenter", "button")] = _recenter.SelectedItem?.ToString() ?? "stick",
                [("recenter", "auto_recenter_idle_seconds")] = _autoRecenter.Value.ToString(CultureInfo.InvariantCulture),
                [("fusion", "madgwick_beta")] = _beta.Value.ToString(CultureInfo.InvariantCulture),
            };
            foreach (var name in ButtonNames)
                updates[("buttons", name)] = ((int)_buttonInputs[name].Value).ToString(CultureInfo.InvariantCulture);

            IniWriter.Update(_iniPath, updates);
            Saved?.Invoke();
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to save settings:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
