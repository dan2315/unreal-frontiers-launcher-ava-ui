using System;
using System.Threading;
using System.Threading.Tasks;

namespace UnrealFrontiersLauncher;

public static class BackendConnectionDetector
{
    public static async Task<bool> ConnectionStatus(string? testUrl = null)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var response = await SharedHttp.Api.GetAsync(testUrl ?? AppStaticData.ServerURL, cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
