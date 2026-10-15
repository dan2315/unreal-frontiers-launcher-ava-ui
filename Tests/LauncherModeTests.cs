using System;
using UnrealFrontiersLauncher;

public class LauncherModeResolverTests
{
    [Fact]
    public void Resolve_UsesTheDefaultWhenNothingIsSet()
    {
        Assert.Equal(LauncherMode.Production, LauncherModeResolver.Resolve(null, [], LauncherMode.Production));
        Assert.Equal(LauncherMode.Test, LauncherModeResolver.Resolve("", [], LauncherMode.Test));
    }

    [Theory]
    [InlineData("test", LauncherMode.Test)]
    [InlineData("TEST", LauncherMode.Test)]
    [InlineData(" Test ", LauncherMode.Test)]
    [InlineData("production", LauncherMode.Production)]
    [InlineData("prod", LauncherMode.Production)]
    public void Resolve_ReadsTheEnvironmentVariable(string value, LauncherMode expected)
    {
        var other = expected == LauncherMode.Test ? LauncherMode.Production : LauncherMode.Test;

        Assert.Equal(expected, LauncherModeResolver.Resolve(value, [], other));
    }

    [Fact]
    public void Resolve_PrefersCommandLineArgumentsOverTheEnvironment()
    {
        Assert.Equal(LauncherMode.Test, LauncherModeResolver.Resolve("production", ["app.exe", "--test"], LauncherMode.Production));
        Assert.Equal(LauncherMode.Production, LauncherModeResolver.Resolve("test", ["app.exe", "--production"], LauncherMode.Test));
        Assert.Equal(LauncherMode.Test, LauncherModeResolver.Resolve(null, ["--TEST"], LauncherMode.Production));
    }

    [Fact]
    public void Resolve_RejectsAnUnknownMode()
    {
        var error = Assert.Throws<InvalidOperationException>(() => LauncherModeResolver.Resolve("staging", [], LauncherMode.Production));

        Assert.Contains("staging", error.Message);
    }
}

public class LauncherServerUrlTests
{
    [Fact]
    public void Production_AlwaysUsesTheProductionUrlAndIgnoresOverrides()
    {
        Assert.Equal("https://unrealfrontiers.com", LauncherModeResolver.ResolveServerUrl(LauncherMode.Production, null));
        Assert.Equal("https://unrealfrontiers.com", LauncherModeResolver.ResolveServerUrl(LauncherMode.Production, "http://localhost:3200"));
    }

    [Fact]
    public void Test_DefaultsToTheDevBackend()
    {
        Assert.Equal("http://localhost:3200", LauncherModeResolver.ResolveServerUrl(LauncherMode.Test, null));
        Assert.Equal("http://localhost:3200", LauncherModeResolver.ResolveServerUrl(LauncherMode.Test, "  "));
    }

    [Theory]
    [InlineData("http://localhost:3201/", "http://localhost:3201")]
    [InlineData("http://192.168.1.20:3200", "http://192.168.1.20:3200")]
    [InlineData("https://dev.example.org", "https://dev.example.org")]
    [InlineData("https://dev.unrealfrontiers.com", "https://dev.unrealfrontiers.com")]
    [InlineData("https://DEV.unrealfrontiers.com/", "https://DEV.unrealfrontiers.com")]
    [InlineData("https://staging.unrealfrontiers.xyz", "https://staging.unrealfrontiers.xyz")]
    public void Test_AcceptsAnOverrideThatIsNotProduction(string value, string expected)
    {
        Assert.Equal(expected, LauncherModeResolver.ResolveServerUrl(LauncherMode.Test, value));
    }

    [Theory]
    [InlineData("https://unrealfrontiers.com")]
    [InlineData("https://www.unrealfrontiers.com/")]
    [InlineData("https://UnrealFrontiers.com")]
    [InlineData("http://localhost:3000")]
    [InlineData("http://127.0.0.1:3000/")]
    [InlineData("http://host.docker.internal:3000")]
    public void Test_RefusesTheProductionBackendInsteadOfFallingBack(string value)
    {
        var error = Assert.Throws<InvalidOperationException>(() => LauncherModeResolver.ResolveServerUrl(LauncherMode.Test, value));

        Assert.Contains("production", error.Message);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://localhost:3200")]
    [InlineData("localhost:3200")]
    public void Test_RefusesAnUnusableUrl(string value)
    {
        Assert.Throws<InvalidOperationException>(() => LauncherModeResolver.ResolveServerUrl(LauncherMode.Test, value));
    }

    [Theory]
    [InlineData("--server-url", "http://10.0.0.5:3200", "http://10.0.0.5:3200")]
    [InlineData("--SERVER-URL=http://10.0.0.5:3200", null, "http://10.0.0.5:3200")]
    public void ParseServerUrlArgument_ReadsBothForms(string first, string? second, string expected)
    {
        var args = second == null ? new[] { "app.exe", first } : new[] { "app.exe", first, second };

        Assert.Equal(expected, LauncherModeResolver.ParseServerUrlArgument(args));
    }

    [Fact]
    public void ParseServerUrlArgument_IsNullWithoutTheArgument()
    {
        Assert.Null(LauncherModeResolver.ParseServerUrlArgument(["app.exe", "--test"]));
        Assert.Null(LauncherModeResolver.ParseServerUrlArgument(["app.exe", "--server-url"]));
    }

    [Theory]
    [InlineData("http://localhost:3200", "http://127.0.0.1:3200")]
    [InlineData("http://LocalHost:3200/", "http://127.0.0.1:3200")]
    [InlineData("http://192.168.1.20:3200", "http://192.168.1.20:3200")]
    [InlineData("https://dev.example.org", "https://dev.example.org")]
    public void ForGame_UsesTheIPv4LoopbackForLocalhost(string value, string expected)
    {
        Assert.Equal(expected, LauncherModeResolver.ForGame(value));
    }

    [Fact]
    public void TestBanner_NamesTheBackend()
    {
        Assert.Equal("TEST (localhost:3200)", LauncherModeResolver.DescribeTestBackend("http://localhost:3200"));
    }
}

public class TestModeIsolationTests
{
    [Fact]
    public void ThisBuildInTestModeKeepsItsDataAndLocksApartFromProduction()
    {
        // The test project is built in Debug, which defaults to test mode.
        Assert.True(AppStaticData.IsTestMode);
        Assert.Equal(".Test", AppStaticData.InstanceSuffix);
        Assert.EndsWith("UnrealFrontiers.Test", AppStaticData.LauncherBasePath);
        Assert.Equal("http://localhost:3200", AppStaticData.ServerURL);
    }
}
