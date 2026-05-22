using JoyconSteering.JoyCon;
using JoyconSteering.JoyCon.Fusion;
using Xunit;

namespace JoyconSteering.Tests;

public class MadgwickFilterTests
{
    // Use 60 dps for 1s = 60° per test. Stays safely under ±90° pitch gimbal-lock limit.
    private const double GyroRate = 60.0;
    private const double Dt = 0.005; // 5 ms = Joy-Con IMU period
    private const int OneSecondSteps = 200;

    [Fact]
    public void Initial_OrientationIsIdentity()
    {
        var f = new MadgwickFilter(beta: 0.05);
        var (roll, pitch, yaw) = f.GetEulerDegrees();
        Assert.Equal(0.0, roll, 3);
        Assert.Equal(0.0, pitch, 3);
        Assert.Equal(0.0, yaw, 3);
    }

    [Fact]
    public void StationaryWithGravity_ConvergesToLevel()
    {
        var f = new MadgwickFilter(beta: 0.1);
        // Start with a slightly off orientation by applying a brief gyro impulse, then stop.
        f.Update(new ImuSample(0, 0, 1, GxDps: 50, GyDps: 0, GzDps: 0), Dt);
        // Now feed many stationary "1g down on Z" samples; filter should pull roll/pitch toward 0.
        for (int i = 0; i < 2000; i++)
            f.Update(new ImuSample(0, 0, 1, 0, 0, 0), Dt);
        var (roll, pitch, _) = f.GetEulerDegrees();
        Assert.Equal(0.0, roll, 1);   // within 0.1 degree
        Assert.Equal(0.0, pitch, 1);
    }

    [Fact]
    public void GyroOnly_IntegratesAngle_AboutX()
    {
        // Pure gyro X rotation, no accel — filter should integrate as pure gyro.
        var f = new MadgwickFilter(beta: 0.05);
        // Rotate at 100 dps for 1 second → expect ~100° about one axis.
        for (int i = 0; i < OneSecondSteps; i++) // 200 * 5ms = 1s
            f.Update(new ImuSample(0, 0, 0, GxDps: GyroRate, GyDps: 0, GzDps: 0), Dt);
        var (roll, pitch, yaw) = f.GetEulerDegrees();
        // Total rotation magnitude should be ~100°. One of the axes carries the rotation.
        double total = Math.Sqrt(roll * roll + pitch * pitch + yaw * yaw);
        Assert.InRange(total, 55.0, 65.0);
    }

    [Fact]
    public void GyroOnly_IntegratesAngle_AboutZ_AsYaw()
    {
        var f = new MadgwickFilter(beta: 0.05);
        for (int i = 0; i < OneSecondSteps; i++)
            f.Update(new ImuSample(0, 0, 0, 0, 0, GzDps: GyroRate), Dt);
        var (_, _, yaw) = f.GetEulerDegrees();
        Assert.InRange(Math.Abs(yaw), 55.0, 65.0);
    }

    [Fact]
    public void GyroOnly_IntegratesAngle_AboutY_AsPitch()
    {
        var f = new MadgwickFilter(beta: 0.05);
        for (int i = 0; i < OneSecondSteps; i++)
            f.Update(new ImuSample(0, 0, 0, 0, GyDps: GyroRate, 0), Dt);
        var (_, pitch, _) = f.GetEulerDegrees();
        Assert.InRange(Math.Abs(pitch), 55.0, 65.0);
    }

    [Fact]
    public void GyroOnly_IntegratesAngle_AboutX_AsRoll()
    {
        var f = new MadgwickFilter(beta: 0.05);
        for (int i = 0; i < OneSecondSteps; i++)
            f.Update(new ImuSample(0, 0, 0, GxDps: GyroRate, 0, 0), Dt);
        var (roll, _, _) = f.GetEulerDegrees();
        Assert.InRange(Math.Abs(roll), 55.0, 65.0);
    }
}
