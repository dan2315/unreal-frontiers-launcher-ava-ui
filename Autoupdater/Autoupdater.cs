using System;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Threading;
using UnrealFrontiersLauncher;
using Velopack;

public static class Autoupdater
{
    public static Action<float>? OnProgress;
    public static Action<string>? OnSetStatus;

    public static Version CurrentVersion
    {
        get
        {
            var version = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);
            return new Version(version.Major, version.Minor, Math.Max(version.Build, 0));
        }
    }

    public static async Task CheckForUpdatesAsync(Action onContinue)
    {
        try
        {
            var manager = new UpdateManager(new ServerUpdateSource());

            if (AppStaticData.IsTestMode)
            {
                SetStatus("Test mode, skipping update check.");
            }
            else if (!manager.IsInstalled)
            {
                SetStatus("Not an installed build, skipping update check.");
            }
            else
            {
                SetStatus("Checking for updates...");
                var update = await manager.CheckForUpdatesAsync();

                if (update != null)
                {
                    SetStatus($"New launcher version available: {update.TargetFullRelease.Version}. Download in progress...");
                    await manager.DownloadUpdatesAsync(update, percent =>
                        Dispatcher.UIThread.Post(() => OnProgress?.Invoke(percent)));

                    SetStatus("Installing update...");
                    manager.ApplyUpdatesAndRestart(update);
                    return;
                }

                SetStatus("Your application is up to date.");
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Update check failed: {ex.Message}");
            await Task.Delay(1500);
        }

        onContinue();
    }

    private static void SetStatus(string status)
    {
        Console.WriteLine(status);
        OnSetStatus?.Invoke(status);
    }
}
