using JoyconSteering.JoyCon;
using JoyconSteering.JoyCon.Fusion;
using Xunit;

namespace JoyconSteering.Tests;

public class ContinuousBiasTests
{
    private static ImuSample Gyro(double gx, double gy, double gz)
        => new(0, 0, 1, gx, gy, gz);

    [Fact]
    public void UpdateRunning_DoesNothingBeforeInitialCalCompletes()
    {
        var c = new GyroBiasCalibrator(sampleCount: 10);
        c.UpdateRunning(Gyro(5, 5, 5));
        Assert.Equal(0.0, c.BiasXDps);
    }

    [Fact]
    public void UpdateRunning_NudgesBiasTowardStationarySample()
    {
        var c = new GyroBiasCalibrator(sampleCount: 10);
        for (int i = 0; i < 10; i++) c.Apply(Gyro(0, 0, 0)); // initial cal yields bias 0
        Assert.True(c.IsCalibrated);
        Assert.Equal(0.0, c.BiasZDps);

        // 200 stationary samples reading 1.0 dps → bias should creep toward 1.0
        for (int i = 0; i < 200; i++) c.UpdateRunning(Gyro(0, 0, 1.0), alpha: 0.01);
        Assert.InRange(c.BiasZDps, 0.5, 1.0);
    }

    [Fact]
    public void UpdateRunning_Convergence_OnEnoughSamples()
    {
        var c = new GyroBiasCalibrator(sampleCount: 10);
        for (int i = 0; i < 10; i++) c.Apply(Gyro(0, 0, 0));
        for (int i = 0; i < 5000; i++) c.UpdateRunning(Gyro(0, 0, 0.5), alpha: 0.01);
        Assert.Equal(0.5, c.BiasZDps, 2);
    }

    [Fact]
    public void Apply_SubtractsContinuouslyUpdatedBias()
    {
        var c = new GyroBiasCalibrator(sampleCount: 10);
        for (int i = 0; i < 10; i++) c.Apply(Gyro(0, 0, 0));
        // Drift the bias to 1.0
        for (int i = 0; i < 5000; i++) c.UpdateRunning(Gyro(0, 0, 1.0), alpha: 0.01);
        // A reading of 1.0 should now be reported as ~0 (bias subtracted)
        var result = c.Apply(Gyro(0, 0, 1.0));
        Assert.Equal(0.0, result.GzDps, 1);
    }
}
