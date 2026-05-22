using JoyconSteering.Config;
using Xunit;

namespace JoyconSteering.Tests;

public class IniReaderTests
{
    private static string WriteTempIni(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"joycon-test-{Guid.NewGuid():N}.ini");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void GetString_ReturnsValue()
    {
        var path = WriteTempIni("[s]\nkey = hello\n");
        var ini = IniReader.Load(path);
        Assert.Equal("hello", ini.GetString("s", "key", "fallback"));
    }

    [Fact]
    public void GetString_Fallback_WhenMissing()
    {
        var path = WriteTempIni("[s]\nkey = hello\n");
        var ini = IniReader.Load(path);
        Assert.Equal("fallback", ini.GetString("s", "other", "fallback"));
    }

    [Fact]
    public void GetInt_ParsesNumber()
    {
        var path = WriteTempIni("[s]\nn = 42\n");
        var ini = IniReader.Load(path);
        Assert.Equal(42, ini.GetInt("s", "n", 0));
    }

    [Fact]
    public void GetDouble_ParsesWithInvariantCulture()
    {
        var path = WriteTempIni("[s]\nd = 1.5\n");
        var ini = IniReader.Load(path);
        Assert.Equal(1.5, ini.GetDouble("s", "d", 0));
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("yes", true)]
    [InlineData("on", true)]
    [InlineData("1", true)]
    [InlineData("false", false)]
    [InlineData("no", false)]
    [InlineData("off", false)]
    [InlineData("0", false)]
    public void GetBool_ParsesCommonForms(string value, bool expected)
    {
        var path = WriteTempIni($"[s]\nb = {value}\n");
        var ini = IniReader.Load(path);
        Assert.Equal(expected, ini.GetBool("s", "b", !expected));
    }

    [Fact]
    public void Comments_AreStripped_FromValue()
    {
        var path = WriteTempIni("[s]\nkey = hello ; trailing comment\n");
        var ini = IniReader.Load(path);
        Assert.Equal("hello", ini.GetString("s", "key", ""));
    }

    [Fact]
    public void SemicolonLines_AreIgnored()
    {
        var path = WriteTempIni("; comment\n[s]\n; another\nkey = v\n");
        var ini = IniReader.Load(path);
        Assert.Equal("v", ini.GetString("s", "key", ""));
    }

    [Fact]
    public void SectionAndKey_AreCaseInsensitive()
    {
        var path = WriteTempIni("[Section]\nKey = X\n");
        var ini = IniReader.Load(path);
        Assert.Equal("X", ini.GetString("SECTION", "KEY", ""));
        Assert.Equal("X", ini.GetString("section", "key", ""));
    }
}
