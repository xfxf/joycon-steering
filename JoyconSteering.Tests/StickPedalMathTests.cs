using JoyconSteering.Steering;
using Xunit;

namespace JoyconSteering.Tests;

public class StickPedalMathTests
{
    [Fact]
    public void Centered_BothZero()
    {
        var (t, b) = StickPedalMath.Compute(0, 0.15);
        Assert.Equal(0, t); Assert.Equal(0, b);
    }

    [Fact]
    public void WithinDeadzone_BothZero()
    {
        var (t, b) = StickPedalMath.Compute(0.1, 0.15);
        Assert.Equal(0, t); Assert.Equal(0, b);
        (t, b) = StickPedalMath.Compute(-0.1, 0.15);
        Assert.Equal(0, t); Assert.Equal(0, b);
    }

    [Fact]
    public void Positive_DrivesThrottle()
    {
        // 0.575 with dz=0.15 → (0.575 - 0.15) / (1 - 0.15) = 0.5
        var (t, b) = StickPedalMath.Compute(0.575, 0.15);
        Assert.Equal(0.5, t, 3);
        Assert.Equal(0.0, b);
    }

    [Fact]
    public void Negative_DrivesBrake()
    {
        var (t, b) = StickPedalMath.Compute(-0.575, 0.15);
        Assert.Equal(0.0, t);
        Assert.Equal(0.5, b, 3);
    }

    [Fact]
    public void FullStick_EqualsOne()
    {
        Assert.Equal((1.0, 0.0), StickPedalMath.Compute(1.0, 0.15));
        Assert.Equal((0.0, 1.0), StickPedalMath.Compute(-1.0, 0.15));
    }

    [Fact]
    public void NegativeOrTooLargeDeadzone_IsClamped()
    {
        // Out-of-range deadzones shouldn't divide by zero / explode.
        var (t, _) = StickPedalMath.Compute(0.5, -0.5);
        Assert.InRange(t, 0, 1);
        var (_, b) = StickPedalMath.Compute(-0.5, 5);
        Assert.InRange(b, 0, 1);
    }
}
