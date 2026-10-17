using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Files;
using CmlLib.Core.Installers;
using UnrealFrontiersLauncher;

public class ModpackInstaller
{
    private readonly HttpClient _httpClient;
    private readonly int _maxRetries;
    private readonly TimeSpan _stallThreshold;

    public ModpackInstaller(HttpClient httpClient, int maxRetries = 5, TimeSpan? stallThreshold = null)
    {
        _httpClient = httpClient;
        _maxRetries = maxRetries;
        _stallThreshold = stallThreshold ?? TimeSpan.FromSeconds(30);
    }

    public async Task InstallModpackAsync(MinecraftInstance minecraftInstance, IProgress<ByteProgress> onProgressChanged, IProgress<InstallerProgressChangedEventArgs> onFileChanged)
    {
        string resourcePath = minecraftInstance.GetResourcePath();

        await DeleteObsoleteFilesAsync(AppStaticData.GetInstanceObsoleteFiles(minecraftInstance.Id), resourcePath);

        var fileIndexes = await FetchFileIndexesAsync(AppStaticData.GetInstanceIndexes(minecraftInstance.Id));
        if (fileIndexes == null || fileIndexes.Count == 0)
        {
            Console.WriteLine("No files found in indexes.");
            return;
        }

        var gameFiles = fileIndexes.Select(index =>
        {
            var (filePath, fileInfo) = index;
            var correctedFilePath = filePath.Replace("\\", "/");

            return new GameFile(correctedFilePath)
            {
                Path = PathSafety.ResolveInside(resourcePath, correctedFilePath),
                Hash = fileInfo.Hash,
                Size = fileInfo.Size,
                Url = AppStaticData.GetFileDownloadPath(correctedFilePath, minecraftInstance.Id)
            };
        }).ToList();

        var installer = ParallelGameInstaller.CreateAsCoreCount(_httpClient);
        await InstallWithRetries(installer, gameFiles.Where(file => file.Hash != "0").ToList(), onProgressChanged, onFileChanged);

        // Hash "0" marks user-editable config files: skip verification.
        installer.CheckFileChecksum = false;
        installer.CheckFileSize = false;
        await InstallWithRetries(installer, gameFiles.Where(file => file.Hash == "0").ToList(), onProgressChanged, onFileChanged);

        Console.WriteLine("All files downloaded and verified.");
    }

    private async Task DeleteObsoleteFilesAsync(string url, string resourcePath)
    {
        List<string> obsoleteFiles;
        try
        {
            obsoleteFiles = await FetchObsoleteFilesAsync(url);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            Console.WriteLine($"Could not fetch obsolete files list: {ex.Message}");
            return;
        }

        foreach (var file in obsoleteFiles)
        {
            try
            {
                var path = PathSafety.ResolveInside(resourcePath, file.Replace("\\", "/"));
                if (File.Exists(path))
                {
                    File.Delete(path);
                    Console.WriteLine($"Deleted: {path}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting file {file}: {ex.Message}");
            }
        }
    }

    private async Task<Dictionary<string, FileMetadata>?> FetchFileIndexesAsync(string indexUrl)
    {
        var json = await _httpClient.GetStringAsync(indexUrl);
        return JsonSerializer.Deserialize<Dictionary<string, FileMetadata>>(json);
    }

    private async Task<List<string>> FetchObsoleteFilesAsync(string url)
    {
        var json = await _httpClient.GetStringAsync(url);
        return JsonSerializer.Deserialize<ObsoleteFiles>(json)?.Files ?? [];
    }

    private async Task InstallWithRetries(
        ParallelGameInstaller installer,
        List<GameFile> gameFiles,
        IProgress<ByteProgress> onProgressChanged,
        IProgress<InstallerProgressChangedEventArgs> onFileChanged)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                Console.WriteLine($"Attempt {attempt}/{_maxRetries} to install files...");
                await InstallWithStallDetection(installer, gameFiles, onProgressChanged, onFileChanged);
                return;
            }
            catch (Exception ex) when (ex is OperationCanceledException or HttpRequestException or IOException)
            {
                Console.WriteLine($"Attempt {attempt}/{_maxRetries} failed: {ex.Message}");
                if (attempt >= _maxRetries) throw;

                await Task.Delay(RetryDelay(ex, attempt));
            }
        }
    }

    // A server that answers "busy" (429/503) needs more room than a dropped connection.
    public static TimeSpan RetryDelay(Exception error, int attempt)
    {
        var busy = error is HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable };
        return TimeSpan.FromMilliseconds((busy ? 3000 : 500) * Math.Pow(2, attempt - 1));
    }

    private async Task InstallWithStallDetection(
        ParallelGameInstaller installer,
        List<GameFile> gameFiles,
        IProgress<ByteProgress> onProgressChanged,
        IProgress<InstallerProgressChangedEventArgs> onFileChanged)
    {
        using var cts = new CancellationTokenSource();
        var monitor = new ProgressMonitor(onProgressChanged, onFileChanged, _stallThreshold, cts);

        await monitor.MonitorProgress(() =>
            installer.Install(gameFiles, fileProgress: monitor.FileProgress, byteProgress: monitor.ByteProgress, cancellationToken: cts.Token).AsTask());
    }
}

public class ProgressMonitor
{
    private readonly TimeSpan _stallThreshold;
    private readonly CancellationTokenSource _installCts;
    private long _lastReportedBytes;
    private long _lastActivityTicks = DateTime.UtcNow.Ticks;

    public IProgress<ByteProgress> ByteProgress { get; }
    public IProgress<InstallerProgressChangedEventArgs> FileProgress { get; }

    public ProgressMonitor(
        IProgress<ByteProgress> byteProgress,
        IProgress<InstallerProgressChangedEventArgs> fileProgress,
        TimeSpan stallThreshold,
        CancellationTokenSource installCts)
    {
        _stallThreshold = stallThreshold;
        _installCts = installCts;

        ByteProgress = new Progress<ByteProgress>(progress =>
        {
            if (progress.ProgressedBytes > _lastReportedBytes)
            {
                _lastReportedBytes = progress.ProgressedBytes;
                Touch();
            }

            byteProgress.Report(progress);
        });

        FileProgress = new Progress<InstallerProgressChangedEventArgs>(args =>
        {
            Touch();
            fileProgress.Report(args);
        });
    }

    public async Task MonitorProgress(Func<Task> installTask)
    {
        Touch();
        using var monitorCts = new CancellationTokenSource();
        var monitorTask = WatchForStall(monitorCts.Token);

        try
        {
            await installTask();
        }
        finally
        {
            monitorCts.Cancel();
            await monitorTask;
        }
    }

    private void Touch() => Interlocked.Exchange(ref _lastActivityTicks, DateTime.UtcNow.Ticks);

    private async Task WatchForStall(CancellationToken token)
    {
        try
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), token);

                var idle = DateTime.UtcNow - new DateTime(Interlocked.Read(ref _lastActivityTicks), DateTimeKind.Utc);
                if (idle > _stallThreshold)
                {
                    Console.WriteLine("Download stalled. Cancelling...");
                    _installCts.Cancel();
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
