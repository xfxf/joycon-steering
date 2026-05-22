using JoyconSteering.JoyCon;
using Xunit;

namespace JoyconSteering.Tests;

public class JoyConBatteryTests
{
    [Theory]
    [InlineData(8, 100)]
    [InlineData(6, 75)]
    [InlineData(4, 50)]
    [InlineData(2, 25)]
    [InlineData(0, 0)]
    public void Percent_DecodesDiscreteLevels(int nibble, int expectedPct)
    {
        Assert.Equal(expectedPct, JoyConBattery.Percent(nibble));
    }

    [Theory]
    [InlineData(9, 100)] // full + charging
    [InlineData(7, 75)]
    [InlineData(5, 50)]
    [InlineData(3, 25)]
    [InlineData(1, 0)]
    public void Percent_IgnoresChargingBit(int nibble, int expectedPct)
    {
        Assert.Equal(expectedPct, JoyConBattery.Percent(nibble));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(8, false)]
    [InlineData(9, true)]
    public void IsCharging_LowBitOfNibble(int nibble, bool expected)
    {
        Assert.Equal(expected, JoyConBattery.IsCharging(nibble));
    }
}
