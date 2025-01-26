using System.Text.Json.Serialization;

namespace UnrealFrontiersLauncher;

public class User
{
    private string _UUID;
    private string? _login;
    private string _email;
    private string? _discordId;
    private string? _googleId;
    private string? _steamId;
    
    public string UUID => _UUID;
    public string? Login => _login;
    public string Email => _email;
    public string? DiscordId => _discordId;
    public string? GoogleId => _googleId;
    public string? SteamId => _steamId;

    [JsonConstructor]
    public User(string id, string? login, string email, string? discord_id, string? google_id, string? steam_id)
    {
        _UUID = id;
        _login = login;
        _email = email;
        _discordId = discord_id;
        _googleId = google_id;
        _steamId = steam_id;
    }
    
    public static User Default => new User(null, "OfflinePlayer", null, null, null, null);
}