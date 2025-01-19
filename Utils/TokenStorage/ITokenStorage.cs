namespace UnrealFrontiersLauncher.TokenStorage;

public interface ITokenStorage
{
    void SaveTokens(string? accessToken, string? refreshToken);
    (string accessToken, string refreshToken) LoadTokens();
}