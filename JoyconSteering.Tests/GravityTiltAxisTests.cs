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
    public void Unwrap_RotationPast180_DoesNotFlip()
    {
        // Rotate slowly through +200° via several intermediate samples. The angle
        // should grow monotonically and end ~+200°, NOT flip to -160°.
        var t = new GravityTiltAxis();
        t.Update(0, 1); // neutral at 0°
        for (int deg = 10; deg <= 200; deg += 10)
        {
            var (x, y) = GravityAt(deg);
            t.Update(x, y);
        }
        Assert.InRange(t.AngleDegrees, 195, 205);
    }

    [Fact]
    public void Unwrap_RotationPast_Minus180_DoesNotFlip()
    {
        var t = new GravityTiltAxis();
        t.Update(0, 1);
        for (int deg = -10; deg >= -240; deg -= 10)
        {
            var (x, y) = GravityAt(deg);
            t.Update(x, y);
        }
        Assert.InRange(t.AngleDegrees, -245, -235);
    }

    [Fact]
    public void Unwrap_FullRevolution_AccumulatesTo360()
    {
        var t = new GravityTiltAxis();
        t.Update(0, 1);
        for (int deg = 5; deg <= 360; deg += 5)
        {
            var (x, y) = GravityAt(deg);
            t.Update(x, y);
        }
        // After one full revolution, accumulated angle is +360° (or close to it).
        Assert.InRange(t.AngleDegrees, 355, 365);
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
