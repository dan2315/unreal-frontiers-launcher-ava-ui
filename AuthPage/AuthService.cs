using System.Text.Json;
using System.Text;
using System.Net.Http;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnrealFrontiersLauncher.ApiResponses.Auth;

namespace UnrealFrontiersLauncher
{
    public class AuthService
    {
        private readonly HttpClientService _httpClient;
        private readonly TokenStorage _tokenStorage;

        public AuthService(HttpClientService httpClient)
        {
            _httpClient = httpClient;
            _tokenStorage = new TokenStorage();
        }

        public async Task<(bool Success, string Message)> CreateAccount(string login, string email, string password)
        {
            var payload = new { login, email, password };
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

        public async Task<(bool Success, string Error, string Login)> Login(string loginOrEmail, string password)
        {
            var payload = new Dictionary<String, String>()
            {
                {"login", loginOrEmail},
                {"password", password}
            };
            var content = new FormUrlEncodedContent(payload);
            var response = await _httpClient.PostAsync("/login", content);
            
            // var response = await new HttpClient().PostAsync("/login", content);

            var responseMessage = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                AuthResponse? authResponse = JsonSerializer.Deserialize<AuthResponse>(responseMessage);

                _tokenStorage.SaveTokens(authResponse.AccessToken);
                return (true, "Login successful.", loginOrEmail);
            }

            return (false, responseMessage, null);
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

        public async Task<User?> TryGetUser() 
        {
            string accessToken = _tokenStorage.LoadToken();
            
            if (string.IsNullOrEmpty(accessToken)) return null;
            
            _httpClient.SetAuthHeader(accessToken);
            var accessResponse = await _httpClient.PostAsync("/api/user", null);

            if (!accessResponse.IsSuccessStatusCode) return null;
            
            
            var responseMessage = await accessResponse.Content.ReadAsStringAsync();
            User? user = JsonSerializer.Deserialize<User>(responseMessage);

            return user;

        }
        public async Task<(bool Success, string Message)> DiscordAuth()
        {
            var url = "auth/discord/redirect";

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = $"{AppSettings.ApiServerURL}{url}",
                UseShellExecute = true
            });

            // Indicate that the method triggered the process successfully
            return (true, "Discord authentication initiated. Follow the browser instructions.");
        }

        public async Task<(bool success, string? accessToken, string? login)> CompleteDiscordAuthSession(string state, string login) 
        {
            var payload = new {state, login};

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/complete_discord_auth_session", content);

            if (response.IsSuccessStatusCode) {
                var responseContent = await response.Content.ReadAsStringAsync();
                var responseObject = JsonSerializer.Deserialize<JsonElement>(responseContent);
                var recievedLogin = responseObject.GetProperty("login").GetString();
                var accessToken = responseObject.GetProperty("accessToken").GetString();

                _tokenStorage.SaveTokens(accessToken);
                return (true, accessToken, recievedLogin);
            }
            else
            {
                Console.WriteLine($"Error: {response.StatusCode}");
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error details: {errorContent}");
                return (false, null, null);
            }
        }
        
        public void GrantAccessAndProceed(string accessToken, NavigationService navigationService)
        {
            GrantAccess(accessToken);
            navigationService.NavigateTo<MainPage>();
        }

        public void GrantAccess(string? accessToken)
        {
            if (accessToken != null)
            {
                _tokenStorage.SaveTokens(accessToken);
            }
        }
    }
}
