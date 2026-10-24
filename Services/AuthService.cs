using System.Text.Json;
using System.Text;
using System.Net.Http.Headers;
using System.Net;
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

        public async Task<(bool Success, string Message, string? AccessToken, string? RefreshToken, string? Nickname, LoginFailure Failure)> Login(string nicknameOrEmail, string password)
        {
            var payload = new { nicknameOrEmail, password };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsync("/auth/login", content);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                return (false, AuthMessages.NoConnection, null, null, null, LoginFailure.NoConnection);
            }

            var responseMessage = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var data = JsonSerializer.Deserialize<JsonElement>(responseMessage);

                string nickname = data.GetProperty("nickname").GetString()!;
                string accessToken = data.GetProperty("accessToken").GetString()!;
                string refreshToken = data.GetProperty("refreshToken").GetString()!;

                ApplyTokens(accessToken, refreshToken);
                return (true, "Login successful.", accessToken, refreshToken, nickname, LoginFailure.None);
            }
            else
            {
                return (false, AuthMessages.Describe(response.StatusCode, responseMessage), null, null, null, AuthResponses.LoginFailureOf(response.StatusCode, responseMessage));
            }
        }

        /// <summary>The last token check got no answer from the backend (as opposed to an answer that refused the tokens).</summary>
        public bool BackendUnreachable { get; private set; }

        public async Task<(bool, string?)> TryAuthWithTokens()
        {
            BackendUnreachable = false;
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
                BackendUnreachable = ex is HttpRequestException or TaskCanceledException;
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

            LoginFlow.Begin(LoginFlow.Discord);

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

        /// <summary>The providers to show next to Discord. None when the backend has none enabled or cannot be reached.</summary>
        public async Task<IReadOnlyList<OAuthProvider>> GetOAuthProvidersAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/auth/oauth/providers");
                return response.IsSuccessStatusCode ? OAuth.ParseProviders(await response.Content.ReadAsStringAsync()) : [];
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                Console.WriteLine($"OAuth providers unavailable: {e.Message}");
                return [];
            }
        }

        /// <summary>Opens the provider's sign-in in the system browser; the player returns to the launcher through uflauncher://.</summary>
        public void StartOAuth(string provider)
        {
            if (!OAuth.IsValidName(provider)) throw new ArgumentException("Not a provider name.", nameof(provider));

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = OAuth.StartUrl(AppStaticData.ServerURL, provider),
                UseShellExecute = true
            });
        }

        /// <summary>A new player's second step: the nickname for the registration session the callback gave.</summary>
        public async Task<(bool Success, string Message, string? Code, string? Nickname)> CompleteOAuthSession(string sessionId, string nickname)
        {
            var payload = new { sessionId, nickname };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsync("/auth/oauth/complete_session", content);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                return (false, AuthMessages.NoConnection, null, null);
            }

            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                return (false, AuthMessages.Describe(response.StatusCode, body), AuthResponses.ErrorCode(body), null);

            try
            {
                var data = JsonSerializer.Deserialize<JsonElement>(body);
                ApplyTokens(data.GetProperty("accessToken").GetString()!, data.GetProperty("refreshToken").GetString()!);
                return (true, "", null, data.GetProperty("nickname").GetString());
            }
            catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                return (false, AuthMessages.ForStatus(HttpStatusCode.InternalServerError), null, null);
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

            OfflineMode.Leave();
            if (parameters.nickname != null) LastPlayer.Save(parameters.nickname);
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
