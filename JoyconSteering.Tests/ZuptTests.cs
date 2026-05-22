using JoyconSteering.JoyCon;
using JoyconSteering.JoyCon.Fusion;
using Xunit;

namespace JoyconSteering.Tests;

public class ZuptTests
{
    private const double Dt = 0.005;

    [Fact]
    public void StationaryWithGyroNoise_BelowThreshold_DoesNotAccumulate()
    {
        // Simulate small residual bias under the ZUPT threshold (default 2 dps).
        // Over many seconds this would normally drift; ZUPT should prevent it.
        var f = new MadgwickFilter(beta: 0.05, zuptThresholdDps: 2.0);
        for (int i = 0; i < 2000; i++) // 10 seconds
        {
            f.Update(new ImuSample(0, 0, 1, GxDps: 0.4, GyDps: 0.3, GzDps: 0.5), Dt);
        }
        var (roll, pitch, yaw) = f.GetEulerDegrees();
        Assert.Equal(0.0, yaw, 1); // should be ~0, not 5 dps × 10 s = 50°
        Assert.Equal(0.0, roll, 1);
        Assert.Equal(0.0, pitch, 1);
    }

    [Fact]
    public void GyroAboveThreshold_IntegratesNormally()
    {
        var f = new MadgwickFilter(beta: 0.05, zuptThresholdDps: 2.0);
        for (int i = 0; i < 200; i++) // 1 s at 60 dps → 60° yaw
        {
            f.Update(new ImuSample(0, 0, 0, GxDps: 0, GyDps: 0, GzDps: 60), Dt);
        }
        var (_, _, yaw) = f.GetEulerDegrees();
        Assert.InRange(Math.Abs(yaw), 55, 65);
    }

    [Fact]
    public void ZuptDisabled_AllowsTinyGyroToIntegrate()
    {
        var f = new MadgwickFilter(beta: 0.05, zuptThresholdDps: 0.0);
        for (int i = 0; i < 2000; i++) // 10 s at 0.5 dps → 5° yaw with no ZUPT
        {
            f.Update(new ImuSample(0, 0, 0, GxDps: 0, GyDps: 0, GzDps: 0.5), Dt);
        }
        var (_, _, yaw) = f.GetEulerDegrees();
        Assert.InRange(Math.Abs(yaw), 4, 6);
    }

    [Fact]
    public void Threshold_IsMagnitude_Not_PerAxis()
    {
        // |(1.5, 1.5, 0)| = 2.12, which is just above 2.0 threshold → should integrate.
        var f = new MadgwickFilter(beta: 0.05, zuptThresholdDps: 2.0);
        for (int i = 0; i < 200; i++) // 1 s
        {
            f.Update(new ImuSample(0, 0, 0, GxDps: 1.5, GyDps: 0, GzDps: 1.5), Dt);
        }
        // Total accumulated magnitude ≈ 2.12 dps × 1 s = ~2.12° spread across axes
        var (roll, _, yaw) = f.GetEulerDegrees();
        var total = Math.Sqrt(roll * roll + yaw * yaw);
        Assert.InRange(total, 1.0, 3.0);
    }
}
