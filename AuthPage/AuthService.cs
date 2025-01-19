using System.Text.Json;
using System.Text;
using System.Net.Http.Headers;
using System.Net.Http;
using System;
using System.Collections.Specialized;
using System.Threading.Tasks;
using UnrealFrontiersLauncher.TokenStorage;
#if WINDOWS
using UnrealFrontiersLauncher.TokenStorage.Windows;
#elif LINUX
using UnrealFrontiersLauncher.TokenStorage.Linux;
#endif

namespace UnrealFrontiersLauncher
{
    public class AuthService
    {
        private readonly HttpClient _httpClient;
        private readonly ITokenStorage _tokenStorage;

        public AuthService()
        {
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri(AppSettings.ServerURL);
#if WINDOWS
            _tokenStorage = new TokenStorageWindows();
#elif LINUX
            _tokenStorage = new TokenStorageLinux();
#endif
        }

        public async Task<(bool Success, string Message)> CreateAccount(string nickname, string email, string password)
        {
            var payload = new { nickname, email, password };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/create_account", content);

            var responseMessage = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return (true, "Account created successfully. Check your email to verify your account.");
            }
            else
            {
                return (false, responseMessage);
            }
        }

        public async Task<(bool Success, string Message, string AccessToken, string RefreshToken, string Nickname)> Login(string nicknameOrEmail, string password)
        {
            var payload = new { nickname = nicknameOrEmail, password };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/login", content);

            var responseMessage = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var data = JsonSerializer.Deserialize<JsonElement>(responseMessage);

                string nickname = data.GetProperty("nickname").GetString();
                string accessToken = data.GetProperty("accessToken").GetString();
                string refreshToken = data.GetProperty("refreshToken").GetString();

                _tokenStorage.SaveTokens(accessToken, refreshToken);
                return (true, "Login successful.", accessToken, refreshToken, nickname);
            }
            else
            {
                return (false, responseMessage, null, null, null);
            }
        }

        public async Task<(bool Success, string Message, bool IsVerified)> CheckAccountVerified(string email)
        {
            var response = await _httpClient.GetAsync($"/check_verification?email={Uri.EscapeDataString(email)}");
            var responseMessage = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var data = JsonSerializer.Deserialize<JsonElement>(responseMessage);
                bool isVerified = data.GetProperty("verified").GetBoolean();
                string message = data.GetProperty("message").GetString();
                return (true, message, isVerified);
            }
            else
            {
                return (false, responseMessage, false);
            }
        }


        public async Task<(bool Success, string Message)> RequestPasswordReset(string email)
        {
            var payload = new { email };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/request_password_reset", content);

            var responseMessage = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return (true, "Password reset request sent. Check your email for further instructions.");
            }
            else
            {
                return (false, responseMessage);
            }
        }

        public async Task<(bool, string?)> TryAuthWithTokens() 
        {
            (string accessToken, string refreshToken) = _tokenStorage.LoadTokens();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var accessResponse = await _httpClient.GetAsync("request_access");

            if (accessResponse.IsSuccessStatusCode)
            {
                var responseMessage = await accessResponse.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<JsonElement>(responseMessage);
                var access = data.GetProperty("access").GetBoolean();
                var nickname = data.GetProperty("nickname").GetString();

                return (access, nickname);
            } 

            var payload = new { refreshToken };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var refreshResponse = await _httpClient.PostAsync("/refresh_tokens", content);

            if (refreshResponse.IsSuccessStatusCode) 
            {
                var data = JsonSerializer.Deserialize<JsonElement>(await refreshResponse.Content.ReadAsStringAsync());
                var newAccessToken = data.GetProperty("accessToken").GetString();
                var nickname = data.GetProperty("nickname").GetString();
                _tokenStorage.SaveTokens(newAccessToken, refreshToken);

                return(true, nickname);
            }

            return (false, null);
        }
        public async Task<(bool Success, string Message)> DiscordAuth(string? nickname)
        {
            var url = "auth_discord";
            if (!string.IsNullOrEmpty(nickname))
            {
                url += $"?nickname={Uri.EscapeDataString(nickname)}";
            }

            // Direct the client to open this URL in the default browser for Discord OAuth
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = $"{_httpClient.BaseAddress}{url}",
                UseShellExecute = true
            });

            // Indicate that the method triggered the process successfully
            return (true, "Discord authentication initiated. Follow the browser instructions.");
        }

        public async Task<(bool success, string? accessToken, string? refreshToken, string? nickname)> CompleteDiscordAuthSession(string state, string nickname) 
        {
            var payload = new {state, nickname};

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/complete_discord_auth_session", content);

            if (response.IsSuccessStatusCode) {
                var responseContent = await response.Content.ReadAsStringAsync();
                var responseObject = JsonSerializer.Deserialize<JsonElement>(responseContent);
                var recievedNickname = responseObject.GetProperty("nickname").GetString();
                var accessToken = responseObject.GetProperty("accessToken").GetString();
                var refreshToken = responseObject.GetProperty("refreshToken").GetString();

                _tokenStorage.SaveTokens(accessToken, refreshToken);
                return (true, accessToken, refreshToken, recievedNickname);
            }
            else
            {
                Console.WriteLine($"Error: {response.StatusCode}");
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error details: {errorContent}");
                return (false, null, null, null);
            }
        }
        
        public void GrantAccessAndProceed(PlayerProfileProvider profileProvider, NavigationService navigationService, NameValueCollection queryParams)
        {
            var parameters = ReadAuthParams(queryParams);
            GrantAccess(profileProvider, parameters);
            navigationService.NavigateTo<MainPage>();
        }

        public void GrantAccess(PlayerProfileProvider profileProvider, AuthPage.AccessData parameters)
        {
            if (parameters.accessToken != null && parameters.refreshToken != null)
            {
                _tokenStorage.SaveTokens(parameters.accessToken, parameters.refreshToken);
            }

            var profile = new PlayerProfile(parameters.nickname);
            profileProvider.SetProfile(profile);
        }
        
        public AuthPage.AccessData ReadAuthParams(NameValueCollection queryParams)
        {
            return new()
            {
                nickname = queryParams["nickname"],
                accessToken = queryParams["accessToken"],
                refreshToken = queryParams["refreshToken"]
            };
        }
    }
}
