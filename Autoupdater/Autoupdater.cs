using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using UnrealFrontiersLauncher;

public class Autoupdater
{
    private static string VersionFileUrl = AppSettings.GetFileDownloadPath("version", "launcher");
    private static string InstallerDownloadUrl = AppSettings.GetFileDownloadPath("UFLauncher_installer.exe", "launcher");
    private const string InstallerFileName = "UFLauncher_installer.exe";
    private const string CurrentVersion = "0.2.9";

    public static async void DoTheThingOrOtherwise(Action? finalize = null)
    {
        try
        {
            Console.WriteLine("Checking for updates...");

            string latestVersion = await FetchLatestVersion();

            // Compare versions
            if (IsNewVersionAvailable(CurrentVersion, latestVersion))
            {
                Console.WriteLine($"↓ SMOTRI SUDA ↓");
                Console.WriteLine($"New launcher version available: {latestVersion}. Download in progress...");
                Console.WriteLine($"↑ SMOTRI SUDA ↑");
                await DownloadInstaller();
                RunInstaller();
            }
            else
            {
                Console.WriteLine("Your application is up to date.");
                finalize?.Invoke();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Auto-update failed: {ex.Message}");
        }
    }

    private static async Task<string> FetchLatestVersion()
    {
        using var httpClient = new HttpClient();
        Console.WriteLine("Fetching version information...");
        string version = await httpClient.GetStringAsync(VersionFileUrl);
        return version.Trim();
    }

    private static bool IsNewVersionAvailable(string currentVersion, string latestVersion)
    {
        var current = new Version(currentVersion);
        var latest = new Version(latestVersion);
        return latest > current;
    }

    private static async Task DownloadInstaller()
    {
        string tempFilePath = Path.Combine(Path.GetTempPath(), InstallerFileName);

        Console.WriteLine("Downloading installer...");

        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(InstallerDownloadUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        using var contentStream = await response.Content.ReadAsStreamAsync();
        using var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
        await contentStream.CopyToAsync(fileStream);

        Console.WriteLine($"Installer downloaded to: {tempFilePath}");
    }

    private static void RunInstaller()
    {
        string tempFilePath = Path.Combine(Path.GetTempPath(), InstallerFileName);

        Console.WriteLine("Running installer...");
        var installerProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = tempFilePath,
                Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /CLOSEAPPLICATIONS",
                UseShellExecute = true
            }
        };

        installerProcess.Start();
        Console.WriteLine("Installer started. Waiting for it to finish...");

        installerProcess.WaitForExit();

        Console.WriteLine("Restarting current application...");
        RestartApplication();
    }

    private static async void RestartApplication()
    {
        string exePath = Process.GetCurrentProcess().MainModule.FileName;

        Console.WriteLine($"Restarting application: {exePath}");

        Process.Start(new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = true
        });

        Environment.Exit(0);
    }
}

