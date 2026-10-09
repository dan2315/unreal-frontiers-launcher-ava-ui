using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Velopack;
using Velopack.Logging;
using Velopack.Sources;

namespace UnrealFrontiersLauncher;

public class ServerUpdateSource : IUpdateSource
{
    private const string LauncherId = "launcher";

    public async Task<VelopackAssetFeed> GetReleaseFeed(
        IVelopackLogger logger,
        string? appId,
        string channel,
        Guid? stagingId = null,
        VelopackAsset? latestLocalRelease = null)
    {
        var url = AppStaticData.GetFileDownloadPath($"releases.{channel}.json", LauncherId);
        var json = await SharedHttp.Api.GetStringAsync(url);
        return VelopackAssetFeed.FromJson(json);
    }

    public async Task DownloadReleaseEntry(
        IVelopackLogger logger,
        VelopackAsset releaseEntry,
        string localFile,
        Action<int> progress,
        CancellationToken cancelToken = default)
    {
        var url = AppStaticData.GetFileDownloadPath(releaseEntry.FileName, LauncherId);

        using var response = await SharedHttp.Transfer.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancelToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? releaseEntry.Size;
        await using var source = await response.Content.ReadAsStreamAsync(cancelToken);
        await using var destination = File.Create(localFile);

        var buffer = new byte[81920];
        long copied = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancelToken)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read), cancelToken);
            copied += read;
            if (totalBytes > 0) progress((int)(copied * 100 / totalBytes));
        }

        progress(100);
    }
}
