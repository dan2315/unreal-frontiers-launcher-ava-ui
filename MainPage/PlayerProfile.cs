namespace UnrealFrontiersLauncher;

public class PlayerProfile
{
    private string _nickname;
    public string Nickname => _nickname;
    public PlayerProfile(string nickname) {
        _nickname = nickname;
    }
}