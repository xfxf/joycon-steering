using JoyconSteering.Steering;
using Xunit;

namespace JoyconSteering.Tests;

public class AngleSourceTests
{
    [Theory]
    [InlineData(SelectedAxis.Roll, 10, 20, 30, 40, 10)]
    [InlineData(SelectedAxis.Pitch, 10, 20, 30, 40, 20)]
    [InlineData(SelectedAxis.Yaw, 10, 20, 30, 40, 30)]
    [InlineData(SelectedAxis.Wheel, 10, 20, 30, 40, 40)]
    public void Pick_ReturnsConfiguredAxis(SelectedAxis axis, double r, double p, double y, double w, double expected)
    {
        Assert.Equal(expected, AngleSource.Pick(axis, r, p, y, w));
    }
}

public class RisingEdgeDetectorTests
{
    [Fact]
    public void NoChange_ReturnsFalse()
    {
        var det = new RisingEdgeDetector();
        Assert.False(det.Update(false));
        Assert.False(det.Update(false));
    }

    [Fact]
    public void FalseToTrue_RisingEdge()
    {
        var det = new RisingEdgeDetector();
        Assert.False(det.Update(false));
        Assert.True(det.Update(true));
        Assert.False(det.Update(true));  // held — no edge
    }

    [Fact]
    public void TrueToFalse_NoFalseRisingEdge()
    {
        var det = new RisingEdgeDetector();
        det.Update(true);
        Assert.False(det.Update(false));
    }
}
