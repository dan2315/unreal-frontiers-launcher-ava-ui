using System;
using System.IO;
using UnrealFrontiersLauncher;

public class InstallLocationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "uf-install-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private string Make(params string[] files)
    {
        foreach (var file in files)
        {
            var path = Path.Combine(_dir, file);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, file);
        }
        return _dir;
    }

    [Fact]
    public void MoveEntries_MovesInstancesAndInstallAndLeavesTheRest()
    {
        var from = Path.Combine(Make("old/Instances/Create/instance.json", "old/Install/libraries/a.jar", "old/preferences.json"), "old");
        var to = Path.Combine(_dir, "new");

        Assert.Null(InstallLocation.MoveEntries(from, to));

        Assert.True(File.Exists(Path.Combine(to, "Instances", "Create", "instance.json")));
        Assert.True(File.Exists(Path.Combine(to, "Install", "libraries", "a.jar")));
        Assert.False(Directory.Exists(Path.Combine(from, "Instances")));
        Assert.True(File.Exists(Path.Combine(from, "preferences.json")));
    }

    [Fact]
    public void MoveEntries_RefusesANonEmptyTargetAndTouchesNothing()
    {
        var from = Path.Combine(Make("old/Instances/Create/instance.json", "new/Instances/Other/instance.json"), "old");
        var to = Path.Combine(_dir, "new");

        var error = InstallLocation.MoveEntries(from, to);

        Assert.NotNull(error);
        Assert.True(File.Exists(Path.Combine(from, "Instances", "Create", "instance.json")));
        Assert.True(File.Exists(Path.Combine(to, "Instances", "Other", "instance.json")));
    }

    [Fact]
    public void MoveEntries_AnEmptyTargetFolderIsFine()
    {
        var from = Path.Combine(Make("old/Instances/Create/instance.json"), "old");
        var to = Path.Combine(_dir, "new");
        Directory.CreateDirectory(Path.Combine(to, "Instances"));

        Assert.Null(InstallLocation.MoveEntries(from, to));
        Assert.True(File.Exists(Path.Combine(to, "Instances", "Create", "instance.json")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("relative/folder")]
    public void Validate_NeedsAFullPath(string path)
    {
        Assert.NotNull(InstallLocation.Validate(path));
    }
}
