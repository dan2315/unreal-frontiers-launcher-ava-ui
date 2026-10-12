using System.Text.Json;
using System.Text;
using System.Net.Http.Headers;
using System.Net.Http;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading.Tasks;
using UnrealFrontiersLauncher.ApiResponses.Auth;

namespace UnrealFrontiersLauncher
{
    public class AuthService
    {
        private readonly HttpClientService _httpClient;
        private readonly TokenStorage _tokenStorage;
        private string _accessToken = "";

        public AuthService(HttpClientService httpClient)
        {
            _httpClient = httpClient;
            _tokenStorage = new TokenStorage();
            var tokens = _tokenStorage.LoadTokens();
            SetAccessToken(tokens.accessToken);
        }

        public async Task<(bool Success, string Message)> CreateAccount(string nickname, string email, string password)
        {
            var payload = new { nickname, email, password };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/auth/create_account", content);

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

        public async Task<(bool Success, string Message, string? AccessToken, string? RefreshToken, string? Nickname)> Login(string nicknameOrEmail, string password)
        {
            var payload = new { nicknameOrEmail, password };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/auth/login", content);

            var responseMessage = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var data = JsonSerializer.Deserialize<JsonElement>(responseMessage);

                string nickname = data.GetProperty("nickname").GetString()!;
                string accessToken = data.GetProperty("accessToken").GetString()!;
                string refreshToken = data.GetProperty("refreshToken").GetString()!;

                ApplyTokens(accessToken, refreshToken);
                return (true, "Login successful.", accessToken, refreshToken, nickname);
            }
            else
            {
                return (false, responseMessage, null, null, null);
            }
        }

        public async Task<(bool Success, string Message, bool IsVerified)> CheckAccountVerified(string email)
        {
            var response = await _httpClient.GetAsync($"/auth/check_verification?email={Uri.EscapeDataString(email)}");
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
            var response = await _httpClient.PostAsync("/auth/request_password_reset", content);

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
            if (accessToken.Length == 0 && refreshToken.Length == 0) return (false, null);

            try
            {
                var accessResponse = await _httpClient.GetAsync("/auth/request_access");

                if (accessResponse.IsSuccessStatusCode)
                {
                    var data = JsonSerializer.Deserialize<JsonElement>(await accessResponse.Content.ReadAsStringAsync());
                    return (data.GetProperty("access").GetBoolean(), data.GetProperty("nickname").GetString());
                }

                var payload = new { refreshToken };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var refreshResponse = await _httpClient.PostAsync("/auth/refresh_tokens", content);

                if (refreshResponse.IsSuccessStatusCode)
                {
                    var data = JsonSerializer.Deserialize<JsonElement>(await refreshResponse.Content.ReadAsStringAsync());
                    var newAccessToken = data.GetProperty("accessToken").GetString()!;
                    var newRefreshToken = data.TryGetProperty("refreshToken", out var refreshElement)
                        ? refreshElement.GetString() ?? refreshToken
                        : refreshToken;
                    ApplyTokens(newAccessToken, newRefreshToken);

                    return (true, data.GetProperty("nickname").GetString());
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
            {
                Console.WriteLine($"Token authentication failed: {ex.Message}");
            }

            return (false, null);
        }

        public async Task<string?> GetFreshAccessTokenAsync()
        {
            await TryAuthWithTokens();
            return _accessToken.Length > 0 ? _accessToken : null;
        }

        public void Logout()
        {
            _tokenStorage.DeleteTokens();
            SetAccessToken("");
        }

        private void ApplyTokens(string accessToken, string refreshToken)
        {
            _tokenStorage.SaveTokens(accessToken, refreshToken);
            SetAccessToken(accessToken);
        }

        private void SetAccessToken(string accessToken)
        {
            _accessToken = accessToken;
            _httpClient.SetAuthHeader(accessToken);
        }
        public Task<(bool Success, string Message)> DiscordAuth(string? nickname)
        {
            var url = "/auth/auth_discord";
            if (!string.IsNullOrEmpty(nickname))
            {
                url += $"?nickname={Uri.EscapeDataString(nickname)}";
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = $"{AppStaticData.ServerURL}{url}",
                UseShellExecute = true
            });

            // Indicate that the method triggered the process successfully
            return Task.FromResult((true, "Discord authentication initiated. Follow the browser instructions."));
        }

        public async Task<(bool success, string? accessToken, string? refreshToken, string? nickname)> CompleteDiscordAuthSession(string state, string nickname) 
        {
            var payload = new { sessionId = state, nickname};

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/auth/complete_discord_auth_session", content);

            if (response.IsSuccessStatusCode) {
                var responseContent = await response.Content.ReadAsStringAsync();
                var responseObject = JsonSerializer.Deserialize<JsonElement>(responseContent);
                var recievedNickname = responseObject.GetProperty("nickname").GetString();
                var accessToken = responseObject.GetProperty("accessToken").GetString()!;
                var refreshToken = responseObject.GetProperty("refreshToken").GetString()!;

                ApplyTokens(accessToken, refreshToken);
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

        public async Task<string> SendResetPasswordEmail(string email)
        {
            try
            {
                var payload = new { email };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("/auth/send_reset_password_email", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var responseObject = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    var message = responseObject.GetProperty("message").GetString()!;

                    return message;
                }
                else
                {
                    Console.WriteLine($"Error: {response.StatusCode}");
                    return "Не удалось отправить письмо";
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
            {
                Console.WriteLine($"Password reset request failed: {ex.Message}");
                return "Не удалось связаться с сервером. Проверьте соединение и попробуйте позже.";
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
                ApplyTokens(parameters.accessToken, parameters.refreshToken);
            }
            else if (parameters.accessToken != null)
            {
                SetAccessToken(parameters.accessToken);
            }

            profileProvider.SetProfile(new PlayerProfile(parameters.nickname ?? PlayerProfile.Default.Nickname));
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
