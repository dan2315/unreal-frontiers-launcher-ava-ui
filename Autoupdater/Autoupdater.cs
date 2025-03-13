using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using UnrealFrontiersLauncher;

public class Autoupdater
{
    private static string VersionFileUrl = AppSettings.GetFileDownloadPath("version", "launcher");
#if WINDOWS
    private static string InstallerDownloadUrl = AppSettings.GetFileDownloadPath("UFLauncher_installer.exe", "launcher");
    private const string InstallerFileName = "UFLauncher_installer.exe";
#elif LINUX
    private static string InstallerDownloadUrl = AppSettings.GetFileDownloadPath("install.sh", "launcher");
    private const string InstallerFileName = "install.sh";
#endif
    private const string CurrentVersion = "0.3.1";

    public static Action<float> OnProgress;
    public static Action<string> OnSetStatus;

    public static async Task AutopdateOrOtherwise(Action? updateIsNotRequiredFallback = null)
    {
        try
        {
            Console.WriteLine("Checking for updates...");
            OnSetStatus?.Invoke("Checking for updates...");

            string latestVersion = await FetchLatestVersion();

            if (IsNewVersionAvailable(CurrentVersion, latestVersion))
            {
                Console.WriteLine($"New launcher version available: {latestVersion}. Download in progress...");
                OnSetStatus?.Invoke($"New launcher version available: {latestVersion}. Download in progress...");
                await DownloadInstaller();
#if WINDOWS
                RunWindowsInstaller();
#elif LINUX
                InstallAndRunFlatpak();
#endif
            }
            else
            {
                Console.WriteLine("Your application is up to date.");
                OnSetStatus?.Invoke($"Your application is up to date.");
                updateIsNotRequiredFallback?.Invoke();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Auto-update failed: {ex.Message}");
            OnSetStatus?.Invoke($"Auto-update failed: {ex.Message}");
        }
    }

    private static async Task<string> FetchLatestVersion()
    {
        using var httpClient = new HttpClient();
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

        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(InstallerDownloadUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        var totalBytesReadable = totalBytes > 0;

        using var contentStream = await response.Content.ReadAsStreamAsync();
        using var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        byte[] buffer = new byte[8192]; // Chunk size
        long totalRead = 0;
        int read;

        while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, read);
            totalRead += read;

            if (totalBytesReadable)
            {
                float progressValue = (float)totalRead / totalBytes * 100;
                OnProgress.Invoke(progressValue);
            }
        }

        Console.WriteLine($"Installer downloaded to: {tempFilePath}");
    }

#if WINDOWS
    private static void RunWindowsInstaller()
    {
        string tempFilePath = Path.Combine(Path.GetTempPath(), InstallerFileName);

        Console.WriteLine("Running installer...");
        OnSetStatus?.Invoke($"Running installer...");
        var installerProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = tempFilePath,
                Arguments = "/CLOSEAPPLICATIONS",
                UseShellExecute = true
            }
        };

        installerProcess.Start();
        
        Task.Delay(1000).Wait();
        
        Environment.Exit(0);
    }
#elif LINUX
    private static void InstallAndRunFlatpak()
    {
        string tempFilePath = Path.Combine(Path.GetTempPath(), InstallerFileName);

        Console.WriteLine("Running installation script...");
        OnSetStatus?.Invoke("Running installation script...");

        // Make the script executable
        var chmodProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"+x {tempFilePath}",
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        chmodProcess.Start();
        chmodProcess.WaitForExit();

        // Run the installation script
        var installProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = tempFilePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        installProcess.Start();
        installProcess.WaitForExit();

        if (installProcess.ExitCode == 0)
        {
            Console.WriteLine("Installation completed successfully.");
            OnSetStatus?.Invoke("Installation completed successfully.");

            Console.WriteLine("Running application...");
            OnSetStatus?.Invoke("Running application...");
            RunUpdatedApplication();
        }
        else
        {
            Console.WriteLine("Installation failed.");
            OnSetStatus?.Invoke("Installation failed.");
        }
    }

    private static void RunUpdatedApplication()
    {
        // Run the newly installed application
        var runProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "UnrealFrontiersLauncher",
                UseShellExecute = false,
                CreateNoWindow = false
            }
        };

        try
        {
            runProcess.Start();
            Console.WriteLine("Application started.");
            OnSetStatus?.Invoke("Application started.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start application: {ex.Message}");
            OnSetStatus?.Invoke($"Failed to start application: {ex.Message}");
        }
    }
#endif

    private static async void RestartApplication()
    {
#if WINDOWS
        string exePath = Process.GetCurrentProcess().MainModule.FileName;
        Process.Start(new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = true
        });
#elif LINUX
        RunUpdatedApplication();
#endif

        Environment.Exit(0);
    }
}

