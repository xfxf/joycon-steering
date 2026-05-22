using JoyconSteering.JoyCon;
using JoyconSteering.JoyCon.Fusion;
using Xunit;

namespace JoyconSteering.Tests;

public class GyroStationaryTests
{
    private static ImuSample Gyro(double x, double y, double z) => new(0, 0, 1, x, y, z);

    [Fact]
    public void AllStill_BelowThreshold_IsStationary()
    {
        Assert.True(GyroStationary.IsStationary(
            Gyro(0.5, 0.3, 0.4), Gyro(0.2, 0.1, 0.3), Gyro(0.6, 0.0, 0.2), 1.5));
    }

    [Fact]
    public void OneNoisySample_AboveThreshold_NotStationary()
    {
        // Even one sample exceeding the threshold should disqualify the packet —
        // peak (not mean) so we don't refine bias during a transient.
        Assert.False(GyroStationary.IsStationary(
            Gyro(0.1, 0, 0), Gyro(10, 0, 0), Gyro(0.1, 0, 0), 1.5));
    }

    [Fact]
    public void AllMoving_NotStationary()
    {
        Assert.False(GyroStationary.IsStationary(
            Gyro(50, 0, 0), Gyro(50, 0, 0), Gyro(50, 0, 0), 1.5));
    }

    [Fact]
    public void Threshold_IsMagnitude_Not_PerAxis()
    {
        // |(1.0, 1.0, 0)| = √2 ≈ 1.414, just under threshold 1.5 → still.
        Assert.True(GyroStationary.IsStationary(
            Gyro(1.0, 1.0, 0), Gyro(0, 0, 0), Gyro(0, 0, 0), 1.5));
        // |(1.1, 1.1, 0)| ≈ 1.556, over → moving.
        Assert.False(GyroStationary.IsStationary(
            Gyro(1.1, 1.1, 0), Gyro(0, 0, 0), Gyro(0, 0, 0), 1.5));
    }
}
