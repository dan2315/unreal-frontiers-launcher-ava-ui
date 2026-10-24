using System;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Threading;
using System.Runtime.CompilerServices;

namespace UnrealFrontiersLauncher;

/// <summary>
/// The sign-in form. Everything else about an account (registration, a forgotten password, a missing letter, linking)
/// is done on the site, so this page only signs in and sends the player there.
/// </summary>
public class AuthPageViewModel : INotifyPropertyChanged
{
    public AuthPage PageReference { get; set; }

    private readonly AuthService _authService;
    private readonly PlayerProfileProvider _playerProfileProvider;
    private readonly NavigationService _navigationService;
    private readonly PopupService _popupService;

    private string _nickname = "";
    private string _password = "";
    private string? _authError;
    private int _authErrorSeq;
    private bool _isEmailNotVerified;
    private bool _isSigningIn;

    /// <summary>Nickname or email.</summary>
    public string Nickname
    {
        get => _nickname;
        set { _nickname = value ?? ""; OnPropertyChanged(); }
    }

    /// <summary>Not checked against the password rules: older accounts may have other passwords.</summary>
    public string Password
    {
        get => _password;
        set { _password = value ?? ""; OnPropertyChanged(); }
    }

    public string? AuthError => string.IsNullOrEmpty(_authError) ? null : _authError;

    /// <summary>Grows with every error shown, even the same text twice: the page restarts the flash-and-fade animation on each step.</summary>
    public int AuthErrorSeq
    {
        get => _authErrorSeq;
        private set { _authErrorSeq = value; OnPropertyChanged(); }
    }

    private void ShowError(string message)
    {
        _authError = message;
        OnPropertyChanged(nameof(AuthError));
        AuthErrorSeq++;
    }

    /// <summary>The account exists but its email is not confirmed: explain what to do instead of the form.</summary>
    public bool IsEmailNotVerified
    {
        get => _isEmailNotVerified;
        private set
        {
            _isEmailNotVerified = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsFormVisible));
        }
    }

    public bool IsFormVisible => !_isEmailNotVerified;

    public const string EmailNotVerifiedHint =
        "Аккаунт есть, но почта не подтверждена. Найдите письмо от нас (проверьте «Спам»). " +
        "Если его нет, откройте сайт, введите тот же email и пароль, и мы предложим отправить письмо ещё раз.";

    public AuthPageViewModel(AuthService authService, PlayerProfileProvider playerProfileProvider, NavigationService navigationService, PopupService popupService)
    {
        _authService = authService;
        _playerProfileProvider = playerProfileProvider;
        _navigationService = navigationService;
        _popupService = popupService;
        SocialButtons.Add(new SocialButton(LoginFlow.Discord, "Discord", "/Assets/Images/svg/discord_icon.svg"));
        _ = LoadSocialProvidersAsync();
    }

    public async void AuthAction()
    {
        if (_isSigningIn) return;
        _isSigningIn = true;
        try
        {
            while (true)
            {
                var response = await _authService.Login(Nickname, Password);
                if (response.Success)
                {
                    _authService.GrantAccess(_playerProfileProvider, new AuthPage.AccessData { nickname = response.Nickname });
                    _navigationService.NavigateTo<MainPage>();
                    return;
                }

                if (response.Failure == LoginFailure.NoConnection)
                {
                    if (await OfflineMode.OfferAsync(_popupService))
                    {
                        OfflineMode.Enter(_playerProfileProvider);
                        _navigationService.NavigateTo<MainPage>();
                        return;
                    }
                    continue;
                }

                if (response.Failure == LoginFailure.EmailNotVerified)
                {
                    _authError = null;
                    OnPropertyChanged(nameof(AuthError));
                    IsEmailNotVerified = true;
                    return;
                }

                ShowError(response.Message);
                return;
            }
        }
        finally
        {
            _isSigningIn = false;
        }
    }

    public void BackToForm() => IsEmailNotVerified = false;

    public void OpenRegister() => OpenSite(SiteLinks.AuthUrl(AppStaticData.ServerURL, "register"));

    public void OpenForgotPassword() => OpenSite(SiteLinks.AuthUrl(AppStaticData.ServerURL, "forgot"));

    private void OpenSite(string url)
    {
        if (!SiteLinks.OpenInBrowser(url))
            ShowError("Не удалось открыть браузер");
    }

    /// <summary>The sign-in buttons under the form: Discord always, then the providers the backend has switched on.</summary>
    public ObservableCollection<SocialButton> SocialButtons { get; } = new();

    private async Task LoadSocialProvidersAsync()
    {
        foreach (var provider in await _authService.GetOAuthProvidersAsync())
            SocialButtons.Add(new SocialButton(provider.Name, TitleFor(provider), IconFor(provider.Name)));
    }

    // The buttons are written in Latin like Discord ("Yandex", not "Яндекс"); an unknown provider keeps the title the backend sends.
    private static string TitleFor(OAuthProvider provider) => provider.Name switch
    {
        "google" => "Google",
        "yandex" => "Yandex",
        _ => provider.Title
    };

    private static string? IconFor(string provider) => provider switch
    {
        "google" => "/Assets/Images/svg/google_icon.svg",
        "yandex" => "/Assets/Images/svg/yandex_icon.svg",
        _ => null
    };

    public async void StartSocial(string provider)
    {
        if (provider == LoginFlow.Discord)
        {
            await _authService.DiscordAuth(null);
            return;
        }

        try
        {
            _authService.StartOAuth(provider);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException)
        {
            ShowError("Не удалось открыть браузер");
            return;
        }

        // The browser may never come back: the sign-in was cancelled, or the email already belongs to an account.
        LoginFlow.Begin(provider, () => Dispatcher.UIThread.Post(async () =>
        {
            if (LoginFlow.Provider == provider)
                await _popupService.ShowOkDialogAsync("Вход", AuthMessages.OAuthTimeoutHint);
        }));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>A sign-in button: the provider's name for the backend, its title, and an icon path when there is one.</summary>
public sealed record SocialButton(string Name, string Title, string? IconPath)
{
    public bool HasIcon => IconPath != null;
}
