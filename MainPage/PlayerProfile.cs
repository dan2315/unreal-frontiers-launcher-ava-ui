namespace UnrealFrontiersLauncher;

public class PlayerProfile
{
    private string _nickname;
    private bool _hasPassword;
    private bool _hasDiscord;
    public string Nickname => _nickname;
    public bool HasPassword => _hasPassword;
    public bool HasDiscord => _hasDiscord;
    public PlayerProfile(string nickname) {
        _nickname = nickname;
    }
    
    public static PlayerProfile Default => new PlayerProfile("DefaultPlayer"); 
}