using JoyconSteering.JoyCon;
using JoyconSteering.Tests.TestHelpers;
using Xunit;

namespace JoyconSteering.Tests;

public class InputReportParserTests
{
    [Fact]
    public void IsStandardReport_TrueFor_0x30()
    {
        var buf = ReportBuilder.EmptyStandard();
        Assert.True(InputReportParser.IsStandardReport(buf));
    }

    [Fact]
    public void IsStandardReport_FalseFor_OtherIds()
    {
        var buf = new byte[64];
        buf[0] = 0x21;
        Assert.False(InputReportParser.IsStandardReport(buf));
    }

    [Fact]
    public void ParseBattery_ReadsUpperNibbleOfByte2()
    {
        var buf = ReportBuilder.WithBattery(ReportBuilder.EmptyStandard(), 8);
        Assert.Equal(8, InputReportParser.ParseBattery(buf));
    }

    [Theory]
    [InlineData(0x01, 0x00, LeftJoyConButton.Down)]
    [InlineData(0x02, 0x00, LeftJoyConButton.Up)]
    [InlineData(0x04, 0x00, LeftJoyConButton.Right)]
    [InlineData(0x08, 0x00, LeftJoyConButton.Left)]
    [InlineData(0x10, 0x00, LeftJoyConButton.SrL)]
    [InlineData(0x20, 0x00, LeftJoyConButton.SlL)]
    [InlineData(0x40, 0x00, LeftJoyConButton.L)]
    [InlineData(0x80, 0x00, LeftJoyConButton.Zl)]
    [InlineData(0x00, 0x01, LeftJoyConButton.Minus)]
    [InlineData(0x00, 0x08, LeftJoyConButton.Stick)]
    [InlineData(0x00, 0x20, LeftJoyConButton.Capture)]
    public void ParseLeftButtons_SingleBits(byte sideMask, byte sharedMask, LeftJoyConButton expected)
    {
        var buf = ReportBuilder.WithLeftButton(ReportBuilder.EmptyStandard(), sideMask, sharedMask);
        Assert.Equal(expected, InputReportParser.ParseLeftButtons(buf));
    }

    [Fact]
    public void ParseLeftButtons_CombinesMultipleBits()
    {
        var buf = ReportBuilder.WithLeftButton(ReportBuilder.EmptyStandard(), 0x40 /*L*/ | 0x80 /*ZL*/, 0x08 /*Stick*/);
        var got = InputReportParser.ParseLeftButtons(buf);
        Assert.True(got.HasFlag(LeftJoyConButton.L));
        Assert.True(got.HasFlag(LeftJoyConButton.Zl));
        Assert.True(got.HasFlag(LeftJoyConButton.Stick));
    }

    [Fact]
    public void ParseStick_CenterRaw_ReturnsZero()
    {
        var buf = ReportBuilder.WithLeftStick(ReportBuilder.EmptyStandard(),
            InputReportParser.StickCenterNom, InputReportParser.StickCenterNom);
        var (x, y) = InputReportParser.ParseStick(buf, isLeft: true);
        Assert.Equal(0.0, x, 3);
        Assert.Equal(0.0, y, 3);
    }

    [Fact]
    public void ParseStick_FullRangePositive_ReturnsOne()
    {
        var buf = ReportBuilder.WithLeftStick(ReportBuilder.EmptyStandard(),
            InputReportParser.StickCenterNom + InputReportParser.StickRangeNom,
            InputReportParser.StickCenterNom + InputReportParser.StickRangeNom);
        var (x, y) = InputReportParser.ParseStick(buf, isLeft: true);
        Assert.Equal(1.0, x, 3);
        Assert.Equal(1.0, y, 3);
    }

    [Fact]
    public void ParseStick_FullRangeNegative_ReturnsMinusOne()
    {
        var buf = ReportBuilder.WithLeftStick(ReportBuilder.EmptyStandard(),
            InputReportParser.StickCenterNom - InputReportParser.StickRangeNom,
            InputReportParser.StickCenterNom - InputReportParser.StickRangeNom);
        var (x, y) = InputReportParser.ParseStick(buf, isLeft: true);
        Assert.Equal(-1.0, x, 3);
        Assert.Equal(-1.0, y, 3);
    }

    [Fact]
    public void ParseStick_BeyondRange_IsClamped()
    {
        // Raw values that, before clamping, would yield > 1 / < -1.
        var buf = ReportBuilder.WithLeftStick(ReportBuilder.EmptyStandard(), 0xFFF, 0x000);
        var (x, y) = InputReportParser.ParseStick(buf, isLeft: true);
        Assert.InRange(x, -1.0, 1.0);
        Assert.InRange(y, -1.0, 1.0);
    }

    [Fact]
    public void ParseImu_ZeroSample_Zeroes()
    {
        var buf = ReportBuilder.WithImuSample(ReportBuilder.EmptyStandard(), 0, 0, 0, 0, 0, 0, 0);
        var s = InputReportParser.ParseImu(buf, 0);
        Assert.Equal(0, s.AxG);
        Assert.Equal(0, s.GxDps);
    }

    [Fact]
    public void ParseImu_Accel1g_Equals1gOutput()
    {
        // 4096 LSB ≈ 1g per the FSR
        var buf = ReportBuilder.WithImuSample(ReportBuilder.EmptyStandard(), 0, ax: 0, ay: 0, az: 4096, gx: 0, gy: 0, gz: 0);
        var s = InputReportParser.ParseImu(buf, 0);
        Assert.Equal(1.0, s.AzG, 3);
    }

    [Fact]
    public void ParseImu_Gyro_ScalesToDps()
    {
        // 16384 LSB * 0.06103 dps/LSB ≈ 1000 dps
        var buf = ReportBuilder.WithImuSample(ReportBuilder.EmptyStandard(), 0, 0, 0, 0, gx: 16384, gy: 0, gz: 0);
        var s = InputReportParser.ParseImu(buf, 0);
        Assert.Equal(1000.0, s.GxDps, 0); // within ~1 dps
    }

    [Fact]
    public void ParseImu_NegativeSignExtension()
    {
        var buf = ReportBuilder.WithImuSample(ReportBuilder.EmptyStandard(), 0, 0, 0, -4096, 0, 0, 0);
        var s = InputReportParser.ParseImu(buf, 0);
        Assert.Equal(-1.0, s.AzG, 3);
    }

    [Fact]
    public void ParseImu_PicksCorrectSampleByIndex()
    {
        var buf = ReportBuilder.EmptyStandard();
        ReportBuilder.WithImuSample(buf, 0, 0, 0, 1000, 0, 0, 0);
        ReportBuilder.WithImuSample(buf, 1, 0, 0, 2000, 0, 0, 0);
        ReportBuilder.WithImuSample(buf, 2, 0, 0, 3000, 0, 0, 0);
        Assert.Equal(1000 * InputReportParser.AccelToG, InputReportParser.ParseImu(buf, 0).AzG, 4);
        Assert.Equal(2000 * InputReportParser.AccelToG, InputReportParser.ParseImu(buf, 1).AzG, 4);
        Assert.Equal(3000 * InputReportParser.AccelToG, InputReportParser.ParseImu(buf, 2).AzG, 4);
    }

    [Fact]
    public void ParseStandard_RejectsWrongReportId()
    {
        var buf = new byte[64];
        buf[0] = 0x21;
        Assert.Throws<ArgumentException>(() => InputReportParser.ParseStandard(buf, isLeft: true));
    }

    [Fact]
    public void ParseStandard_RejectsShortBuffer()
    {
        var buf = new byte[20];
        buf[0] = 0x30;
        Assert.Throws<ArgumentException>(() => InputReportParser.ParseStandard(buf, isLeft: true));
    }
}
