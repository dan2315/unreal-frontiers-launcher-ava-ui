using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace UnrealFrontiersLauncher;

public class HttpClientService
{
    private HttpClient _httpClient;
    public HttpClient Get => _httpClient;
    
    public HttpClientService()
    {
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(AppStaticData.ServerURL);
    }

    public void SetAuthHeader(string accessToken)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }
    
    public async Task<HttpResponseMessage> GetAsync(string url)
    {
        var response = await _httpClient.GetAsync(url);

        await AfterResponseMiddlewareAsync(response);

        return response;
    }

    public async Task<HttpResponseMessage> PostAsync(string url, HttpContent? content)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = content
        };

        var response = await _httpClient.SendAsync(request);

        await AfterResponseMiddlewareAsync(response);

        return response;
    }

    private Task AfterResponseMiddlewareAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return Task.CompletedTask;
        
        Console.WriteLine($"Request failed with status code {response.StatusCode}");

        return Task.CompletedTask;
    }
}