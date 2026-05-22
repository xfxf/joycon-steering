using JoyconSteering.Config;
using Xunit;

namespace JoyconSteering.Tests;

public class IniWriterTests
{
    private static string WriteTempIni(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"joycon-writer-{Guid.NewGuid():N}.ini");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void UpdatesExistingKey_PreservesComments_AndOtherKeys()
    {
        var path = WriteTempIni("""
            ; top-of-file comment
            [device]
            ; which side
            joycon_side = left
            vjoy_device_id = 1

            [steering]
            range_degrees = 180  ; trailing comment
            """);

        IniWriter.Update(path, new Dictionary<(string, string), string>
        {
            [("steering", "range_degrees")] = "270",
        });

        var content = File.ReadAllText(path);
        Assert.Contains("; top-of-file comment", content);
        Assert.Contains("; which side", content);
        Assert.Contains("joycon_side = left", content);
        Assert.Contains("range_degrees = 270", content);
        Assert.Contains("; trailing comment", content);
    }

    [Fact]
    public void AddsMissingKey_ToExistingSection()
    {
        var path = WriteTempIni("""
            [steering]
            range_degrees = 180
            """);

        IniWriter.Update(path, new Dictionary<(string, string), string>
        {
            [("steering", "invert")] = "true",
        });

        var content = File.ReadAllText(path);
        Assert.Contains("range_degrees = 180", content);
        Assert.Contains("invert = true", content);
    }

    [Fact]
    public void AddsMissingSection_WhenAbsent()
    {
        var path = WriteTempIni("""
            [device]
            joycon_side = left
            """);

        IniWriter.Update(path, new Dictionary<(string, string), string>
        {
            [("fusion", "madgwick_beta")] = "0.08",
        });

        var content = File.ReadAllText(path);
        Assert.Contains("[fusion]", content);
        Assert.Contains("madgwick_beta = 0.08", content);
    }

    [Fact]
    public void IsCaseInsensitive_ForSectionsAndKeys()
    {
        var path = WriteTempIni("[Steering]\nRange_Degrees = 180\n");

        IniWriter.Update(path, new Dictionary<(string, string), string>
        {
            [("STEERING", "range_degrees")] = "360",
        });

        var content = File.ReadAllText(path);
        Assert.Contains("Range_Degrees = 360", content);
    }

    [Fact]
    public void RoundTrip_WithIniReader_PreservesUpdatedValue()
    {
        var path = WriteTempIni("""
            [steering]
            range_degrees = 180
            invert = false
            """);

        IniWriter.Update(path, new Dictionary<(string, string), string>
        {
            [("steering", "range_degrees")] = "270",
            [("steering", "invert")] = "true",
        });

        var ini = IniReader.Load(path);
        Assert.Equal(270.0, ini.GetDouble("steering", "range_degrees", -1));
        Assert.True(ini.GetBool("steering", "invert", false));
    }

    [Fact]
    public void EmptyFile_GetsNewContent()
    {
        var path = WriteTempIni("");

        IniWriter.Update(path, new Dictionary<(string, string), string>
        {
            [("device", "vjoy_device_id")] = "3",
        });

        var ini = IniReader.Load(path);
        Assert.Equal(3, ini.GetInt("device", "vjoy_device_id", -1));
    }
}
