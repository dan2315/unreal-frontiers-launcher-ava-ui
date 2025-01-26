using System;
using UnrealFrontiersLauncher;

public class UserProvider
{
    private User? _currentUser;
    private Action _onProfileChanged;

    public User GetProfile()
    {
        return _currentUser;
    }

    public void SetProfile(User profile)
    {
        _currentUser = profile;
        _onProfileChanged?.Invoke();
    }

    public void Subscribe(Action onProfileChanged)
    {
        _onProfileChanged = onProfileChanged;
    }
}
