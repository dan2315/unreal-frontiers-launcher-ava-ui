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
    
    private string? _csrfToken = null;
    public string GetCsrfToken
    {
        get
        {
            if (_csrfToken == null)
            {
                var response = _httpClient.GetAsync($"{AppSettings.ServerURL}/sanctum/csrf-cookie").Result;
                response.EnsureSuccessStatusCode();
                
                var cookies = response.Headers.GetValues("Set-Cookie");
                foreach (var cookie in cookies)
                {
                    if (!cookie.Contains("XSRF-TOKEN")) continue;
                    
                    _csrfToken = WebUtility.UrlDecode(cookie.Split(';')[0].Split('=')[1]);
                    break;
                }
            }
            
            return _csrfToken;
        }
    }

    public HttpClientService()
    {
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(AppSettings.ServerURL);
        _httpClient.DefaultRequestHeaders.Add("X-CSRF-Token", GetCsrfToken);
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

        await PrePostRequestMiddlewareAsync(request);

        var response = await _httpClient.SendAsync(request);

        await AfterResponseMiddlewareAsync(response);

        return response;
    }


    private async Task PrePostRequestMiddlewareAsync(HttpRequestMessage request)
    {
        if (!_httpClient.DefaultRequestHeaders.Contains("X-CSRF-TOKEN"))
        {
            FlushCsrf();
            var csrf = GetCsrfToken;
            _httpClient.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf);
        }
    }

    private Task AfterResponseMiddlewareAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return Task.CompletedTask;
        
        Console.WriteLine($"Request failed with status code {response.StatusCode}");

        if (response.StatusCode != (System.Net.HttpStatusCode)419) return Task.CompletedTask;
        
        Console.Error.WriteLine("CSRF token expired or invalid.");
        FlushCsrf();

        return Task.CompletedTask;
    }
    
    private void FlushCsrf()
    {
        _csrfToken = null;
        _httpClient.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
    }
}