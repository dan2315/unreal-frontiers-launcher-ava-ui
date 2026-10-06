using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        _stallThreshold = stallThreshold ?? TimeSpan.FromSeconds(15);
    }

    public async Task InstallModpackAsync(MinecraftInstance minecraftInstance, IProgress<ByteProgress> onProgressChanged, IProgress<InstallerProgressChangedEventArgs> onFileChanged)
    {
        string indexUrl = AppStaticData.GetInstanceIndexes(minecraftInstance.Id);
        string obsoleteFilesUrl = AppStaticData.GetInstanceObsoleteFiles(minecraftInstance.Id);
        string resourcePath = minecraftInstance.GetResourcePath();

        List<string> obsoleteFiles = await FetchObsoleteFilesAsync(obsoleteFilesUrl);
        if (obsoleteFiles != null && obsoleteFiles.Count > 0) 
        {
            foreach (var file in obsoleteFiles)
            {
                string path = Path.Combine(minecraftInstance.GetResourcePath(), file);
                var correctedFilePath = path.Replace("\\", "/");
                try
                {
                    if (File.Exists(correctedFilePath))
                    {
                        File.Delete(correctedFilePath);
                        Console.WriteLine($"Deleted: {correctedFilePath}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error deleting file {correctedFilePath}: {ex.Message}");
                }
            }
        }
        
        var fileIndexes = await FetchFileIndexesAsync(indexUrl);
        if (fileIndexes == null || fileIndexes.Count == 0)
        {
            Console.WriteLine("No files found in indexes.");
            return;
        }

        // Create list of game files
        var gameFiles = fileIndexes.Select(index =>
        {
            var (filePath, fileInfo) = index;
            var correctedFilePath = filePath.Replace("\\", "/");

            string fileUrl = AppStaticData.GetFileDownloadPath(correctedFilePath, minecraftInstance.Id);

            return new GameFile(correctedFilePath)
            {
                Path = Path.Combine(resourcePath, correctedFilePath),
                Hash = fileInfo.Hash,
                Size = fileInfo.Size,
                Url = fileUrl
            };
        }).ToList();

        // Create an installer and download the files with retries
        var installer = ParallelGameInstaller.CreateAsCoreCount(_httpClient);
        await InstallWithRetries(installer, gameFiles.Where(file => file.Hash != "0").ToList(), onProgressChanged, onFileChanged);
        
        installer.CheckFileChecksum = false;
        installer.CheckFileSize = false;
        Console.WriteLine("Processing config files.");
        await InstallWithRetries(installer, gameFiles.Where(file => file.Hash == "0").ToList(), onProgressChanged, onFileChanged);

        Console.WriteLine("All files downloaded and verified.");
    }

    private async Task<Dictionary<string, FileMetadata>> FetchFileIndexesAsync(string indexUrl)
    {
        var response = await _httpClient.GetAsync(indexUrl);
        response.EnsureSuccessStatusCode();
        string jsonResponse = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Dictionary<string, FileMetadata>>(jsonResponse);
    }

    private async Task<List<string>> FetchObsoleteFilesAsync(string url)
    {
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        string jsonResponse = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ObsoleteFiles>(jsonResponse).Files;
    }

    private async Task InstallWithRetries(
        ParallelGameInstaller installer,
        List<GameFile> gameFiles,
        IProgress<ByteProgress> onProgressChanged,
        IProgress<InstallerProgressChangedEventArgs> onFileChanged)
    {
        int attempt = 0;
        while (attempt < _maxRetries)
        {
            try
            {
                Console.WriteLine($"Attempt {attempt + 1}/{_maxRetries} to install files...");
                await InstallWithStallDetection(installer, gameFiles, onProgressChanged, onFileChanged);
                return; // Exit loop if successful
            }
            catch (OperationCanceledException)
            {
                attempt++;
                Console.WriteLine($"Attempt {attempt}/{_maxRetries} failed due to stall. Retrying...");
                if (attempt < _maxRetries)
                {
                    await Task.Delay(500 * (int)Math.Pow(2, attempt - 1)); // Exponential backoff
                }
                else
                {
                    Console.WriteLine("Maximum retry attempts reached. Download failed.");
                    throw;
                }
            }
        }
    }

    private async Task InstallWithStallDetection(ParallelGameInstaller installer, List<GameFile> gameFiles, IProgress<ByteProgress> onProgressChanged, IProgress<InstallerProgressChangedEventArgs> onFileChanged)
    {
        using var cts = new CancellationTokenSource();
        var progressTracker = new ProgressMonitor(onProgressChanged, _stallThreshold, cts);

        try
        {
            var wrappedProgress = new Progress<ByteProgress>(progressTracker.ReportProgress);
            await progressTracker.MonitorProgress(async () =>
            {
                await installer.Install(gameFiles, fileProgress: onFileChanged, byteProgress: wrappedProgress, cancellationToken: cts.Token);
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }
}

public class ProgressMonitor
{
    private readonly IProgress<ByteProgress> _progress;
    private readonly TimeSpan _stallThreshold;
    private readonly CancellationTokenSource _cts;
    private long _lastReportedBytes = 0;
    private DateTime _lastProgressTime = DateTime.UtcNow;

    public ProgressMonitor(IProgress<ByteProgress> progress, TimeSpan stallThreshold, CancellationTokenSource cts)
    {
        _progress = progress;
        _stallThreshold = stallThreshold;
        _cts = cts;
    }

    public async Task MonitorProgress(Func<Task> downloadTask)
    {
        Task monitorTask = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                await Task.Delay(_stallThreshold);

                if ((DateTime.UtcNow - _lastProgressTime) > _stallThreshold)
                {
                    Console.WriteLine("Download stalled. Cancelling...");
                    _cts.Cancel();
                }
            }
        });

        try
        {
            await downloadTask();
        }
        finally
        {
            _cts.Cancel();
            await monitorTask;
        }
    }

    public void ReportProgress(ByteProgress progress)
    {
        if (progress.ProgressedBytes > _lastReportedBytes)
        {
            _lastReportedBytes = progress.ProgressedBytes;
            _lastProgressTime = DateTime.UtcNow;
        }

        _progress?.Report(progress);
    }
}
