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
    int BatteryPercent,
    bool Charging,
    bool GyroSaturated,
    int DroppedPacketsTotal,
    string? ErrorMessage)
{
    public static readonly WorkerStatus Stopped = new(false, 0, 0, 0, 0, false, false, 0, null);
}

/// <summary>
/// Owns the runtime pipeline: HID polling, sensor fusion, steering math, and vJoy output.
/// One worker at a time; calling <see cref="Start"/> while running stops the previous one.
/// All public methods are thread-safe.
/// </summary>
public sealed class SteeringWorker : IDisposable
{
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

    /// <summary>
    /// True when any gyro axis is close enough to the int16 rail that we know
    /// the chip's ±2000 dps full-scale was hit. Threshold a hair below the rail
    /// (32760 out of 32767) to leave room for noise.
    /// </summary>
    private static bool GyroSaturated(ImuSample s)
    {
        // We have dps values (already scaled by 0.06103). 32760 LSB * 0.06103 ≈ 1999.4 dps.
        const double satDps = 1995.0;
        return Math.Abs(s.GxDps) >= satDps
            || Math.Abs(s.GyDps) >= satDps
            || Math.Abs(s.GzDps) >= satDps;
    }

    private void Loop(AppConfig config, JoyConDevice jc, VJoyOutput vjoy, CancellationToken token)
    {
        var axis = SteeringAxisSelector.Resolve(config.Axis, config.Side);
        var fusion = new MadgwickFilter(config.MadgwickBeta);
        var wheel = new WheelAxisIntegrator();
        var tilt = new GravityTiltAxis();
        var biasCal = new GyroBiasCalibrator(); // 200 samples = 1 s at 200 Hz
        var steering = new SteeringMath(new SteeringSettings(
            config.RangeDegrees, config.DeadzoneDegrees, config.SmoothingMs, config.Invert));
        var mapper = new WheelOutputMapper(config);
        var recenterButton = JoyConButtonNames.FromName(config.RecenterButton);
        var edge = new RisingEdgeDetector();
        bool wasCalibrated = false;
        Logger.Info($"Steering axis = {axis}. Calibrating gyro — hold still for ~1 s…");

        // Drop-detection via Joy-Con Timer byte (increments by 3 per packet at 5 ms/tick).
        int? lastTimer = null;
        int dropsSinceHeartbeat = 0;
        int dropsTotal = 0;
        // Continuous bias refinement (only while controller is detected as stationary).
        const double biasRefinementThresholdDps = 1.5; // tighter than ZUPT to be conservative

        var sw = Stopwatch.StartNew();
        long lastMs = 0;
        long lastPacketMs = 0;
        long lastHeartbeatMs = 0;
        int framesSinceHeartbeat = 0;
        bool saturatedSinceHeartbeat = false;
        double packetDtMs = 15.0;
        // Auto-recenter state
        double idleSecondsAccum = 0;
        bool autoRecenteredThisIdle = false;
        const double autoRecenterMotionThresholdDps = 5.0;

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

            // The Joy-Con's gyro samples at a fixed 200 Hz (5 ms / sample), regardless of
            // how BT delivers them. Bluetooth often batches packets — Read() can return
            // multiple buffered reports in quick succession (dt≈0), then block for the next
            // batch. Using wall-clock dt would under-attribute motion to the burst samples;
            // we ALWAYS credit each IMU sample with exactly 5 ms.
            const double sampleDtSeconds = 0.005;
            long packetNowMs = sw.ElapsedMilliseconds;
            packetDtMs = lastPacketMs == 0 ? 15.0 : packetNowMs - lastPacketMs;
            lastPacketMs = packetNowMs;

            // Saturation: int16 gyro at ±32760+ LSB ≈ at the ±2000 dps rail. Means fast
            // motion was clipped and the integrated angle is under-counting reality.
            bool saturatedThisTick =
                  GyroSaturated(state.Sample0) || GyroSaturated(state.Sample1) || GyroSaturated(state.Sample2);
            if (saturatedThisTick) saturatedSinceHeartbeat = true;

            // Detect dropped packets via Joy-Con's Timer byte.
            // Each packet covers 3 IMU samples = 3 ticks of 5 ms each. So delta == 3 = no drop;
            // delta == 6 = 1 packet dropped, etc. (mod 256 for the byte rollover.)
            if (lastTimer is int prev)
            {
                int delta = (state.Timer - prev) & 0xFF;
                if (delta > 3 && delta < 200) // ignore huge gaps (probably reconnect)
                {
                    int dropped = (delta - 3) / 3;
                    dropsSinceHeartbeat += dropped;
                    dropsTotal += dropped;
                }
            }
            lastTimer = state.Timer;

            var s0 = biasCal.Apply(state.Sample0);
            var s1 = biasCal.Apply(state.Sample1);
            var s2 = biasCal.Apply(state.Sample2);

            // Continuous bias refinement: when the controller is held still, slowly
            // pull the gyro bias toward the current reading. Catches thermal drift
            // without requiring a full recalibration.
            double peakMag = Math.Max(Math.Max(
                Math.Sqrt(s0.GxDps * s0.GxDps + s0.GyDps * s0.GyDps + s0.GzDps * s0.GzDps),
                Math.Sqrt(s1.GxDps * s1.GxDps + s1.GyDps * s1.GyDps + s1.GzDps * s1.GzDps)),
                Math.Sqrt(s2.GxDps * s2.GxDps + s2.GyDps * s2.GyDps + s2.GzDps * s2.GzDps));
            if (peakMag < biasRefinementThresholdDps)
            {
                biasCal.UpdateRunning(state.Sample0);
                biasCal.UpdateRunning(state.Sample1);
                biasCal.UpdateRunning(state.Sample2);
            }

            fusion.Update(s0, sampleDtSeconds);
            fusion.Update(s1, sampleDtSeconds);
            fusion.Update(s2, sampleDtSeconds);
            wheel.Apply(s0, sampleDtSeconds);
            wheel.Apply(s1, sampleDtSeconds);
            wheel.Apply(s2, sampleDtSeconds);
            var (gx, gy, _) = fusion.GravityInBody();
            tilt.Update(gx, gy);
            if (!wasCalibrated && biasCal.IsCalibrated)
            {
                Logger.Info($"Gyro bias calibrated: x={biasCal.BiasXDps:F3} y={biasCal.BiasYDps:F3} z={biasCal.BiasZDps:F3} dps");
                wasCalibrated = true;
            }

            var (roll, pitch, yaw) = fusion.GetEulerDegrees();
            double angle = AngleSource.Pick(axis, roll, pitch, yaw, wheel.AngleDegrees, tilt.AngleDegrees);

            bool recenterEdge = recenterButton != LeftJoyConButton.None
                                && edge.Update(state.Buttons.HasFlag(recenterButton));
            bool externalRecenter;
            lock (_gate)
            {
                externalRecenter = _recenterRequested;
                _recenterRequested = false;
            }
            if (recenterEdge || externalRecenter)
            {
                steering.Recenter(angle);
                tilt.SetNeutral();
                wheel.Reset();
                idleSecondsAccum = 0;
                autoRecenteredThisIdle = true;
            }

            // Auto-recenter when controller has been still for the configured idle period.
            // Counteracts integrated drift after a few hard corners: lift off, let it settle,
            // and we silently snap the centre to wherever it ends up.
            if (config.AutoRecenterIdleSeconds > 0 && biasCal.IsCalibrated)
            {
                double peakAbsGyro = Math.Max(Math.Max(
                    Math.Abs(s0.GxDps) + Math.Abs(s0.GyDps) + Math.Abs(s0.GzDps),
                    Math.Abs(s1.GxDps) + Math.Abs(s1.GyDps) + Math.Abs(s1.GzDps)),
                    Math.Abs(s2.GxDps) + Math.Abs(s2.GyDps) + Math.Abs(s2.GzDps));

                if (peakAbsGyro < autoRecenterMotionThresholdDps)
                {
                    idleSecondsAccum += 3 * sampleDtSeconds; // 3 samples per packet
                    if (!autoRecenteredThisIdle && idleSecondsAccum >= config.AutoRecenterIdleSeconds)
                    {
                        steering.Recenter(angle);
                        autoRecenteredThisIdle = true;
                        Logger.Info($"Auto-recenter after {idleSecondsAccum:F1}s idle (angle was {angle:F1}°)");
                    }
                }
                else
                {
                    idleSecondsAccum = 0;
                    autoRecenteredThisIdle = false;
                }
            }

            long now = sw.ElapsedMilliseconds;
            double dtMs = lastMs == 0 ? 16 : now - lastMs;
            lastMs = now;

            double steer = steering.Compute(angle, dtMs);
            mapper.Apply(steer, state, vjoy);

            int batPct = JoyConBattery.Percent(state.Battery);
            bool charging = JoyConBattery.IsCharging(state.Battery);

            lock (_gate)
            {
                _status = new WorkerStatus(true, angle, steer, state.StickY, batPct, charging, saturatedThisTick, dropsTotal, null);
            }

            framesSinceHeartbeat++;
            if (now - lastHeartbeatMs >= 100) // 10 Hz heartbeat
            {
                string satTag = saturatedSinceHeartbeat ? " SAT!" : "";
                string dropTag = dropsSinceHeartbeat > 0 ? $" drops+={dropsSinceHeartbeat}" : "";
                Logger.Info($"hb angle={angle:F2}° steer={steer:+0.000;-0.000;0.000} stickY={state.StickY:+0.00;-0.00;0.00} bat={batPct}%{(charging ? "+chg" : "")} frames+={framesSinceHeartbeat} dt={packetDtMs:F1}ms{satTag}{dropTag}");
                framesSinceHeartbeat = 0;
                saturatedSinceHeartbeat = false;
                dropsSinceHeartbeat = 0;
                lastHeartbeatMs = now;
            }
        }
    }
}
