using System;
using System.Threading.Tasks;
using UnrealFrontiersLauncher;

// Opt-in: installs and starts a real modpack against the local backend.
// Run with UF_RUN_LAUNCH_TEST=1 UF_LAUNCH_MODPACK=<id>.
public class LaunchIntegrationTests
{
    [Fact]
    public async Task Installs_and_starts_the_modpack()
    {
        if (Environment.GetEnvironmentVariable("UF_RUN_LAUNCH_TEST") != "1") return;

        var modpackId = Environment.GetEnvironmentVariable("UF_LAUNCH_MODPACK") ?? "zaruba";
        var instances = new InstanceManager();
        Assert.True(await instances.SelectInstance(modpackId), "version info could not be loaded");

        var instance = instances.selectedInstance!;
        instance.ApplyNewShouldCloseValue(false);
        instance.ApplyNewShouldOpenConsoleValue(false);
        Console.WriteLine($"[launch-test] {instance.Name}: {instance.GameVersion} {instance.Loader} {instance.ForgeVersion}");

        var auth = new AuthService(new HttpClientService());
        var nickname = "LauncherTest";
        var error = (string?)null;

        // Optional: a real dev account, so the game gets a token the server accepts.
        if (Environment.GetEnvironmentVariable("UF_TEST_USER") is { Length: > 0 } user)
        {
            var login = await auth.Login(user, Environment.GetEnvironmentVariable("UF_TEST_PASSWORD") ?? "");
            Assert.True(login.Success, login.Message);
            nickname = login.Nickname!;
        }

        await new GameLauncher(auth).LaunchMinecraft(
            instance,
            new PlayerProfile(nickname),
            progress => { },
            file => Console.WriteLine($"[launch-test] {file}"),
            speed => { },
            () => Console.WriteLine("[launch-test] game exited"),
            message => { error = message; Console.WriteLine($"[launch-test] ERROR {message}"); },
            null!);

        Assert.Null(error);
    }
}
