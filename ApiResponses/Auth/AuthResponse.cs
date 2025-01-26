using System.Text.Json.Serialization;

namespace UnrealFrontiersLauncher.ApiResponses.Auth;

public class AuthResponse
{
    private string _accessToken;
    private int _expiresIn;
    private string _type;

    public string AccessToken => _accessToken;
    public int ExpiresIn => _expiresIn;

    [JsonConstructor]
    public AuthResponse(string access_token, int expires_in, string token_type)
    {
        _accessToken = access_token;
        _expiresIn = expires_in;
        _type = token_type;
    }
}