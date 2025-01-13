using UnrealFrontiersLauncher;

public class PlayerProfileProvider
{
    private PlayerProfile? _currentProfile;

    public PlayerProfile GetProfile()
    {
        return _currentProfile;
    }

    public void SetProfile(PlayerProfile profile)
    {
        _currentProfile = profile;
    }
}
