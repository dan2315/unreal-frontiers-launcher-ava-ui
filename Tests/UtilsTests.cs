using System;
using System.Diagnostics;
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

public class GameEnvironmentTests
{
    private static Process NewProcess(string fileName = "cmd.exe", string arguments = "")
    {
        return new Process
        {
            StartInfo = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            }
        };
    }

    [Fact]
    public void ApplyGameEnvironment_SetsTokenAndBackendUrl()
    {
        var process = NewProcess();

        GameLauncher.ApplyGameEnvironment(process, "access-token");

        Assert.Equal("access-token", process.StartInfo.EnvironmentVariables["UF_ACCESS_TOKEN"]);
        Assert.Equal(AppStaticData.ServerURL, process.StartInfo.EnvironmentVariables["UF_BACKEND_URL"]);
    }

    [Fact]
    public void ApplyGameEnvironment_RemovesInheritedTokenWhenLoggedOut()
    {
        var process = NewProcess();
        process.StartInfo.EnvironmentVariables["UF_ACCESS_TOKEN"] = "stale";

        GameLauncher.ApplyGameEnvironment(process, null);

        Assert.False(process.StartInfo.EnvironmentVariables.ContainsKey("UF_ACCESS_TOKEN"));
    }

    [Fact]
    public void ApplyGameEnvironment_KeepsTokenOutOfCommandLine()
    {
        var process = NewProcess("java", "-Xmx4G -jar game.jar");

        GameLauncher.ApplyGameEnvironment(process, "secret-token");

        Assert.DoesNotContain("secret-token", process.StartInfo.Arguments);
    }

    [Fact]
    public void ApplyGameEnvironment_ReachesChildProcess()
    {
        if (!OperatingSystem.IsWindows()) return;

        var process = NewProcess("cmd.exe", "/c echo %UF_ACCESS_TOKEN%");
        GameLauncher.ApplyGameEnvironment(process, "token-from-launcher");

        process.Start();
        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();

        Assert.Equal("token-from-launcher", output);
    }
}

public class ModLoaderTests
{
    [Theory]
    [InlineData("1.21.1", "21.1.252", ModLoader.NeoForge)]
    [InlineData("1.21", "21.0.167", ModLoader.NeoForge)]
    [InlineData("1.20.4", "20.4.237", ModLoader.NeoForge)]
    [InlineData("1.20.1", "47.4.0", ModLoader.Forge)]
    [InlineData("1.21.1", "52.0.40", ModLoader.Forge)]
    [InlineData("1.18.2", "40.2.21", ModLoader.Forge)]
    [InlineData("1.21.1", "", ModLoader.Forge)]
    public void Resolve_InfersLoaderFromVersionFormat(string game, string loader, string expected)
    {
        Assert.Equal(expected, ModLoader.Resolve(null, game, loader));
    }

    [Theory]
    [InlineData("neoforge", "1.20.1", "47.4.0", ModLoader.NeoForge)]
    [InlineData("NeoForge", "1.20.1", "47.4.0", ModLoader.NeoForge)]
    [InlineData("neo_forge", "1.20.1", "47.4.0", ModLoader.NeoForge)]
    [InlineData("forge", "1.21.1", "21.1.252", ModLoader.Forge)]
    public void Resolve_PrefersTheExplicitLoader(string explicitLoader, string game, string loader, string expected)
    {
        Assert.Equal(expected, ModLoader.Resolve(explicitLoader, game, loader));
    }

    [Fact]
    public void Instance_RoundTripsLoaderThroughInstanceJson()
    {
        var instance = new MinecraftInstance("zaruba", "Unreal Zaruba", "1.21.1", "21.1.252", "unrealfrontiers.com:25570", 4096);
        Assert.Equal(ModLoader.NeoForge, instance.Loader);

        var json = System.Text.Json.JsonSerializer.Serialize(instance);
        var restored = System.Text.Json.JsonSerializer.Deserialize<MinecraftInstance>(json)!;

        Assert.Equal(ModLoader.NeoForge, restored.Loader);
        Assert.Equal("21.1.252", restored.ForgeVersion);
        Assert.Equal("unrealfrontiers.com:25570", restored.Address);
    }

    [Fact]
    public void Instance_FromOldInstanceJsonWithoutLoaderStillResolves()
    {
        const string oldJson = """{"Id":"create","Name":"Create","GameVersion":"1.20.1","ForgeVersion":"47.4.0","ServerIp":"example.com","ServerPort":"25565"}""";

        var restored = System.Text.Json.JsonSerializer.Deserialize<MinecraftInstance>(oldJson)!;

        Assert.Equal(ModLoader.Forge, restored.Loader);
    }
}

public class CrashLogTests : IDisposable
{
    private readonly string _previousDirectory = CrashLog.LogDirectory;
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "uf-crashlog-" + Guid.NewGuid().ToString("N"));

    public CrashLogTests()
    {
        CrashLog.LogDirectory = _directory;
    }

    public void Dispose()
    {
        CrashLog.LogDirectory = _previousDirectory;
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    [Fact]
    public void Write_AppendsTheFullExceptionToTheLogFile()
    {
        CrashLog.Write("First", new InvalidOperationException("boom"));
        CrashLog.Write("Second", new NullReferenceException());

        var log = File.ReadAllText(CrashLog.LogFilePath);
        Assert.Contains("First", log);
        Assert.Contains("boom", log);
        Assert.Contains("Second", log);
        Assert.Contains("NullReferenceException", log);
    }
}
