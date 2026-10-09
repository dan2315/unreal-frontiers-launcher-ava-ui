using System;
using System.Text.Json;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;

namespace UnrealFrontiersLauncher
{
    public class ServerService
    {
        private readonly HttpClientService _httpClient;

        public ServerService(HttpClientService httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<(bool Success, int CurrentPlayers, int MaxPlayers)> GetPlayerCount(string serverId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/server-management/player-count/{serverId}");
                var responseMessage = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var data = JsonSerializer.Deserialize<JsonElement>(responseMessage);
                    int currentPlayers = data.GetProperty("playerCount").GetInt32();
                    int maxPlayers = data.GetProperty("maxPlayers").GetInt32();
                    return (true, currentPlayers, maxPlayers);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed fetching player count for {serverId}: {e.Message}");
            }

            return (false, 0, 0);
        }
    }
} 