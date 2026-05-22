using JoyconSteering.Config;
using JoyconSteering.JoyCon;
using JoyconSteering.JoyCon.Fusion;
using JoyconSteering.Steering;

namespace JoyconSteering;

public sealed record PedalWorkerStatus(
    bool Running,
    bool Connected,
    double Throttle,
    double Brake,
    double TiltAngleDeg,
    int BatteryPercent,
    string? ErrorMessage)
{
    public static readonly PedalWorkerStatus Stopped = new(false, false, 0, 0, 0, 0, null);
}

/// <summary>
/// Optional pedal worker — opens the OTHER Joy-Con (opposite side to the steering
/// controller) and computes throttle/brake from either its assignable buttons or a
/// gravity-anchored tilt. Runs on its own thread so its read cadence doesn't stall
/// the steering loop. Safe to start when no second Joy-Con is paired — it will sit
/// in the reconnect loop until one appears, and surface "disconnected" in status.
/// </summary>
public sealed class PedalJoyConWorker : IDisposable
{
    private const double SampleDtSeconds = 0.005;
    private const double BiasRefinementThresholdDps = 1.5;

    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private Task? _task;
    private PedalWorkerStatus _status = PedalWorkerStatus.Stopped;
    private bool _recenterRequested;

    public PedalWorkerStatus Status { get { lock (_gate) return _status; } }

    public (double Throttle, double Brake) CurrentPedals
    {
        get { lock (_gate) return (_status.Throttle, _status.Brake); }
    }

    public void Recenter() { lock (_gate) _recenterRequested = true; }

    public void Start(AppConfig config)
    {
        Stop();
        lock (_gate)
        {
            _cts = new CancellationTokenSource();
            _status = _status with { Running = true };
            var ct = _cts.Token;
            _task = Task.Run(() => Run(config, ct), ct);
        }
    }

    public void Stop()
    {
        CancellationTokenSource? cts; Task? task;
        lock (_gate) { cts = _cts; task = _task; _cts = null; _task = null; }
        if (cts is null) return;
        try { cts.Cancel(); } catch { }
        try { task?.Wait(2000); } catch { }
        cts.Dispose();
        lock (_gate) _status = PedalWorkerStatus.Stopped;
    }

    public void Dispose() => Stop();

    private void Run(AppConfig config, CancellationToken token)
    {
        var pedalSide = PedalsConfigHelper.PedalSideFor(config.Side);
        Logger.Info($"Pedal worker starting on {pedalSide} Joy-Con, mode={config.ThrottleBrake}");

        while (!token.IsCancellationRequested)
        {
            JoyConDevice? dev = null;
            try
            {
                dev = pedalSide == JoyConSide.Left ? JoyConDevice.OpenLeft() : JoyConDevice.OpenRight();
                dev.Initialize();
                Logger.Info($"Pedal Joy-Con ({pedalSide}) initialized");
                lock (_gate) _status = _status with { Connected = true, ErrorMessage = null };

                Loop(config, pedalSide, dev, token);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) when (ex is IOException or TimeoutException or InvalidOperationException)
            {
                Logger.Warn($"Pedal Joy-Con link lost: {ex.Message}. Retrying in 3 s…");
                lock (_gate)
                {
                    _status = _status with
                    {
                        Connected = false,
                        Throttle = 0,
                        Brake = 0,
                        ErrorMessage = "Pedal Joy-Con disconnected — waiting. Pair / wake the second Joy-Con."
                    };
                }
            }
            finally { dev?.Dispose(); }

            if (!SleepCancellable(3000, token)) return;
        }
    }

    private void Loop(AppConfig config, JoyConSide pedalSide, JoyConDevice dev, CancellationToken token)
    {
        var biasCal = new GyroBiasCalibrator();
        var fusion = new MadgwickFilter(config.MadgwickBeta);
        var wheel = new WheelAxisIntegrator();
        var tilt = new GravityTiltAxis();
        var pedalAxis = SteeringAxisSelector.Resolve(config.PedalTiltAxis, pedalSide);
        var pedalSettings = new TiltPedalSettings(
            RangeDegrees: config.PedalTiltRangeDegrees,
            DeadzoneDegrees: config.PedalTiltDeadzoneDegrees,
            Invert: config.PedalTiltInvert);
        var recenterBtn = config.PedalRecenterButton;
        bool prevRecenterPressed = false;
        bool calibratedLogged = false;
        Logger.Info($"Pedal axis = {pedalAxis}");

        while (!token.IsCancellationRequested)
        {
            ImuSample s0, s1, s2;
            bool throttlePressed = false, brakePressed = false;
            bool recenterPressed = false;
            int batRaw = 0;
            double stickAxisValue = 0;

            if (pedalSide == JoyConSide.Right)
            {
                var st = dev.ReadAsRight();
                batRaw = st.Battery;
                stickAxisValue = config.StickAxis == StickAxis.X ? st.StickX : st.StickY;
                s0 = st.Sample0; s1 = st.Sample1; s2 = st.Sample2;

                var throttleBtn = RightJoyConButtonNames.FromName(config.PedalThrottleButton);
                var brakeBtn    = RightJoyConButtonNames.FromName(config.PedalBrakeButton);
                var recenter    = RightJoyConButtonNames.FromName(recenterBtn);
                throttlePressed = throttleBtn != RightJoyConButton.None && st.Buttons.HasFlag(throttleBtn);
                brakePressed    = brakeBtn    != RightJoyConButton.None && st.Buttons.HasFlag(brakeBtn);
                recenterPressed = recenter    != RightJoyConButton.None && st.Buttons.HasFlag(recenter);
            }
            else
            {
                var st = dev.Read();
                batRaw = st.Battery;
                stickAxisValue = config.StickAxis == StickAxis.X ? st.StickX : st.StickY;
                s0 = st.Sample0; s1 = st.Sample1; s2 = st.Sample2;

                var throttleBtn = JoyConButtonNames.FromName(config.PedalThrottleButton);
                var brakeBtn    = JoyConButtonNames.FromName(config.PedalBrakeButton);
                var recenter    = JoyConButtonNames.FromName(recenterBtn);
                throttlePressed = throttleBtn != LeftJoyConButton.None && st.Buttons.HasFlag(throttleBtn);
                brakePressed    = brakeBtn    != LeftJoyConButton.None && st.Buttons.HasFlag(brakeBtn);
                recenterPressed = recenter    != LeftJoyConButton.None && st.Buttons.HasFlag(recenter);
            }

            // Bias cal + fusion + tilt
            var bs0 = biasCal.Apply(s0);
            var bs1 = biasCal.Apply(s1);
            var bs2 = biasCal.Apply(s2);
            if (!calibratedLogged && biasCal.IsCalibrated)
            {
                Logger.Info($"Pedal gyro bias calibrated: x={biasCal.BiasXDps:F3} y={biasCal.BiasYDps:F3} z={biasCal.BiasZDps:F3}");
                calibratedLogged = true;
            }

            // Continuous bias refinement when still
            double peakMag = Math.Max(Math.Max(
                Math.Sqrt(bs0.GxDps * bs0.GxDps + bs0.GyDps * bs0.GyDps + bs0.GzDps * bs0.GzDps),
                Math.Sqrt(bs1.GxDps * bs1.GxDps + bs1.GyDps * bs1.GyDps + bs1.GzDps * bs1.GzDps)),
                Math.Sqrt(bs2.GxDps * bs2.GxDps + bs2.GyDps * bs2.GyDps + bs2.GzDps * bs2.GzDps));
            if (peakMag < BiasRefinementThresholdDps)
            {
                biasCal.UpdateRunning(s0);
                biasCal.UpdateRunning(s1);
                biasCal.UpdateRunning(s2);
            }

            fusion.Update(bs0, SampleDtSeconds);
            fusion.Update(bs1, SampleDtSeconds);
            fusion.Update(bs2, SampleDtSeconds);
            wheel.Apply(bs0, SampleDtSeconds);
            wheel.Apply(bs1, SampleDtSeconds);
            wheel.Apply(bs2, SampleDtSeconds);
            var (gx, gy, _) = fusion.GravityInBody();
            tilt.Update(gx, gy);

            var (roll, pitch, yaw) = fusion.GetEulerDegrees();
            double angle = AngleSource.Pick(pedalAxis, roll, pitch, yaw, wheel.AngleDegrees, tilt.AngleDegrees);

            // Recenter on rising edge of pedal-specific recenter button OR external request
            bool external;
            lock (_gate) { external = _recenterRequested; _recenterRequested = false; }
            if ((recenterPressed && !prevRecenterPressed) || external)
            {
                tilt.SetNeutral();
                wheel.Reset();
                Logger.Info($"Pedal recenter (axis={pedalAxis}, current angle={angle:F1}°)");
            }
            prevRecenterPressed = recenterPressed;

            // Compute pedals
            double throttle = 0, brake = 0;
            switch (config.ThrottleBrake)
            {
                case ThrottleBrakeMode.PedalButtons:
                    throttle = throttlePressed ? 1.0 : 0.0;
                    brake    = brakePressed    ? 1.0 : 0.0;
                    break;
                case ThrottleBrakeMode.PedalTilt:
                    (throttle, brake) = TiltPedalMath.Compute(angle, pedalSettings);
                    break;
                case ThrottleBrakeMode.PedalStick:
                    (throttle, brake) = StickPedalMath.Compute(stickAxisValue, config.StickDeadzone);
                    break;
            }

            lock (_gate)
            {
                _status = new PedalWorkerStatus(
                    Running: true,
                    Connected: true,
                    Throttle: throttle,
                    Brake: brake,
                    TiltAngleDeg: angle,
                    BatteryPercent: JoyConBattery.Percent(batRaw),
                    ErrorMessage: null);
            }
        }
    }

    private static bool SleepCancellable(int ms, CancellationToken token)
    {
        try { Task.Delay(ms, token).Wait(token); return true; }
        catch (OperationCanceledException) { return false; }
        catch (AggregateException) { return false; }
    }
}
