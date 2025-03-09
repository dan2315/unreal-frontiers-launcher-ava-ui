using System;
using UnrealFrontiersLauncher;

public class PlayerProfileProvider
{
    private PlayerProfile? _currentProfile;
    private Action _onProfileChanged;

    public PlayerProfile GetProfile()
    {
        return _currentProfile;
    }

    public void SetProfile(PlayerProfile profile)
    {
        _currentProfile = profile;
        _onProfileChanged?.Invoke();
    }

    public void OnProfileUpdate(Action onProfileChanged)
    {
        _onProfileChanged = onProfileChanged;
    }
}
