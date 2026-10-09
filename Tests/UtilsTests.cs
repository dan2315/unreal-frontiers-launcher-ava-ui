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

public class LegacyDataMigrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "uf-migration-" + Guid.NewGuid().ToString("N"));
    private string Legacy => Path.Combine(_root, "legacy");
    private string Target => Path.Combine(_root, "target");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Fact]
    public void Migrate_MovesInstancesAndPreferences()
    {
        Directory.CreateDirectory(Path.Combine(Legacy, "Instances", "Create"));
        File.WriteAllText(Path.Combine(Legacy, "Instances", "Create", "launch.json"), "{}");
        File.WriteAllText(Path.Combine(Legacy, "preferences.json"), "{}");
        File.WriteAllText(Path.Combine(Legacy, "UnrealFrontiersLauncher.exe"), "binary");

        LegacyDataMigration.Migrate(Legacy, Target);

        Assert.True(File.Exists(Path.Combine(Target, "Instances", "Create", "launch.json")));
        Assert.True(File.Exists(Path.Combine(Target, "preferences.json")));
        Assert.False(Directory.Exists(Path.Combine(Legacy, "Instances")));
        Assert.True(File.Exists(Path.Combine(Legacy, "UnrealFrontiersLauncher.exe")));
    }

    [Fact]
    public void Migrate_DoesNotOverwriteExistingData()
    {
        Directory.CreateDirectory(Path.Combine(Legacy, "Instances"));
        File.WriteAllText(Path.Combine(Legacy, "preferences.json"), "old");
        Directory.CreateDirectory(Target);
        File.WriteAllText(Path.Combine(Target, "preferences.json"), "new");

        LegacyDataMigration.Migrate(Legacy, Target);

        Assert.Equal("new", File.ReadAllText(Path.Combine(Target, "preferences.json")));
        Assert.True(File.Exists(Path.Combine(Legacy, "preferences.json")));
    }

    [Fact]
    public void Migrate_IgnoresMissingLegacyRoot()
    {
        LegacyDataMigration.Migrate(Legacy, Target);
        Assert.False(Directory.Exists(Target));
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
