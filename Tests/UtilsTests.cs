using System;
using System.IO;
using UnrealFrontiersLauncher;

public class PathSafetyTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "uf-root");

    [Theory]
    [InlineData("mods/a.jar")]
    [InlineData("config/sub/../b.toml")]
    public void ResolveInside_AllowsPathsUnderRoot(string relative)
    {
        Assert.StartsWith(Path.GetFullPath(Root), PathSafety.ResolveInside(Root, relative));
    }

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("mods/../../evil.txt")]
    [InlineData("/etc/passwd")]
    public void ResolveInside_RejectsEscapingPaths(string relative)
    {
        Assert.Throws<InvalidOperationException>(() => PathSafety.ResolveInside(Root, relative));
    }

    [Fact]
    public void ResolveInside_RejectsSiblingWithSamePrefix()
    {
        Assert.Throws<InvalidOperationException>(() => PathSafety.ResolveInside(Root, "../uf-root-evil/a"));
    }

    [Theory]
    [InlineData("Unreal Domains: Create", "Unreal Domains_ Create")]
    [InlineData("Monifactory", "Monifactory")]
    [InlineData("...", "_")]
    public void SanitizeFileName_ReplacesInvalidCharacters(string input, string expected)
    {
        Assert.Equal(expected, PathSafety.SanitizeFileName(input));
    }
}

public class AutoupdaterTests
{
    [Theory]
    [InlineData("1.3.5", true)]
    [InlineData("v1.4.0", true)]
    [InlineData("1.3.5-beta", true)]
    [InlineData("1.3.4", false)]
    [InlineData("1.2.0", false)]
    [InlineData("garbage", false)]
    [InlineData("", false)]
    public void IsNewVersionAvailable_ComparesAgainstCurrent(string latest, bool expected)
    {
        Assert.Equal(expected, Autoupdater.IsNewVersionAvailable(new Version(1, 3, 4), latest));
    }
}

public class MinecraftInstanceTests
{
    [Theory]
    [InlineData("play.example.com:25566", "play.example.com", "25566")]
    [InlineData("play.example.com", "play.example.com", "")]
    [InlineData("", "", "")]
    public void SplitAddress_HandlesOptionalPort(string address, string ip, string port)
    {
        Assert.Equal((ip, port), MinecraftInstance.SplitAddress(address));
    }

    [Fact]
    public void Address_OmitsPortWhenMissing()
    {
        var instance = new MinecraftInstance("id", "name", "1.20.1", "47.4.0", "play.example.com", 4096);
        Assert.Equal("play.example.com", instance.Address);
    }
}
