using System.Diagnostics;
using JoyconSteering.Config;
using JoyconSteering.JoyCon;
using JoyconSteering.JoyCon.Fusion;
using JoyconSteering.Output;
using JoyconSteering.Steering;

namespace JoyconSteering;

/// <summary>Snapshot of the worker's current state for the UI to display.</summary>
public sealed record WorkerStatus(
    bool Running,
    double AngleDeg,
    double Steer,
    double StickY,
    int Battery,
    string? ErrorMessage)
{
    public static readonly WorkerStatus Stopped = new(false, 0, 0, 0, 0, null);
}

/// <summary>
/// Owns the runtime pipeline: HID polling, sensor fusion, steering math, and vJoy output.
/// One worker at a time; calling <see cref="Start"/> while running stops the previous one.
/// All public methods are thread-safe.
/// </summary>
public sealed class SteeringWorker : IDisposable
{
    private const double JoyConSampleDtSeconds = 0.005;

    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private Task? _task;
    private WorkerStatus _status = WorkerStatus.Stopped;
    private bool _recenterRequested;
    private bool _recalibrateRequested;

    public WorkerStatus Status
    {
        get { lock (_gate) return _status; }
    }

    public void Start(AppConfig config)
    {
        Stop();
        lock (_gate)
        {
            _cts = new CancellationTokenSource();
            _status = WorkerStatus.Stopped with { Running = true };
            _task = Task.Run(() => Run(config, _cts.Token), _cts.Token);
        }
    }

    public void Stop()
    {
        CancellationTokenSource? cts;
        Task? task;
        lock (_gate)
        {
            cts = _cts;
            task = _task;
            _cts = null;
            _task = null;
        }

        if (cts is null) return;

        try { cts.Cancel(); } catch { /* swallow */ }
        try { task?.Wait(2000); } catch { /* swallow */ }
        cts.Dispose();

        lock (_gate)
        {
            _status = WorkerStatus.Stopped;
        }
    }

    public void Recenter()
    {
        lock (_gate) _recenterRequested = true;
    }

    public void RecalibrateGyro()
    {
        lock (_gate) _recalibrateRequested = true;
    }

    public void Dispose() => Stop();

    private void Run(AppConfig config, CancellationToken token)
    {
        VJoyOutput? vjoy = null;
        try
        {
            Logger.Info($"Worker starting: vjoy={config.VJoyDeviceId} side={config.Side}");
            vjoy = new VJoyOutput(config.VJoyDeviceId);
            vjoy.Open();
            Logger.Info($"vJoy device {config.VJoyDeviceId} acquired");

            // Outer loop: keep trying to acquire the Joy-Con whenever it goes away.
            // BT can drop the link on Joy-Con sleep or transient noise; this lets the user
            // simply turn the controller back on without restarting the app.
            while (!token.IsCancellationRequested)
            {
                JoyConDevice? jc = null;
                try
                {
                    jc = config.Side == JoyConSide.Left ? JoyConDevice.OpenLeft() : JoyConDevice.OpenRight();
                    jc.Initialize();
                    Logger.Info($"Joy-Con ({config.Side}) initialized");
                    lock (_gate) _status = _status with { Running = true, ErrorMessage = null };

                    Loop(config, jc, vjoy, token);
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex) when (ex is IOException
                                        || ex is TimeoutException
                                        || ex is InvalidOperationException)
                {
                    Logger.Warn($"Joy-Con link lost: {ex.Message}. Retrying in 3 s…");
                    lock (_gate)
                    {
                        _status = _status with
                        {
                            Running = false,
                            ErrorMessage = "Joy-Con disconnected — waiting to reconnect. Power it on / re-wake it (any button)."
                        };
                    }
                }
                finally
                {
                    jc?.Dispose();
                }

                if (!SleepCancellable(3000, token)) return;
            }
        }
        catch (OperationCanceledException) { Logger.Info("Worker cancelled"); }
        catch (Exception ex)
        {
            Logger.Error("Worker failed (non-recoverable)", ex);
            lock (_gate) _status = _status with { Running = false, ErrorMessage = ex.Message };
        }
        finally
        {
            vjoy?.Dispose();
            lock (_gate)
            {
                if (_status.ErrorMessage is null) _status = WorkerStatus.Stopped;
                else _status = _status with { Running = false };
            }
        }
    }

    private static bool SleepCancellable(int ms, CancellationToken token)
    {
        try { Task.Delay(ms, token).Wait(token); return true; }
        catch (OperationCanceledException) { return false; }
        catch (AggregateException) { return false; }
    }

    private void Loop(AppConfig config, JoyConDevice jc, VJoyOutput vjoy, CancellationToken token)
    {
        var axis = SteeringAxisSelector.Resolve(config.Axis, config.Side);
        var fusion = new MadgwickFilter(config.MadgwickBeta);
        var wheel = new WheelAxisIntegrator();
        var biasCal = new GyroBiasCalibrator(); // 200 samples = 1 s at 200 Hz
        var steering = new SteeringMath(new SteeringSettings(
            config.RangeDegrees, config.DeadzoneDegrees, config.SmoothingMs, config.Invert));
        var mapper = new WheelOutputMapper(config);
        var recenterButton = JoyConButtonNames.FromName(config.RecenterButton);
        var edge = new RisingEdgeDetector();
        bool wasCalibrated = false;
        Logger.Info($"Steering axis = {axis}. Calibrating gyro — hold still for ~1 s…");

        var sw = Stopwatch.StartNew();
        long lastMs = 0;
        long lastHeartbeatMs = 0;
        int framesSinceHeartbeat = 0;

        while (!token.IsCancellationRequested)
        {
            JoyConState state;
            try { state = jc.Read(); }
            catch (TimeoutException) { continue; }

            bool recalNow;
            lock (_gate) { recalNow = _recalibrateRequested; _recalibrateRequested = false; }
            if (recalNow)
            {
                biasCal.Restart();
                wheel.Reset();
                wasCalibrated = false;
                Logger.Info("Gyro recalibration requested — hold still for ~1 s…");
            }

            var s0 = biasCal.Apply(state.Sample0);
            var s1 = biasCal.Apply(state.Sample1);
            var s2 = biasCal.Apply(state.Sample2);
            fusion.Update(s0, JoyConSampleDtSeconds);
            fusion.Update(s1, JoyConSampleDtSeconds);
            fusion.Update(s2, JoyConSampleDtSeconds);
            wheel.Apply(s0, JoyConSampleDtSeconds);
            wheel.Apply(s1, JoyConSampleDtSeconds);
            wheel.Apply(s2, JoyConSampleDtSeconds);
            if (!wasCalibrated && biasCal.IsCalibrated)
            {
                Logger.Info($"Gyro bias calibrated: x={biasCal.BiasXDps:F3} y={biasCal.BiasYDps:F3} z={biasCal.BiasZDps:F3} dps");
                wasCalibrated = true;
            }

            var (roll, pitch, yaw) = fusion.GetEulerDegrees();
            double angle = AngleSource.Pick(axis, roll, pitch, yaw, wheel.AngleDegrees);

            bool recenterEdge = recenterButton != LeftJoyConButton.None
                                && edge.Update(state.Buttons.HasFlag(recenterButton));
            bool externalRecenter;
            lock (_gate)
            {
                externalRecenter = _recenterRequested;
                _recenterRequested = false;
            }
            if (recenterEdge || externalRecenter) steering.Recenter(angle);

            long now = sw.ElapsedMilliseconds;
            double dtMs = lastMs == 0 ? 16 : now - lastMs;
            lastMs = now;

            double steer = steering.Compute(angle, dtMs);
            mapper.Apply(steer, state, vjoy);

            lock (_gate)
            {
                _status = new WorkerStatus(true, angle, steer, state.StickY, state.Battery, null);
            }

            framesSinceHeartbeat++;
            if (now - lastHeartbeatMs >= 100) // 10 Hz heartbeat
            {
                Logger.Info($"hb angle={angle:F2}° steer={steer:+0.000;-0.000;0.000} stickY={state.StickY:+0.00;-0.00;0.00} bat={state.Battery} frames+={framesSinceHeartbeat}");
                framesSinceHeartbeat = 0;
                lastHeartbeatMs = now;
            }
        }
    }
}
