using JoyconSteering.Config;
using JoyconSteering.JoyCon;
using JoyconSteering.JoyCon.Fusion;
using JoyconSteering.Output;
using JoyconSteering.Steering;
using JoyconSteering.Tests.TestHelpers;
using Xunit;

namespace JoyconSteering.Tests;

/// <summary>
/// End-to-end tests for the non-hardware pipeline:
///     (bytes or JoyConState) → fusion → steering math → output mapper → wheel sink
///
/// These exist so future changes to any pipeline stage can't quietly break the
/// composition. Unit tests cover each stage in isolation; these cover the seams.
/// Hardware-touching layers (HID I/O and vJoy P/Invoke) are NOT exercised here —
/// they have to be validated manually with `dotnet run` against a real Joy-Con.
/// </summary>
public class PipelineIntegrationTests
{
    private const double Dt = 0.005;
    private const int OneSecondSteps = 200;

    private static AppConfig DefaultConfig() => new()
    {
        Side = JoyConSide.Left,
        VJoyDeviceId = 1,
        Axis = SteeringAxis.Auto,
        RangeDegrees = 90,
        DeadzoneDegrees = 0,
        SmoothingMs = 0,
        Invert = false,
        ThrottleBrake = ThrottleBrakeMode.Stick,
        StickDeadzone = 0.15,
        ButtonMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["up"] = 1, ["down"] = 2, ["left"] = 3, ["right"] = 4,
            ["l"] = 5, ["zl"] = 6, ["minus"] = 7, ["stick"] = 8,
            ["sl"] = 9, ["sr"] = 10, ["capture"] = 11,
        },
        RecenterButton = "stick",
        MadgwickBeta = 0.05,
    };

    private static JoyConState State(
        LeftJoyConButton buttons = LeftJoyConButton.None,
        double stickX = 0,
        double stickY = 0,
        ImuSample? sample = null)
    {
        var s = sample ?? new ImuSample(0, 0, 1, 0, 0, 0); // gravity straight down
        return new JoyConState(buttons, stickX, stickY, 8, s, s, s);
    }

    [Fact]
    public void Recenter_ThenRotate_ProducesProportionalSteering()
    {
        // Auto axis resolves to Wheel (body-frame gyro Z integration).
        // Use invert=false here so a positive gyro Z input yields a positive steer output.
        var cfg = DefaultConfig() with { Invert = false };
        var fusion = new MadgwickFilter(cfg.MadgwickBeta);
        var wheelInt = new WheelAxisIntegrator();
        var steering = new SteeringMath(new SteeringSettings(cfg.RangeDegrees, cfg.DeadzoneDegrees, cfg.SmoothingMs, cfg.Invert));
        var mapper = new WheelOutputMapper(cfg);
        var sink = new FakeWheelOutput();
        var edge = new RisingEdgeDetector();
        var axis = SteeringAxisSelector.Resolve(cfg.Axis, cfg.Side);

        // Phase 1: hold level for 1s, press recenter, ensure steering is centered.
        var levelState = State(buttons: LeftJoyConButton.Stick);
        for (int i = 0; i < OneSecondSteps; i++)
        {
            fusion.Update(levelState.Sample0, Dt);
            wheelInt.Apply(levelState.Sample0, Dt);
        }
        var (r0, p0, y0) = fusion.GetEulerDegrees();
        var angle0 = AngleSource.Pick(axis, r0, p0, y0, wheelInt.AngleDegrees);
        if (edge.Update(true)) steering.Recenter(angle0);
        var out0 = steering.Compute(angle0, 16);
        mapper.Apply(out0, levelState, sink);
        Assert.Equal(0.0, sink.Steering, 2);

        // Phase 2: rotate at 45 dps around body Z for 1 second → 45° wheel rotation.
        // With 90°-per-side range, this should produce ~0.5 steering.
        // Magnitude is well above the ZUPT threshold so integration proceeds normally.
        var rotState = State(sample: new ImuSample(0, 0, 1, GxDps: 0, GyDps: 0, GzDps: 45));
        for (int i = 0; i < OneSecondSteps; i++)
        {
            fusion.Update(rotState.Sample0, Dt);
            wheelInt.Apply(rotState.Sample0, Dt);
        }
        var (r, p, y) = fusion.GetEulerDegrees();
        var angle = AngleSource.Pick(axis, r, p, y, wheelInt.AngleDegrees);
        var output = steering.Compute(angle, 16);
        mapper.Apply(output, rotState, sink);

        Assert.InRange(sink.Steering, 0.4, 0.6);
    }

    [Fact]
    public void WheelAxis_PastFullLock_ClampsAndDoesNotWrap()
    {
        // 270° of physical rotation with 180°-per-side range should clamp at +1.0,
        // NOT wrap around to a negative value (the bug the user reported).
        var cfg = DefaultConfig() with { Invert = false };
        var wheelInt = new WheelAxisIntegrator();
        var steering = new SteeringMath(new SteeringSettings(cfg.RangeDegrees, cfg.DeadzoneDegrees, cfg.SmoothingMs, cfg.Invert));
        var mapper = new WheelOutputMapper(cfg);
        var sink = new FakeWheelOutput();

        // Drive 270° of rotation at 90 dps for 3 seconds.
        var rotState = State(sample: new ImuSample(0, 0, 1, GxDps: 0, GyDps: 0, GzDps: 90));
        for (int i = 0; i < OneSecondSteps * 3; i++)
            wheelInt.Apply(rotState.Sample0, Dt);

        Assert.InRange(wheelInt.AngleDegrees, 265, 275); // unbounded, no wrap
        var output = steering.Compute(wheelInt.AngleDegrees, 16);
        mapper.Apply(output, rotState, sink);
        Assert.Equal(1.0, sink.Steering, 2); // clamped, not wrapped
    }

    [Fact]
    public void StickThrottle_PropagatesThroughPipeline()
    {
        var cfg = DefaultConfig();
        var mapper = new WheelOutputMapper(cfg);
        var sink = new FakeWheelOutput();

        mapper.Apply(0.0, State(stickY: 0.575), sink); // 0.5 throttle after 0.15 deadzone
        Assert.Equal(0.5, sink.Throttle, 2);
        Assert.Equal(0.0, sink.Brake);
    }

    [Fact]
    public void ButtonPress_PropagatesThroughPipeline()
    {
        var cfg = DefaultConfig();
        var mapper = new WheelOutputMapper(cfg);
        var sink = new FakeWheelOutput();

        mapper.Apply(0.0, State(buttons: LeftJoyConButton.Up | LeftJoyConButton.Capture), sink);
        Assert.True(sink.Buttons[1]);   // up → vJoy 1
        Assert.True(sink.Buttons[11]);  // capture → vJoy 11
        Assert.False(sink.Buttons[2]);  // down not pressed
    }

    [Fact]
    public void RawBytes_ThroughParser_ThroughMapper_Works()
    {
        // Build a synthetic 0x30 report: L button held, stick centered, accel = 1g down,
        // gyro stationary. Feed through parser → mapper → confirm vJoy button 5 (L) set.
        var buf = ReportBuilder.EmptyStandard();
        ReportBuilder.WithLeftButton(buf, sideMask: 0x40 /*L*/, sharedMask: 0);
        ReportBuilder.WithLeftStick(buf,
            InputReportParser.StickCenterNom, InputReportParser.StickCenterNom);
        for (int i = 0; i < 3; i++)
            ReportBuilder.WithImuSample(buf, i, ax: 0, ay: 0, az: 4096, gx: 0, gy: 0, gz: 0);

        var state = InputReportParser.ParseStandard(buf, isLeft: true);
        var cfg = DefaultConfig() with { ThrottleBrake = ThrottleBrakeMode.Buttons };
        var sink = new FakeWheelOutput();
        new WheelOutputMapper(cfg).Apply(0.0, state, sink);

        Assert.True(sink.Buttons[5]); // L → vJoy 5
        Assert.Equal(1.0, sink.Throttle); // L = throttle in buttons mode
    }

    [Fact]
    public void Smoothing_DampsAbruptInput_Through_FullPipeline()
    {
        var cfg = DefaultConfig() with { SmoothingMs = 50 };
        var steering = new SteeringMath(new SteeringSettings(cfg.RangeDegrees, cfg.DeadzoneDegrees, cfg.SmoothingMs, cfg.Invert));
        var mapper = new WheelOutputMapper(cfg);
        var sink = new FakeWheelOutput();

        // Jump straight to 45° physical angle in one tick: smoothed output should NOT
        // immediately read 0.5; it should be smaller and converge over many ticks.
        var first = steering.Compute(45, 16);
        mapper.Apply(first, State(), sink);
        Assert.True(sink.Steering < 0.5);
        Assert.True(sink.Steering > 0.0);

        double final = 0;
        for (int i = 0; i < 50; i++)
        {
            final = steering.Compute(45, 16);
            mapper.Apply(final, State(), sink);
        }
        Assert.Equal(0.5, sink.Steering, 1);
    }
}

