using UnrealFrontiersInstaller;

public class InstallPathTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "uf-installpath-" + Guid.NewGuid().ToString("N"));

    public InstallPathTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, true);

    private string Make(string name) => Directory.CreateDirectory(Path.Combine(_root, name)).FullName;

    [Fact]
    public void ANewFolderIsUsedAsIs()
    {
        var target = Path.Combine(_root, "Games", "UF");

        Assert.Equal(target, InstallPath.Resolve(target, "UnrealFrontiersLauncher"));
    }

    [Fact]
    public void AnEmptyFolderIsUsedAsIs()
    {
        var folder = Make("empty");

        Assert.Equal(folder, InstallPath.Resolve(folder, "UnrealFrontiersLauncher"));
    }

    [Fact]
    public void AFolderWithOtherFilesGetsASubfolderSoUninstallCannotWipeThem()
    {
        var folder = Make("Games");
        File.WriteAllText(Path.Combine(folder, "save.dat"), "x");

        Assert.Equal(Path.Combine(folder, "UnrealFrontiersLauncher"), InstallPath.Resolve(folder, "UnrealFrontiersLauncher"));
    }

    [Fact]
    public void AnExistingInstallIsReused()
    {
        var folder = Make("install");
        File.WriteAllText(Path.Combine(folder, "Update.exe"), "x");
        Directory.CreateDirectory(Path.Combine(folder, "current"));

        Assert.Equal(folder, InstallPath.Resolve(folder, "UnrealFrontiersLauncher"));
    }

    [Fact]
    public void AnUpdateExeAloneIsNotAnInstall()
    {
        var folder = Make("fake");
        File.WriteAllText(Path.Combine(folder, "Update.exe"), "x");

        Assert.Equal(Path.Combine(folder, "UnrealFrontiersLauncher"), InstallPath.Resolve(folder, "UnrealFrontiersLauncher"));
    }

    [Fact]
    public void QuotesAndSpacesAroundThePathAreIgnored()
    {
        var folder = Path.Combine(_root, "quoted");

        Assert.Equal(folder, InstallPath.Resolve($"  \"{folder}\" ", "UnrealFrontiersLauncher"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyPathIsRejected(string path)
    {
        Assert.Throws<ArgumentException>(() => InstallPath.Resolve(path, "UnrealFrontiersLauncher"));
    }

    [Fact]
    public void ADriveRootGetsASubfolder()
    {
        if (!OperatingSystem.IsWindows()) return;

        Assert.Equal(@"C:\UnrealFrontiersLauncher", InstallPath.Resolve(@"C:\", "UnrealFrontiersLauncher"));
    }
}
