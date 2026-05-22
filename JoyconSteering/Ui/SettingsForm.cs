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
    private readonly RadioButton _modeWheel = new() { Text = "Wheel — gyro integration", AutoSize = true };
    private readonly RadioButton _modeTilt = new() { Text = "Tilt — gravity-anchored (Mario Kart style)", AutoSize = true };
    private readonly ComboBox _axisAdvanced = new();
    private readonly NumericUpDown _range = NewNum(30, 720, 1);
    private readonly NumericUpDown _deadzone = NewNum(0, 20, 1);
    private readonly NumericUpDown _smoothing = NewNum(0, 200, 0);
    private readonly CheckBox _invert = new() { Text = "Invert steering direction" };
    private readonly ComboBox _tbMode = new() { Width = 250, DropDownWidth = 320 };
    private readonly ComboBox _stickAxis = new();
    private readonly NumericUpDown _stickDead = NewNum(0, 0.9m, 2);
    private readonly ComboBox _recenter = new();
    private readonly CheckBox _autoRecenterEnabled = new() { Text = "Auto-recenter when held still" };
    private readonly NumericUpDown _autoRecenter = NewNum(0.5m, 30, 1);
    private readonly NumericUpDown _beta = NewNum(0.01m, 0.5m, 3);

    // Pedal joy-con (the other one — opposite to the steering side)
    private readonly ComboBox _pedalThrottleBtn = new();
    private readonly ComboBox _pedalBrakeBtn    = new();
    private readonly RadioButton _pedalModeWheel = new() { Text = "Wheel — gyro integration", AutoSize = true };
    private readonly RadioButton _pedalModeTilt  = new() { Text = "Tilt — gravity-anchored", AutoSize = true };
    private readonly ComboBox _pedalAxisAdvanced = new();
    private readonly NumericUpDown _pedalTiltRange    = NewNum(5,  90, 1);
    private readonly NumericUpDown _pedalTiltDeadzone = NewNum(0,  30, 1);
    private readonly CheckBox _pedalTiltInvert        = new() { Text = "Invert pedal tilt direction" };
    private readonly ComboBox _pedalRecenter          = new();

    private static readonly string[] ButtonNames =
        { "up", "down", "left", "right", "l", "zl", "minus", "stick", "sl", "sr", "capture" };

    /// <summary>User-friendly throttle/brake mode labels. ToString = displayed text;
    /// IniValue is the canonical key written to App.ini.</summary>
    private sealed class ModeOption
    {
        public string Display { get; }
        public string IniValue { get; }
        public ModeOption(string display, string iniValue) { Display = display; IniValue = iniValue; }
        public override string ToString() => Display;
    }

    private static readonly ModeOption[] _modeOptions =
    {
        new("Steering Joy-Con's stick (Y)",       "stick"),
        new("Steering Joy-Con's L / ZL buttons",  "buttons"),
        new("Pedal Joy-Con's stick (Y)",          "pedal_stick"),
        new("Pedal Joy-Con's buttons",            "pedal_buttons"),
        new("Pedal Joy-Con's tilt (Mario Kart)",  "pedal_tilt"),
        new("Off — bind in-game to keyboard etc.","none"),
    };
    private static readonly string[] LeftButtonNames =
        { "up", "down", "left", "right", "l", "zl", "minus", "stick", "sl", "sr", "capture" };
    private static readonly string[] RightButtonNames =
        { "y", "x", "b", "a", "r", "zr", "plus", "stick", "sl", "sr", "home" };
    private readonly Dictionary<string, NumericUpDown> _buttonInputs = new();

    public SettingsForm(string iniPath, AppConfig config)
    {
        _iniPath = iniPath;
        Text = "JoyconSteering — Settings";
        Width = 600;
        Height = 760;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(560, 700);
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9);

        // Populate combo box options
        _side.Items.AddRange(new object[] { "left", "right" });
        _axisAdvanced.Items.AddRange(new object[] { "(use mode above)", "roll", "pitch", "yaw" });
        _tbMode.Items.AddRange(_modeOptions.Cast<object>().ToArray());
        _stickAxis.Items.AddRange(new object[] { "y (up = throttle)", "x (right = throttle)" });
        // _recenter is populated by RepopulateSteeringRecenterDropdown based on _side
        _pedalAxisAdvanced.Items.AddRange(new object[] { "(use mode above)", "roll", "pitch", "yaw" });
        foreach (var combo in new[] { _side, _axisAdvanced, _tbMode, _stickAxis, _recenter, _pedalThrottleBtn, _pedalBrakeBtn, _pedalAxisAdvanced, _pedalRecenter })
            combo.DropDownStyle = ComboBoxStyle.DropDownList;

        // Both the steering recenter dropdown (steering side's buttons) and the pedal
        // button dropdowns (OPPOSITE side's buttons) are side-aware.
        _side.SelectedIndexChanged += (s, e) =>
        {
            RepopulateSteeringRecenterDropdown();
            RepopulatePedalButtonDropdowns();
        };
        _modeWheel.CheckedChanged += (s, e) => { if (_modeWheel.Checked) _axisAdvanced.SelectedIndex = 0; };
        _modeTilt.CheckedChanged += (s, e) => { if (_modeTilt.Checked) _axisAdvanced.SelectedIndex = 0; };
        _axisAdvanced.SelectedIndexChanged += (s, e) =>
        {
            if (_axisAdvanced.SelectedIndex > 0)
            {
                // Switching to an advanced axis clears the mode radios.
                _modeWheel.Checked = false;
                _modeTilt.Checked = false;
            }
        };

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
        var reset = new Button { Text = "Reset to defaults", Width = 130 };
        save.Click  += (s, e) => OnSave();
        cancel.Click+= (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
        reset.Click += (s, e) => OnResetToDefaults();
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        // Push Reset to the far left
        var spacer = new Label { Width = 80, Height = 1 };
        buttons.Controls.Add(spacer);
        buttons.Controls.Add(reset);
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

        // Mode radio group — primary steering-source selector.
        var modePanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
        };
        _modeWheel.Margin = new Padding(0, 2, 0, 2);
        _modeTilt.Margin = new Padding(0, 2, 0, 2);
        modePanel.Controls.Add(_modeWheel);
        modePanel.Controls.Add(_modeTilt);
        AddPair(grid, "Steering mode:", modePanel);
        AddPair(grid, "  Advanced axis:", _axisAdvanced);

        AddPair(grid, "Full-lock tilt (deg/side):", _range);
        AddPair(grid, "Deadzone (degrees):", _deadzone);
        AddPair(grid, "Smoothing (ms):", _smoothing);
        AddPair(grid, "", _invert);
        AddPair(grid, "Recenter button:", _recenter);
        AddPair(grid, "", _autoRecenterEnabled);
        AddPair(grid, "  …after idle for (sec):", _autoRecenter);
        _autoRecenterEnabled.CheckedChanged += (s, e) => _autoRecenter.Enabled = _autoRecenterEnabled.Checked;
        var hint = new Label
        {
            Text = "Auto-recenter snaps the wheel centre to its current orientation whenever\n" +
                   "the controller has been still for the given duration. Helps mask the small\n" +
                   "drift that accumulates during fast motion (BT can drop a few samples).\n" +
                   "Disable this if you sometimes drive holding a deliberate steady offset.",
            Dock = DockStyle.Fill,
            AutoSize = false,
            ForeColor = Color.Gray,
            Padding = new Padding(0, 8, 0, 0),
        };
        grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.Controls.Add(hint, 0, grid.RowCount - 1);
        grid.SetColumnSpan(hint, 2);
        tab.Controls.Add(grid);
        return tab;
    }

    private TabPage BuildPedalsTab()
    {
        var tab = new TabPage("Pedals");
        var grid = NewFormGrid();
        AddPair(grid, "Throttle/Brake mode:", _tbMode);
        AddPair(grid, "Stick axis:", _stickAxis);
        AddPair(grid, "Stick deadzone (0-1):", _stickDead);
        _tbMode.SelectedIndexChanged += (s, e) => RefreshPedalFieldVisibility();

        // Section separator + label for pedal-joy-con-specific settings
        var sep = new Label
        {
            Text = "── Pedal Joy-Con (the other one — opposite to the steering side) ──",
            Dock = DockStyle.Fill, AutoSize = false, Height = 24,
            ForeColor = Color.DimGray, Font = new Font(Font, FontStyle.Italic),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 6, 0, 0),
        };
        grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.Controls.Add(sep, 0, grid.RowCount - 1);
        grid.SetColumnSpan(sep, 2);

        AddPair(grid, "  Throttle button:",     _pedalThrottleBtn);
        AddPair(grid, "  Brake button:",        _pedalBrakeBtn);

        // Pedal mode radio group — mirrors steering's Wheel/Tilt selector
        var pedalModePanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
        };
        _pedalModeWheel.Margin = new Padding(0, 2, 0, 2);
        _pedalModeTilt.Margin  = new Padding(0, 2, 0, 2);
        pedalModePanel.Controls.Add(_pedalModeWheel);
        pedalModePanel.Controls.Add(_pedalModeTilt);
        AddPair(grid, "  Pedal tilt mode:", pedalModePanel);
        AddPair(grid, "    Advanced axis:", _pedalAxisAdvanced);

        AddPair(grid, "  Tilt full range (°):", _pedalTiltRange);
        AddPair(grid, "  Tilt deadzone (°):",   _pedalTiltDeadzone);
        AddPair(grid, "", _pedalTiltInvert);
        AddPair(grid, "  Tilt recenter button:", _pedalRecenter);

        _pedalModeWheel.CheckedChanged += (s, e) => { if (_pedalModeWheel.Checked) _pedalAxisAdvanced.SelectedIndex = 0; };
        _pedalModeTilt.CheckedChanged  += (s, e) => { if (_pedalModeTilt.Checked)  _pedalAxisAdvanced.SelectedIndex = 0; };
        _pedalAxisAdvanced.SelectedIndexChanged += (s, e) =>
        {
            if (_pedalAxisAdvanced.SelectedIndex > 0)
            {
                _pedalModeWheel.Checked = false;
                _pedalModeTilt.Checked = false;
            }
        };

        var help = new Label
        {
            Text = "Modes ending in \"pedal\" (Pedal Joy-Con's stick / buttons / tilt) use the\n" +
                   "OTHER Joy-Con — the side opposite the one you picked for steering. The\n" +
                   "second Joy-Con must be paired via Windows Bluetooth, but the app still\n" +
                   "works without it: those modes just yield zero throttle/brake until it's\n" +
                   "connected. Stick deadzone and axis only apply to the two stick modes;\n" +
                   "button pickers only to \"Pedal Joy-Con's buttons\"; tilt fields only to\n" +
                   "\"Pedal Joy-Con's tilt\". Irrelevant fields are greyed for the current mode.",
            Dock = DockStyle.Fill, AutoSize = false,
            ForeColor = Color.Gray, Padding = new Padding(0, 16, 0, 0),
        };
        var holder = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        holder.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        holder.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        holder.Controls.Add(grid, 0, 0);
        holder.Controls.Add(help, 0, 1);
        tab.Controls.Add(holder);
        return tab;
    }

    private void RepopulateSteeringRecenterDropdown()
    {
        var steeringIsLeft = (_side.SelectedItem?.ToString() ?? "left") == "left";
        var buttons = steeringIsLeft ? LeftButtonNames : RightButtonNames;
        var previous = _recenter.SelectedItem?.ToString();
        _recenter.Items.Clear();
        _recenter.Items.AddRange(buttons.Cast<object>().Append("none").ToArray());
        if (previous is not null && _recenter.Items.Contains(previous))
            _recenter.SelectedItem = previous;
        else if (_recenter.Items.Count > 0)
            _recenter.SelectedIndex = 0;
    }

    private void RepopulatePedalButtonDropdowns()
    {
        // Pedal joy-con = the OPPOSITE side of whatever's selected for steering.
        var steeringIsLeft = (_side.SelectedItem?.ToString() ?? "left") == "left";
        var pedalButtons = steeringIsLeft ? RightButtonNames : LeftButtonNames;

        foreach (var combo in new[] { _pedalThrottleBtn, _pedalBrakeBtn, _pedalRecenter })
        {
            var previous = combo.SelectedItem?.ToString();
            combo.Items.Clear();
            combo.Items.AddRange(pedalButtons.Cast<object>().ToArray());
            if (previous is not null && combo.Items.Contains(previous))
                combo.SelectedItem = previous;
            else if (combo.Items.Count > 0)
                combo.SelectedIndex = 0;
        }
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
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
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

    private void OnResetToDefaults()
    {
        var result = MessageBox.Show(this,
            "Reset every setting on every tab to defaults?\n\n" +
            "Nothing is written to disk yet — you'll still need to click Save & Apply for the reset to persist.",
            "Reset to defaults",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (result != DialogResult.Yes) return;

        // Brand-new defaults are produced by loading an empty INI through AppConfig.Load.
        // This guarantees the UI matches what a fresh install would get.
        var tmp = Path.Combine(Path.GetTempPath(), $"joycon-defaults-{Guid.NewGuid():N}.ini");
        try
        {
            File.WriteAllText(tmp, "");
            var defaults = AppConfig.Load(tmp);
            LoadFrom(defaults);
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }

    private void LoadFrom(AppConfig cfg)
    {
        _side.SelectedItem = cfg.Side.ToString().ToLowerInvariant();
        RepopulateSteeringRecenterDropdown();
        RepopulatePedalButtonDropdowns();
        _vjoyId.Value = cfg.VJoyDeviceId;

        switch (cfg.Axis)
        {
            case SteeringAxis.Tilt:
                _modeTilt.Checked = true;
                _axisAdvanced.SelectedIndex = 0;
                break;
            case SteeringAxis.Roll:
                _modeWheel.Checked = false;
                _modeTilt.Checked = false;
                _axisAdvanced.SelectedItem = "roll";
                break;
            case SteeringAxis.Pitch:
                _modeWheel.Checked = false;
                _modeTilt.Checked = false;
                _axisAdvanced.SelectedItem = "pitch";
                break;
            case SteeringAxis.Yaw:
                _modeWheel.Checked = false;
                _modeTilt.Checked = false;
                _axisAdvanced.SelectedItem = "yaw";
                break;
            case SteeringAxis.Wheel:
                _modeWheel.Checked = true;
                _axisAdvanced.SelectedIndex = 0;
                break;
            default: // Auto → Tilt (the new default)
                _modeTilt.Checked = true;
                _axisAdvanced.SelectedIndex = 0;
                break;
        }
        _range.Value = (decimal)cfg.RangeDegrees;
        _deadzone.Value = (decimal)cfg.DeadzoneDegrees;
        _smoothing.Value = (decimal)cfg.SmoothingMs;
        _invert.Checked = cfg.Invert;
        var iniValue = ModeToIniString(cfg.ThrottleBrake);
        _tbMode.SelectedItem = _modeOptions.FirstOrDefault(m => m.IniValue == iniValue) ?? _modeOptions[0];
        _stickAxis.SelectedIndex = cfg.StickAxis == StickAxis.X ? 1 : 0;
        _stickDead.Value = (decimal)cfg.StickDeadzone;
        RefreshPedalFieldVisibility();
        _recenter.SelectedItem = cfg.RecenterButton;
        _autoRecenterEnabled.Checked = cfg.AutoRecenterIdleSeconds > 0;
        _autoRecenter.Enabled = _autoRecenterEnabled.Checked;
        _autoRecenter.Value = cfg.AutoRecenterIdleSeconds > 0 ? (decimal)cfg.AutoRecenterIdleSeconds : 1.5m;

        SetComboIfPresent(_pedalThrottleBtn, cfg.PedalThrottleButton);
        SetComboIfPresent(_pedalBrakeBtn,    cfg.PedalBrakeButton);
        SetComboIfPresent(_pedalRecenter,    cfg.PedalRecenterButton);

        // Pedal axis: same mode-resolution as steering — auto and tilt → Tilt radio,
        // wheel → Wheel radio, roll/pitch/yaw → advanced.
        switch (cfg.PedalTiltAxis)
        {
            case SteeringAxis.Wheel:
                _pedalModeWheel.Checked = true; _pedalAxisAdvanced.SelectedIndex = 0; break;
            case SteeringAxis.Roll:
                _pedalModeWheel.Checked = false; _pedalModeTilt.Checked = false;
                _pedalAxisAdvanced.SelectedItem = "roll"; break;
            case SteeringAxis.Pitch:
                _pedalModeWheel.Checked = false; _pedalModeTilt.Checked = false;
                _pedalAxisAdvanced.SelectedItem = "pitch"; break;
            case SteeringAxis.Yaw:
                _pedalModeWheel.Checked = false; _pedalModeTilt.Checked = false;
                _pedalAxisAdvanced.SelectedItem = "yaw"; break;
            default: // Auto and Tilt → Tilt radio
                _pedalModeTilt.Checked = true; _pedalAxisAdvanced.SelectedIndex = 0; break;
        }
        _pedalTiltRange.Value    = (decimal)Math.Clamp(cfg.PedalTiltRangeDegrees, 5, 90);
        _pedalTiltDeadzone.Value = (decimal)Math.Clamp(cfg.PedalTiltDeadzoneDegrees, 0, 30);
        _pedalTiltInvert.Checked = cfg.PedalTiltInvert;

        _beta.Value = (decimal)cfg.MadgwickBeta;
        foreach (var name in ButtonNames)
            _buttonInputs[name].Value = cfg.ButtonMap.TryGetValue(name, out var v) ? v : 0;
    }

    private static void SetComboIfPresent(ComboBox combo, string value)
    {
        if (combo.Items.Contains(value)) combo.SelectedItem = value;
        else if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    // ThrottleBrakeMode → INI string. Direct enum.ToString() loses the underscore in
    // PedalButtons / PedalTilt, so the dropdown couldn't match its items on round-trip.
    private static string ModeToIniString(ThrottleBrakeMode mode) => mode switch
    {
        ThrottleBrakeMode.Stick => "stick",
        ThrottleBrakeMode.Buttons => "buttons",
        ThrottleBrakeMode.PedalStick => "pedal_stick",
        ThrottleBrakeMode.PedalButtons => "pedal_buttons",
        ThrottleBrakeMode.PedalTilt => "pedal_tilt",
        ThrottleBrakeMode.None => "none",
        _ => "pedal_tilt",
    };

    private string ResolveSelectedAxis()
    {
        // Advanced override wins if set.
        if (_axisAdvanced.SelectedIndex > 0 && _axisAdvanced.SelectedItem is string adv)
            return adv;
        if (_modeTilt.Checked) return "tilt";
        if (_modeWheel.Checked) return "wheel";
        return "auto";
    }

    private void RefreshPedalFieldVisibility()
    {
        // Enable / disable irrelevant fields based on the chosen throttle/brake mode so
        // the Pedals tab isn't a wall of greyable options when only a few apply.
        var mode = (_tbMode.SelectedItem as ModeOption)?.IniValue ?? "pedal_stick";
        bool usesStick     = mode is "stick" or "pedal_stick";
        bool usesButtons   = mode == "pedal_buttons";
        bool usesTilt      = mode == "pedal_tilt";

        _stickAxis.Enabled         = usesStick;
        _stickDead.Enabled         = usesStick;
        _pedalThrottleBtn.Enabled  = usesButtons;
        _pedalBrakeBtn.Enabled     = usesButtons;
        _pedalModeWheel.Enabled    = usesTilt;
        _pedalModeTilt.Enabled     = usesTilt;
        _pedalAxisAdvanced.Enabled = usesTilt;
        _pedalTiltRange.Enabled    = usesTilt;
        _pedalTiltDeadzone.Enabled = usesTilt;
        _pedalTiltInvert.Enabled   = usesTilt;
        _pedalRecenter.Enabled     = usesTilt;
    }

    private string ResolvePedalAxis()
    {
        if (_pedalAxisAdvanced.SelectedIndex > 0 && _pedalAxisAdvanced.SelectedItem is string adv)
            return adv;
        if (_pedalModeTilt.Checked) return "tilt";
        if (_pedalModeWheel.Checked) return "wheel";
        return "auto";
    }

    private void OnSave()
    {
        try
        {
            var updates = new Dictionary<(string, string), string>
            {
                [("device", "joycon_side")] = _side.SelectedItem?.ToString() ?? "left",
                [("device", "vjoy_device_id")] = ((int)_vjoyId.Value).ToString(CultureInfo.InvariantCulture),
                [("steering", "axis")] = ResolveSelectedAxis(),
                [("steering", "range_degrees")] = _range.Value.ToString(CultureInfo.InvariantCulture),
                [("steering", "deadzone_degrees")] = _deadzone.Value.ToString(CultureInfo.InvariantCulture),
                [("steering", "smoothing_ms")] = _smoothing.Value.ToString(CultureInfo.InvariantCulture),
                [("steering", "invert")] = _invert.Checked ? "true" : "false",
                [("throttle_brake", "mode")] = (_tbMode.SelectedItem as ModeOption)?.IniValue ?? "pedal_stick",
                [("throttle_brake", "stick_axis")] = _stickAxis.SelectedIndex == 1 ? "x" : "y",
                [("throttle_brake", "stick_deadzone")] = _stickDead.Value.ToString(CultureInfo.InvariantCulture),
                [("recenter", "button")] = _recenter.SelectedItem?.ToString() ?? "stick",
                [("recenter", "auto_recenter_idle_seconds")] =
                    (_autoRecenterEnabled.Checked ? _autoRecenter.Value : 0m).ToString(CultureInfo.InvariantCulture),
                [("fusion", "madgwick_beta")] = _beta.Value.ToString(CultureInfo.InvariantCulture),
            };
            foreach (var name in ButtonNames)
                updates[("buttons", name)] = ((int)_buttonInputs[name].Value).ToString(CultureInfo.InvariantCulture);

            updates[("pedal_buttons", "throttle")] = _pedalThrottleBtn.SelectedItem?.ToString() ?? "zr";
            updates[("pedal_buttons", "brake")]    = _pedalBrakeBtn.SelectedItem?.ToString() ?? "r";
            updates[("pedal_tilt", "axis")]             = ResolvePedalAxis();
            updates[("pedal_tilt", "range_degrees")]    = _pedalTiltRange.Value.ToString(CultureInfo.InvariantCulture);
            updates[("pedal_tilt", "deadzone_degrees")] = _pedalTiltDeadzone.Value.ToString(CultureInfo.InvariantCulture);
            updates[("pedal_tilt", "invert")]           = _pedalTiltInvert.Checked ? "true" : "false";
            updates[("pedal_tilt", "recenter_button")]  = _pedalRecenter.SelectedItem?.ToString() ?? "home";

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
