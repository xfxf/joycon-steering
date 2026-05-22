using JoyconSteering.Steering;
using Xunit;

namespace JoyconSteering.Tests;

public class TiltPedalMathTests
{
    private static TiltPedalSettings Default(double range = 45, double dead = 3, bool invert = false)
        => new(RangeDegrees: range, DeadzoneDegrees: dead, Invert: invert);

    [Fact]
    public void Centered_BothZero()
    {
        var (t, b) = TiltPedalMath.Compute(0, Default());
        Assert.Equal(0, t);
        Assert.Equal(0, b);
    }

    [Fact]
    public void WithinDeadzone_BothZero()
    {
        var (t, b) = TiltPedalMath.Compute(2, Default(range: 45, dead: 3));
        Assert.Equal(0, t);
        Assert.Equal(0, b);

        (t, b) = TiltPedalMath.Compute(-2.5, Default(range: 45, dead: 3));
        Assert.Equal(0, t);
        Assert.Equal(0, b);
    }

    [Fact]
    public void PositiveTilt_DrivesThrottle_NotBrake()
    {
        var (t, b) = TiltPedalMath.Compute(24, Default(range: 45, dead: 3));
        // (24 - 3) / (45 - 3) = 21/42 = 0.5
        Assert.Equal(0.5, t, 3);
        Assert.Equal(0.0, b, 3);
    }

    [Fact]
    public void NegativeTilt_DrivesBrake_NotThrottle()
    {
        var (t, b) = TiltPedalMath.Compute(-24, Default(range: 45, dead: 3));
        Assert.Equal(0.0, t, 3);
        Assert.Equal(0.5, b, 3);
    }

    [Fact]
    public void AtFullRange_Equals1()
    {
        var (t, b) = TiltPedalMath.Compute(45, Default(range: 45, dead: 3));
        Assert.Equal(1.0, t, 3);
        Assert.Equal(0.0, b);

        (t, b) = TiltPedalMath.Compute(-45, Default(range: 45, dead: 3));
        Assert.Equal(0.0, t);
        Assert.Equal(1.0, b, 3);
    }

    [Fact]
    public void BeyondRange_Clamped()
    {
        var (t, _) = TiltPedalMath.Compute(120, Default(range: 45, dead: 3));
        Assert.Equal(1.0, t);
        var (_, b) = TiltPedalMath.Compute(-120, Default(range: 45, dead: 3));
        Assert.Equal(1.0, b);
    }

    [Fact]
    public void Invert_SwapsThrottleAndBrake()
    {
        var (t, b) = TiltPedalMath.Compute(24, Default(range: 45, dead: 3, invert: true));
        Assert.Equal(0.0, t);
        Assert.Equal(0.5, b, 3);
    }

    [Fact]
    public void ZeroRange_ReturnsZero_Safely()
    {
        var (t, b) = TiltPedalMath.Compute(10, Default(range: 0, dead: 0));
        Assert.Equal(0, t);
        Assert.Equal(0, b);
    }
}
