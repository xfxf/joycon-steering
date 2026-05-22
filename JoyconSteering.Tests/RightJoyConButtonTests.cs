using JoyconSteering.JoyCon;
using JoyconSteering.Tests.TestHelpers;
using Xunit;

namespace JoyconSteering.Tests;

public class RightJoyConButtonTests
{
    private static byte[] WithRightSide(byte sideMask, byte sharedMask = 0)
    {
        var buf = ReportBuilder.EmptyStandard();
        buf[3] |= sideMask;
        buf[4] |= sharedMask;
        return buf;
    }

    [Theory]
    [InlineData(0x01, 0x00, RightJoyConButton.Y)]
    [InlineData(0x02, 0x00, RightJoyConButton.X)]
    [InlineData(0x04, 0x00, RightJoyConButton.B)]
    [InlineData(0x08, 0x00, RightJoyConButton.A)]
    [InlineData(0x10, 0x00, RightJoyConButton.SrR)]
    [InlineData(0x20, 0x00, RightJoyConButton.SlR)]
    [InlineData(0x40, 0x00, RightJoyConButton.R)]
    [InlineData(0x80, 0x00, RightJoyConButton.Zr)]
    [InlineData(0x00, 0x02, RightJoyConButton.Plus)]
    [InlineData(0x00, 0x04, RightJoyConButton.Stick)]
    [InlineData(0x00, 0x10, RightJoyConButton.Home)]
    public void ParseRightButtons_SingleBits(byte sideMask, byte sharedMask, RightJoyConButton expected)
    {
        Assert.Equal(expected, InputReportParser.ParseRightButtons(WithRightSide(sideMask, sharedMask)));
    }

    [Fact]
    public void ParseRightButtons_CombinesMultipleBits()
    {
        var buf = WithRightSide(0x80 /*ZR*/ | 0x40 /*R*/, 0x10 /*Home*/);
        var got = InputReportParser.ParseRightButtons(buf);
        Assert.True(got.HasFlag(RightJoyConButton.Zr));
        Assert.True(got.HasFlag(RightJoyConButton.R));
        Assert.True(got.HasFlag(RightJoyConButton.Home));
        Assert.False(got.HasFlag(RightJoyConButton.A));
    }

    [Fact]
    public void ParseRightButtons_NoBitsSet_Returns_None()
    {
        Assert.Equal(RightJoyConButton.None,
            InputReportParser.ParseRightButtons(ReportBuilder.EmptyStandard()));
    }

    [Theory]
    [InlineData("y", RightJoyConButton.Y)]
    [InlineData("x", RightJoyConButton.X)]
    [InlineData("b", RightJoyConButton.B)]
    [InlineData("a", RightJoyConButton.A)]
    [InlineData("r", RightJoyConButton.R)]
    [InlineData("zr", RightJoyConButton.Zr)]
    [InlineData("sl", RightJoyConButton.SlR)]
    [InlineData("sr", RightJoyConButton.SrR)]
    [InlineData("plus", RightJoyConButton.Plus)]
    [InlineData("stick", RightJoyConButton.Stick)]
    [InlineData("home", RightJoyConButton.Home)]
    [InlineData("garbage", RightJoyConButton.None)]
    public void RightButtonNames_FromName_RoundTrip(string name, RightJoyConButton expected)
    {
        Assert.Equal(expected, RightJoyConButtonNames.FromName(name));
    }
}

public class RightJoyConStateTests
{
    [Fact]
    public void ParseRightStandard_PicksRightSticks_AndRightButtons()
    {
        var buf = ReportBuilder.EmptyStandard();
        ReportBuilder.WithBattery(buf, 8);
        // Right stick offset = 9
        buf[9] = 0x00;
        buf[10] = 0x00;
        buf[11] = 0x08; // center-ish
        buf[3] |= 0x80; // ZR pressed
        for (int i = 0; i < 3; i++)
            ReportBuilder.WithImuSample(buf, i, 0, 0, 4096, 0, 0, 0); // 1g down on Z

        var s = InputReportParser.ParseRightStandard(buf);
        Assert.True(s.Buttons.HasFlag(RightJoyConButton.Zr));
        Assert.Equal(8, s.Battery);
        Assert.Equal(1.0, s.Sample0.AzG, 3);
    }
}
