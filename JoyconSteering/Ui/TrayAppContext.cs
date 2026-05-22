using System.Drawing;
using System.Windows.Forms;
using JoyconSteering.Config;

namespace JoyconSteering.Ui;

/// <summary>
/// Root of the WinForms application. Owns the NotifyIcon, the SteeringWorker,
/// and the diagnostics/settings windows.
///
/// On launch the main (diagnostics) window is shown. Minimize hides it to the tray;
/// closing the X button prompts to confirm quit. Tray double-click restores the window.
/// </summary>
internal sealed class TrayAppContext : ApplicationContext
{
    private readonly string _iniPath;
    private readonly NotifyIcon _icon;
    private readonly ContextMenuStrip _menu;
    private readonly System.Windows.Forms.Timer _tooltipTimer;
    private readonly SteeringWorker _worker = new();
    private readonly DiagnosticsForm _mainForm;
    private SettingsForm? _settingsForm;
    private AppConfig _config;

    public TrayAppContext(string iniPath, AppConfig initialConfig)
    {
        _iniPath = iniPath;
        _config = initialConfig;

        _mainForm = CreateMainForm();

        _menu = BuildMenu();
        _icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "JoyconSteering — starting…",
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _icon.DoubleClick += (s, e) => _mainForm.ShowFromTray();

        _tooltipTimer = new System.Windows.Forms.Timer { Interval = 500 };
        _tooltipTimer.Tick += (s, e) => UpdateTooltip();
        _tooltipTimer.Start();

        _mainForm.Show();

        _worker.Start(_config);
        Logger.Info("Tray app started; main window visible");
    }

    private DiagnosticsForm CreateMainForm()
    {
        var form = new DiagnosticsForm(_worker, _config);
        form.SettingsRequested += ShowSettings;
        form.QuitRequested += Quit;
        return form;
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show window", null, (s, e) => _mainForm.ShowFromTray());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Recenter", null, (s, e) => { _worker.Recenter(); Logger.Info("Recenter via tray"); });
        menu.Items.Add("Recalibrate gyro", null, (s, e) => { _worker.RecalibrateGyro(); Logger.Info("Recalibrate via tray"); });
        menu.Items.Add("Settings…", null, (s, e) => ShowSettings());
        menu.Items.Add("Reload config", null, (s, e) => ReloadAndRestart());
        menu.Items.Add("Open log folder", null, (s, e) => OpenLogFolder());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (s, e) => ConfirmAndQuit());
        return menu;
    }

    private void ShowSettings()
    {
        if (_settingsForm is { IsDisposed: false }) { _settingsForm.Activate(); return; }
        _settingsForm = new SettingsForm(_iniPath, _config);
        _settingsForm.Saved += ReloadAndRestart;
        _settingsForm.Show(_mainForm);
    }

    private void ReloadAndRestart()
    {
        try
        {
            _config = AppConfig.Load(_iniPath);
            Logger.Info($"Reloaded config: side={_config.Side} vjoy={_config.VJoyDeviceId} range={_config.RangeDegrees}");
            _worker.Stop();
            _worker.Start(_config);
            _mainForm.UpdateConfig(_config);
        }
        catch (Exception ex)
        {
            Logger.Error("Reload failed", ex);
            MessageBox.Show($"Failed to reload config:\n{ex.Message}", "JoyconSteering",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenLogFolder()
    {
        try
        {
            System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + Logger.LogFilePath + "\"");
        }
        catch (Exception ex)
        {
            Logger.Error("Open log folder failed", ex);
        }
    }

    private void ConfirmAndQuit()
    {
        // Reuse the window's close-confirm dialog by closing the window.
        // If hidden, prompt directly.
        if (_mainForm.Visible)
        {
            _mainForm.Close();
        }
        else
        {
            var result = MessageBox.Show("Quit JoyconSteering?",
                "Confirm Quit", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            if (result == DialogResult.Yes) Quit();
        }
    }

    private void UpdateTooltip()
    {
        var s = _worker.Status;
        string text;
        if (s.ErrorMessage is not null)
            text = "JoyconSteering: ERROR — " + s.ErrorMessage;
        else if (!s.Running)
            text = "JoyconSteering — stopped";
        else
            text = $"JoyconSteering  angle {s.AngleDeg:F0}°  steer {s.Steer:+0.00;-0.00;0.00}  bat {s.BatteryPercent}%";

        // NotifyIcon tooltip is limited to 127 chars.
        _icon.Text = Truncate(text, 63);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

    private void Quit()
    {
        Logger.Info("Quit invoked");
        _tooltipTimer.Stop();
        _icon.Visible = false;
        _worker.Dispose();
        _icon.Dispose();
        _menu.Dispose();
        ExitThread();
    }

    private static Icon LoadIcon()
    {
        var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(Color.FromArgb(35, 132, 213));
            g.FillEllipse(brush, 0, 0, 31, 31);
            using var font = new Font("Segoe UI", 12, FontStyle.Bold);
            using var text = new SolidBrush(Color.White);
            var sz = g.MeasureString("JS", font);
            g.DrawString("JS", font, text, (32 - sz.Width) / 2, (32 - sz.Height) / 2);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }
}
