using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using UnrealFrontiersLauncher;

public static class Autoupdater
{
    private static readonly string VersionFileUrl = AppStaticData.GetFileDownloadPath("version", "launcher");
#if WINDOWS
    private const string InstallerFileName = "UFLauncher_installer.exe";
#elif LINUX
    private const string InstallerFileName = "install.sh";
#endif
    private static readonly string InstallerDownloadUrl = AppStaticData.GetFileDownloadPath(InstallerFileName, "launcher");

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
            SetStatus("Checking for updates...");

            var latestVersion = await FetchLatestVersion();

            if (IsNewVersionAvailable(CurrentVersion, latestVersion))
            {
                SetStatus($"New launcher version available: {latestVersion}. Download in progress...");
                await DownloadInstaller();
#if WINDOWS
                await RunWindowsInstaller();
#elif LINUX
                await InstallAndRunInstallation();
#endif
                return;
            }

            SetStatus("Your application is up to date.");
        }
        catch (Exception ex)
        {
            SetStatus($"Update check failed: {ex.Message}");
            await Task.Delay(1500);
        }

        onContinue();
    }

    public static bool IsNewVersionAvailable(Version current, string latest)
    {
        return TryParseVersion(latest, out var latestVersion) && latestVersion > current;
    }

    public static bool TryParseVersion(string text, out Version version)
    {
        var trimmed = text.Trim().TrimStart('v', 'V');
        var suffixStart = trimmed.IndexOfAny(new[] { '-', '+' });
        if (suffixStart >= 0) trimmed = trimmed[..suffixStart];
        return Version.TryParse(trimmed, out version!);
    }

    private static void SetStatus(string status)
    {
        Console.WriteLine(status);
        OnSetStatus?.Invoke(status);
    }

    private static async Task<string> FetchLatestVersion()
    {
        var version = await SharedHttp.Api.GetStringAsync(VersionFileUrl);
        return version.Trim();
    }

    private static void EnsureSecureSource(string url)
    {
        var uri = new Uri(url);
        if (uri.Scheme != Uri.UriSchemeHttps && !uri.IsLoopback)
        {
            throw new InvalidOperationException("Refusing to download an installer over an insecure connection.");
        }
    }

    private static string InstallerPath => Path.Combine(Path.GetTempPath(), InstallerFileName);

    private static async Task DownloadInstaller()
    {
        EnsureSecureSource(InstallerDownloadUrl);

        using var response = await SharedHttp.Api.GetAsync(InstallerDownloadUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;

        await using var contentStream = await response.Content.ReadAsStreamAsync();
        await using var fileStream = new FileStream(InstallerPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        var buffer = new byte[8192];
        long totalRead = 0;
        int read;

        while ((read = await contentStream.ReadAsync(buffer)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read));
            totalRead += read;

            if (totalBytes > 0)
            {
                OnProgress?.Invoke((float)totalRead / totalBytes * 100);
            }
        }
    }

#if WINDOWS
    private static async Task RunWindowsInstaller()
    {
        SetStatus("Running installer...");
        Process.Start(new ProcessStartInfo
        {
            FileName = InstallerPath,
            Arguments = "/CLOSEAPPLICATIONS",
            UseShellExecute = true
        });

        await Task.Delay(1000);
        Environment.Exit(0);
    }
#elif LINUX
    private static async Task InstallAndRunInstallation()
    {
        SetStatus("Running installation script...");
        File.SetUnixFileMode(InstallerPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        using var installProcess = Process.Start(new ProcessStartInfo
        {
            FileName = InstallerPath,
            UseShellExecute = false,
            CreateNoWindow = true
        })!;
        await installProcess.WaitForExitAsync();

        if (installProcess.ExitCode != 0)
        {
            throw new InvalidOperationException($"Installation script exited with code {installProcess.ExitCode}.");
        }

        SetStatus("Running application...");
        Process.Start(new ProcessStartInfo { FileName = "UnrealFrontiersLauncher", UseShellExecute = false });
        Environment.Exit(0);
    }
#endif
}
