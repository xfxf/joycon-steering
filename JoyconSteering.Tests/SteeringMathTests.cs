using JoyconSteering.Steering;
using Xunit;

namespace JoyconSteering.Tests;

public class SteeringMathTests
{
    // RangeDegrees is "degrees per side" — full lock at +RangeDegrees / -RangeDegrees.
    private static SteeringSettings Default(double range = 90, double dead = 0, double smoothMs = 0, bool invert = false)
        => new(RangeDegrees: range, DeadzoneDegrees: dead, SmoothingMs: smoothMs, Invert: invert);

    [Fact]
    public void Centered_ReturnsZero()
    {
        var s = new SteeringMath(Default());
        Assert.Equal(0.0, s.Compute(angleDegrees: 0, dtMs: 16), 3);
    }

    [Fact]
    public void FullPositiveLock_AtRange_ReturnsOne()
    {
        var s = new SteeringMath(Default(range: 90));
        Assert.Equal(1.0, s.Compute(90, 16), 3);
    }

    [Fact]
    public void FullNegativeLock_AtRange_ReturnsMinusOne()
    {
        var s = new SteeringMath(Default(range: 90));
        Assert.Equal(-1.0, s.Compute(-90, 16), 3);
    }

    [Fact]
    public void HalfLock_AtHalfRange_ReturnsHalf()
    {
        var s = new SteeringMath(Default(range: 90));
        Assert.Equal(0.5, s.Compute(45, 16), 3);
    }

    [Fact]
    public void BeyondRange_ClampedToOne()
    {
        var s = new SteeringMath(Default(range: 90));
        Assert.Equal(1.0, s.Compute(135, 16), 3);
        Assert.Equal(-1.0, s.Compute(-135, 16), 3);
    }

    [Fact]
    public void LargerRange_LessSensitive()
    {
        var s = new SteeringMath(Default(range: 180));
        // 90° is now half-lock under a 180-per-side range
        Assert.Equal(0.5, s.Compute(90, 16), 3);
    }

    [Fact]
    public void Deadzone_HoldsZeroWithinDeadband()
    {
        var s = new SteeringMath(Default(range: 90, dead: 5));
        Assert.Equal(0.0, s.Compute(2, 16), 3);
        Assert.Equal(0.0, s.Compute(-3, 16), 3);
    }

    [Fact]
    public void Deadzone_OutputResumesPastDeadband()
    {
        // 10° past a 5° deadzone in a 90°-per-side range → (10-5)/(90-5) ≈ 0.0588
        var s = new SteeringMath(Default(range: 90, dead: 5));
        Assert.Equal((10.0 - 5) / (90.0 - 5), s.Compute(10, 16), 3);
    }

    [Fact]
    public void Invert_FlipsSign()
    {
        var s = new SteeringMath(Default(range: 90, invert: true));
        Assert.Equal(-0.5, s.Compute(45, 16), 3);
    }

    [Fact]
    public void Recenter_ResetsCenterToCurrentAngle()
    {
        var s = new SteeringMath(Default(range: 90));
        Assert.True(s.Compute(30, 16) > 0.3);
        s.Recenter(30);
        Assert.Equal(0.0, s.Compute(30, 16), 3);
        Assert.Equal(0.5, s.Compute(75, 16), 3);
    }

    [Fact]
    public void Smoothing_Off_OutputIsImmediate()
    {
        var s = new SteeringMath(Default(range: 90, smoothMs: 0));
        Assert.Equal(0.5, s.Compute(45, 16), 3);
    }

    [Fact]
    public void Smoothing_On_ResponseLagsToward_Target()
    {
        var s = new SteeringMath(Default(range: 90, smoothMs: 50));
        var step1 = s.Compute(45, 16);
        Assert.True(step1 > 0 && step1 < 0.5, $"Expected partial response, got {step1}");
        double v = step1;
        for (int i = 0; i < 50; i++) v = s.Compute(45, 16);
        Assert.Equal(0.5, v, 2);
    }

    [Fact]
    public void Smoothing_RespectsDt_LargerDtConvergesFaster()
    {
        var fast = new SteeringMath(Default(range: 90, smoothMs: 50));
        var slow = new SteeringMath(Default(range: 90, smoothMs: 50));
        var bigStep = fast.Compute(45, dtMs: 100);
        var smallStep = slow.Compute(45, dtMs: 5);
        Assert.True(bigStep > smallStep, $"100ms ({bigStep}) should converge more than 5ms ({smallStep})");
    }
}

public class SteeringAxisSelectorTests
{
    [Fact]
    public void Auto_PicksWheel_ForSidewaysGrip()
    {
        Assert.Equal(SelectedAxis.Wheel,
            SteeringAxisSelector.Resolve(JoyconSteering.Config.SteeringAxis.Auto, JoyconSteering.Config.JoyConSide.Left));
    }

    [Fact]
    public void Explicit_OverridesAuto()
    {
        Assert.Equal(SelectedAxis.Roll,
            SteeringAxisSelector.Resolve(JoyconSteering.Config.SteeringAxis.Roll, JoyconSteering.Config.JoyConSide.Left));
        Assert.Equal(SelectedAxis.Pitch,
            SteeringAxisSelector.Resolve(JoyconSteering.Config.SteeringAxis.Pitch, JoyconSteering.Config.JoyConSide.Right));
    }
}
