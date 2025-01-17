using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace UnrealFrontiersLauncher;
   
public static class BackendConnectionDetector
{
    public static async Task<bool> ConnectionStatus(string testUrl = "http://www.google.com")
    {
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            using var response = await client.GetAsync(testUrl);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
