using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace UnrealFrontiersLauncher.Packs;

public readonly record struct PackProgress(long TotalBytes, long DoneBytes, int TotalFiles, int DoneFiles, string? File);

public sealed record PackInstallResult(int Downloaded, long DownloadedBytes, int Removed, int Verified);

/// <summary>
/// Makes an instance folder match a pack manifest (docs/modpacks.md in the backend repo): checks the files it has, removes what an
/// older version installed and the new one dropped, downloads what is missing by SHA-256 (pack server first, then the file's extra
/// URLs), resuming partial downloads. Files of the player that were never installed are never touched.
/// </summary>
public sealed class PackInstaller(HttpClient http, string serverUrl, int concurrency = 4, TimeSpan? stall = null, int attemptsPerSource = 3)
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private readonly TimeSpan _stall = stall ?? TimeSpan.FromSeconds(30);

    /// <summary>A server that answers "busy" (429/503) needs more room than a dropped connection.</summary>
    public static TimeSpan RetryDelay(Exception error, int attempt)
    {
        var busy = error is HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable };
        return TimeSpan.FromMilliseconds((busy ? 3000 : 500) * Math.Pow(2, attempt - 1));
    }

    public async Task<PackInstallResult> InstallAsync(PackManifest manifest, string instanceFolder, IProgress<PackProgress>? progress, CancellationToken ct)
    {
        var root = Path.GetFullPath(instanceFolder);
        Directory.CreateDirectory(root);
        var state = InstalledState.Load(root);
        // Paths are compared the way the file system does: "mods/A.jar" and "mods/a.jar" are one file on Windows.
        state.Files = new Dictionary<string, InstalledState.InstalledFile>(state.Files, PathComparer);

        var wanted = new Dictionary<string, (PackFile File, string Target)>(PathComparer);
        var wantedTargets = new HashSet<string>(PathComparer);
        foreach (var file in manifest.ClientFiles)
        {
            string target;
            try
            {
                target = PathSafety.ResolveInside(root, file.Path);
            }
            catch (InvalidOperationException)
            {
                throw new PackException($"Сборка содержит недопустимый путь: {file.Path}");
            }
            if (Path.GetFileName(target).Equals(InstalledState.FileName, PathComparison) || target.EndsWith(".uf-part", PathComparison))
                throw new PackException($"Сборка содержит служебное имя файла: {file.Path}");
            EnsureNoLinks(root, target, file.Path);
            wanted[file.Path] = (file, target);
            wantedTargets.Add(target);
        }

        var toDownload = new List<(PackFile File, string Target)>();
        var verified = 0;
        try
        {
            // 1. What is already right: unchanged files are recognised by size and time, the rest are hashed.
            await Parallel.ForEachAsync(wanted.Values, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(2, concurrency), CancellationToken = ct }, async (entry, token) =>
            {
                var (file, target) = entry;
                var info = new FileInfo(target);
                bool ok;
                if (!info.Exists)
                {
                    ok = false;
                }
                else if (file.Overwrite == "once")
                {
                    // The player's from now on: neither replaced nor, if the pack drops it, removed.
                    ok = true;
                }
                else
                {
                    bool known;
                    lock (state)
                    {
                        known = state.Files.TryGetValue(file.Path, out var s)
                            && s.Sha256 == file.Sha256 && s.Size == info.Length && s.MTimeTicks == info.LastWriteTimeUtc.Ticks && info.Length == file.Size;
                    }
                    ok = known || (info.Length == file.Size && await HashAsync(target, token) == file.Sha256);
                    if (ok)
                        Track(state, file, new FileInfo(target));
                }
                lock (toDownload)
                {
                    if (ok)
                        verified++;
                    else
                        toDownload.Add(entry);
                }
            });

            // 2. What an older version installed and this one no longer lists: only files that are still what was installed, and
            //    only through folders that are not links. A file that is locked stays tracked so the next update tries again.
            var removed = 0;
            foreach (var (path, installed) in state.Files.Where(kv => !wanted.ContainsKey(kv.Key)).ToList())
            {
                try
                {
                    var target = PathSafety.ResolveInside(root, path);
                    if (wantedTargets.Contains(target))
                    {
                        state.Files.Remove(path);
                        continue;
                    }
                    EnsureNoLinks(root, target, path);
                    if (!File.Exists(target))
                    {
                        state.Files.Remove(path);
                        continue;
                    }
                    if (await HashAsync(target, ct) != installed.Sha256)
                    {
                        // The player changed it: it is theirs now.
                        state.Files.Remove(path);
                        continue;
                    }
                    File.Delete(target);
                    state.Files.Remove(path);
                    removed++;
                    RemoveEmptyFolders(root, Path.GetDirectoryName(target)!);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or PackException)
                {
                    Console.WriteLine($"Could not remove {path}: {e.Message}");
                }
            }

            // 3. What is missing.
            var totalBytes = toDownload.Sum(d => d.File.Size);
            long doneBytes = 0;
            var doneFiles = 0;
            var clock = Stopwatch.StartNew();
            long lastReport = -1000;
            void Report(string? current, bool force)
            {
                if (progress is null)
                    return;
                var now = clock.ElapsedMilliseconds;
                if (!force && now - Interlocked.Read(ref lastReport) < 100)
                    return;
                Interlocked.Exchange(ref lastReport, now);
                progress.Report(new PackProgress(totalBytes, Interlocked.Read(ref doneBytes), toDownload.Count, Volatile.Read(ref doneFiles), current));
            }

            Report(null, true);
            await Parallel.ForEachAsync(toDownload, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, concurrency), CancellationToken = ct }, async (entry, token) =>
            {
                var (file, target) = entry;
                await DownloadAsync(file, target, delta =>
                {
                    Interlocked.Add(ref doneBytes, delta);
                    Report(file.Path, false);
                }, token);
                if (file.Overwrite != "once")
                    Track(state, file, new FileInfo(target));
                Interlocked.Increment(ref doneFiles);
                Report(file.Path, true);
            });

            return new PackInstallResult(toDownload.Count, totalBytes, removed, verified);
        }
        finally
        {
            // Saved even when a download failed: what was installed so far is not hashed again next time.
            state.Pack = manifest.Pack;
            state.Version = manifest.Version;
            try
            {
                lock (state)
                    state.Save(root);
            }
            catch (IOException e)
            {
                Console.WriteLine($"Could not save the install state: {e.Message}");
            }
        }
    }

    private static void Track(InstalledState state, PackFile file, FileInfo info)
    {
        lock (state)
            state.Files[file.Path] = new InstalledState.InstalledFile(file.Sha256, info.Length, info.LastWriteTimeUtc.Ticks);
    }

    private async Task DownloadAsync(PackFile file, string target, Action<long> onBytes, CancellationToken ct)
    {
        var sources = new List<string> { $"{serverUrl.TrimEnd('/')}/updateclient/blobs/{file.Sha256}" };
        foreach (var url in file.Urls ?? [])
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
                sources.Add(url);

        Exception? last = null;
        long reported = 0;
        foreach (var source in sources)
        {
            var restarted = false;
            for (var attempt = 1; attempt <= attemptsPerSource; attempt++)
            {
                try
                {
                    await FetchAsync(source, file, target, delta => { reported += delta; onBytes(delta); }, () => reported, ct);
                    return;
                }
                catch (HashMismatchException e)
                {
                    last = e;
                    // A resumed download that fails the hash may have been resumed onto a blob that changed since: once more from scratch.
                    if (e.WasResumed && !restarted)
                    {
                        restarted = true;
                        attempt--;
                        continue;
                    }
                    // Otherwise this source serves something else: another try of it will not help.
                    break;
                }
                catch (Exception e) when (e is HttpRequestException or IOException or TimeoutException or UnauthorizedAccessException
                                          || (e is OperationCanceledException && !ct.IsCancellationRequested))
                {
                    last = e;
                    // A file that is not there or a request that is refused will not change in a second.
                    if (e is HttpRequestException { StatusCode: { } status } && (int)status is >= 400 and < 500 && status is not (HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests))
                        break;
                    if (attempt < attemptsPerSource)
                        await Task.Delay(RetryDelay(e, attempt), ct);
                }
            }
        }
        var reason = last is UnauthorizedAccessException ? "файл занят другой программой" : last?.Message;
        throw new PackException($"Не удалось скачать {file.Path}: {reason}", last);
    }

    private async Task FetchAsync(string url, PackFile file, string target, Action<long> onBytes, Func<long> reportedSoFar, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        // Named by the hash: a partial file of an older blob at the same path is never continued with the tail of a new one.
        var part = $"{target}.{file.Sha256[..16]}.uf-part";
        long have = File.Exists(part) ? new FileInfo(part).Length : 0;
        if (have > file.Size)
        {
            File.Delete(part);
            have = 0;
        }
        var resumed = have > 0;
        // Bytes counted for this file so far are replaced by what is on disk now.
        onBytes(have - reportedSoFar());

        if (have < file.Size)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (have > 0)
                request.Headers.Range = new RangeHeaderValue(have, null);
            HttpResponseMessage response;
            using (var headers = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                headers.CancelAfter(_stall);
                try
                {
                    response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, headers.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    throw new IOException("сервер не отвечает");
                }
            }
            using (response)
            {
                if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                {
                    File.Delete(part);
                    throw new IOException("сервер не принял докачку, начинаем заново");
                }
                response.EnsureSuccessStatusCode();
                var append = have > 0 && response.StatusCode == HttpStatusCode.PartialContent;
                if (append && response.Content.Headers.ContentRange?.From is { } from && from != have)
                {
                    File.Delete(part);
                    throw new IOException("сервер вернул не тот фрагмент, начинаем заново");
                }
                if (!append && have > 0)
                {
                    onBytes(-have);
                    have = 0;
                }

                await using var input = await response.Content.ReadAsStreamAsync(ct);
                await using var output = new FileStream(part, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                var buffer = new byte[81920];
                var total = have;
                while (true)
                {
                    using var watchdog = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    watchdog.CancelAfter(_stall);
                    int read;
                    try
                    {
                        read = await input.ReadAsync(buffer, watchdog.Token);
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        throw new IOException("загрузка зависла");
                    }
                    if (read == 0)
                        break;
                    total += read;
                    if (total > file.Size)
                    {
                        await output.DisposeAsync();
                        File.Delete(part);
                        throw new HashMismatchException($"{file.Path}: источник {new Uri(url).Host} отдаёт больше, чем указано в манифесте", resumed);
                    }
                    await output.WriteAsync(buffer.AsMemory(0, read), ct);
                    onBytes(read);
                }
            }
        }

        if (new FileInfo(part).Length != file.Size)
            throw new IOException("файл скачан не полностью");
        if (await HashAsync(part, ct) != file.Sha256)
        {
            File.Delete(part);
            throw new HashMismatchException($"{file.Path}: содержимое с {new Uri(url).Host} не совпадает с контрольной суммой", resumed);
        }
        File.Move(part, target, overwrite: true);
    }

    private static async Task<string> HashAsync(string path, CancellationToken ct)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).ToLowerInvariant();
    }

    /// <summary>A folder inside the instance that is a link could lead the writes out of it, even though the path text looks fine.</summary>
    private static void EnsureNoLinks(string root, string target, string shown)
    {
        var folder = Path.GetDirectoryName(target);
        while (folder is not null && folder.Length > root.Length)
        {
            if (Directory.Exists(folder) && new DirectoryInfo(folder).Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new PackException($"Папка {Path.GetRelativePath(root, folder)} в сборке является ссылкой: {shown}");
            folder = Path.GetDirectoryName(folder);
        }
        if (File.Exists(target) && new FileInfo(target).Attributes.HasFlag(FileAttributes.ReparsePoint))
            throw new PackException($"Файл {shown} является ссылкой");
    }

    private static void RemoveEmptyFolders(string root, string folder)
    {
        while (folder.Length > root.Length && Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
        {
            Directory.Delete(folder);
            folder = Path.GetDirectoryName(folder)!;
        }
    }

    private sealed class HashMismatchException(string message, bool wasResumed) : Exception(message)
    {
        public bool WasResumed { get; } = wasResumed;
    }
}
