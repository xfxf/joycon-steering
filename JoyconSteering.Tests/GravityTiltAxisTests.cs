using JoyconSteering.JoyCon.Fusion;
using Xunit;

namespace JoyconSteering.Tests;

public class GravityTiltAxisTests
{
    // Synthesises a gravity vector at the given angle θ from "neutral up" (+Y direction),
    // measured CCW from +Y axis (so +θ means tilted right). Matches the conventions used
    // by GravityTiltAxis.Update.
    private static (double X, double Y) GravityAt(double degrees)
    {
        double rad = degrees * Math.PI / 180;
        return (Math.Sin(rad), Math.Cos(rad));
    }

    [Fact]
    public void FirstSample_BecomesNeutral_AngleZero()
    {
        var t = new GravityTiltAxis();
        var (x, y) = GravityAt(0);
        t.Update(x, y);
        Assert.Equal(0.0, t.AngleDegrees, 3);
    }

    [Fact]
    public void TiltRight_PositiveAngle()
    {
        var t = new GravityTiltAxis();
        t.Update(0, 1);
        var (x, y) = GravityAt(45);
        t.Update(x, y);
        Assert.Equal(45.0, t.AngleDegrees, 1);
    }

    [Fact]
    public void TiltLeft_NegativeAngle()
    {
        var t = new GravityTiltAxis();
        t.Update(0, 1);
        var (x, y) = GravityAt(-30);
        t.Update(x, y);
        Assert.Equal(-30.0, t.AngleDegrees, 1);
    }

    [Fact]
    public void SetNeutral_AtCurrentSample_AngleBecomesZero()
    {
        var t = new GravityTiltAxis();
        t.Update(0, 1);
        var (x45, y45) = GravityAt(45);
        t.Update(x45, y45);
        Assert.Equal(45.0, t.AngleDegrees, 1);

        t.SetNeutral();
        t.Update(x45, y45);
        Assert.Equal(0.0, t.AngleDegrees, 1);

        var (x135, y135) = GravityAt(135);
        t.Update(x135, y135);
        Assert.Equal(90.0, t.AngleDegrees, 1);
    }

    [Fact]
    public void Past180_WrapsToNegative()
    {
        // Tilt mode is bounded to (-180°, +180°] by design — past that, it wraps.
        // For unbounded tracking, use the gyro-integrated WheelAxisIntegrator.
        var t = new GravityTiltAxis();
        t.Update(0, 1);
        var (x, y) = GravityAt(200);
        t.Update(x, y);
        // 200° physical = -160° relative (shortest path).
        Assert.InRange(t.AngleDegrees, -165, -155);
    }

    [Fact]
    public void DoesNotDrift_OverManyTicks_WhenInputIsStable()
    {
        // A stable gravity reading should produce the SAME angle every tick, with
        // no accumulator-style drift. This is the bug we fixed by removing the
        // delta-accumulator — symptom was steering centre walking off during use.
        var t = new GravityTiltAxis();
        t.Update(0, 1); // neutral
        var (x, y) = GravityAt(20); // tilted +20°
        for (int i = 0; i < 10_000; i++) t.Update(x, y);
        Assert.Equal(20.0, t.AngleDegrees, 3);
    }

    [Fact]
    public void DoesNotDrift_AfterMotionSettles()
    {
        // Move from 0° → 30° via many intermediate samples (simulating Madgwick's
        // slow gravity estimate settling), then HOLD at 30° for thousands of ticks.
        // The final reading must be exactly 30°, regardless of how the angle got
        // there.
        var t = new GravityTiltAxis();
        t.Update(0, 1);
        for (int deg = 1; deg <= 30; deg++) {
            var (x, y) = GravityAt(deg);
            t.Update(x, y);
        }
        var (xh, yh) = GravityAt(30);
        for (int i = 0; i < 10_000; i++) t.Update(xh, yh);
        Assert.Equal(30.0, t.AngleDegrees, 3);
    }

    [Fact]
    public void Reset_ClearsNeutral_NextSampleIsZero()
    {
        var t = new GravityTiltAxis();
        t.Update(0, 1);
        var (x, y) = GravityAt(90);
        t.Update(x, y);
        Assert.Equal(90.0, t.AngleDegrees, 1);

        t.Reset();
        t.Update(x, y);
        Assert.Equal(0.0, t.AngleDegrees, 1);
    }
}
