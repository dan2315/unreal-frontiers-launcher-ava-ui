using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using ReactiveUI;

namespace UnrealFrontiersLauncher;

public class AuthPageViewModel : INotifyPropertyChanged
{
    public AuthPage PageReference { get; set; }
    public ValidatableEntry NicknameEntry { get; }
    public ValidatableEntry EmailEntry { get; }
    public ValidatableEntry PasswordEntry { get; }
    public ValidatableEntry RepeatPasswordEntry {get; }
    private bool _isRegisterMode;

    private AuthService _authService;
    private readonly PlayerProfileProvider _playerProfileProvider;
    private NavigationService _navigationService;

    private string _authError;

    public bool IsRegisterMode
    {
        get => _isRegisterMode;
        set
        {
            _isRegisterMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PageTitle));
            OnPropertyChanged(nameof(ActionButtonText));
            OnPropertyChanged(nameof(ToggleMessage));
            OnPropertyChanged(nameof(ToggleButtonText));
            OnPropertyChanged(nameof(IsLoginMode));
            NicknameEntry.OnPropertyChanged(nameof(NicknameEntry.IsVisible));
            EmailEntry.OnPropertyChanged(nameof(EmailEntry.IsVisible));
            PasswordEntry.OnPropertyChanged(nameof(PasswordEntry.IsVisible));
            RepeatPasswordEntry.OnPropertyChanged(nameof(RepeatPasswordEntry.IsVisible));
        }
    }

    public string AuthError 
    {
        get => _authError;
        set {
            if (_authError != value) {
                _authError = value;
                OnPropertyChanged(nameof(AuthError));
                OnPropertyChanged(nameof(AuthErrorVisible));
            }       
        }
    }

    public bool AuthErrorVisible => !String.IsNullOrEmpty(_authError);

    public bool IsLoginMode => !IsRegisterMode;
    public string PageTitle => IsRegisterMode ? "Register" : "Login";
    public string ActionButtonText => IsRegisterMode ? "Создать аккаунт" : "Войти";
    public string ToggleMessage => IsLoginMode ? "Нет аккаунта?" : "Аккаунт уже есть?";
    public string ToggleButtonText => IsRegisterMode ? "Login" : "Sign Up";

    public AuthPageViewModel(AuthService authService, PlayerProfileProvider playerProfileProvider, NavigationService navigationService)
    {
        _authService = authService;
        _playerProfileProvider = playerProfileProvider;
        _navigationService = navigationService;
        NicknameEntry = new ValidatableEntry(ValidateNickname, this);
        EmailEntry = new ValidatableEntry(ValidateEmail, this);
        PasswordEntry = new ValidatableEntry(ValidatePassword, this);
        RepeatPasswordEntry = new ValidatableEntry(ValidateRepeatPassword, this);
        IsRegisterMode = false;
    }

    // RelayCommand for Login or Register action
    public async void AuthAction()
    {
        if (IsRegisterMode)
        {
            NicknameEntry.Validate();
            EmailEntry.Validate();
            PasswordEntry.Validate();
            RepeatPasswordEntry.Validate();
            bool valid = NicknameEntry.IsValid && EmailEntry.IsValid && PasswordEntry.IsValid && RepeatPasswordEntry.IsValid;

            if (valid) {
                var response = await _authService.CreateAccount(NicknameEntry.Value, EmailEntry.Value, PasswordEntry.Value);
                PageReference.ActionButtonRef.Unlock();
                
                if(! response.Success) 
                {
                    AuthError = response.Message;
                }
                else 
                {
                    // TODO: await PageReference.DisplayAlert("Успех", "Вам на почту отправлено письмо для подтверждения аккаунта", "Оки-доки");
                }
            }
        }
        else if (IsLoginMode)
        {
            var response = await _authService.Login(NicknameEntry.Value, PasswordEntry.Value);
            PageReference.ActionButtonRef.Unlock();

            if (!response.Success)
            {
                AuthError = response.Message;
                Console.WriteLine(AuthError);
            }
            else
            {
                AuthPage.GrantAccess(_playerProfileProvider, new() { nickname = response.Nickname, accessToken = response.AccessToken, refreshToken = response.RefreshToken });
                _navigationService.NavigateTo<MainPage>();
            }
        }
    }

    public async void InitiateDiscordAuth(string? nickname)
    {
        PageReference.StartDiscordAuthButtonRef.Lock();
        if (nickname is string nicknameString)
        {
            await _authService.DiscordAuth(nicknameString);
        }
        else
        {
            await _authService.DiscordAuth(null);
        }
        PageReference.StartDiscordAuthButtonRef.Unlock();
    }

    public void ToggleView()
    {
        Console.WriteLine("Toggle Happened");
        IsRegisterMode = !IsRegisterMode;
    }

    public void InitiatePasswordRecovery()
    {
        //TODO: PageReference.DisplayAlert("Ой-ёй","Больше не забывай =- )", "Ну ладно :("); 
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public (bool IsValid, string Message) ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return (false, "Email cannot be empty.");

        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return (addr.Address == email, addr.Address == email ? "Email is valid." : "Invalid email format.");
        }
        catch
        {
            return (false, "Invalid email format.");
        }
    }

    public (bool IsValid, string Message) ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return (false, "Password cannot be empty.");

        if (password.Length < 8)
            return (false, "Password must be at least 8 characters long.");

        if (!password.Any(char.IsUpper))
            return (false, "Password must contain at least one uppercase letter.");

        if (!password.Any(char.IsLower))
            return (false, "Password must contain at least one lowercase letter.");

        if (!password.Any(char.IsDigit))
            return (false, "Password must contain at least one digit.");

        return (true, "Password is valid.");
    }

    public (bool IsValid, string Message) ValidateNickname(string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
            return (false, "Nickname cannot be empty.");

        if (nickname.Length < 3)
            return (false, "Nickname must be at least 3 characters long.");

        if (nickname.Length > 16)
            return (false, "Nickname must be no more than 16 characters long.");

        var regex = new System.Text.RegularExpressions.Regex("^[a-zA-Z0-9_]+$");
        if (!regex.IsMatch(nickname))
            return (false, "Nickname can only contain letters, numbers, and underscores.");

        return (true, "Nickname is valid.");
    }

    private (bool, string) ValidateRepeatPassword(string repeatedPassword)
    {
        if (string.IsNullOrWhiteSpace(repeatedPassword))
            return (false, "Поле не может быть пустым.");

        if (repeatedPassword != PasswordEntry.Value) 
            return (false, "Пароли не совпадают.");

        return (true, "Ахуэнчик");
    }
}
