using System;
using System.ComponentModel;
using System.Linq;
using System.Net.Mail;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    private PopupService _popupService;

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
        get { 
            if (string.IsNullOrEmpty(_authError))
                return null;

            try
            {
                using var jsonDoc = JsonDocument.Parse(_authError);
                if (jsonDoc.RootElement.TryGetProperty("error", out var errorElement))
                {
                    return errorElement.GetString();
                }
                return "Unknown error format.";
            }
            catch (JsonException)
            {
                return "Invalid error format.";
            }
        }
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

    public AuthPageViewModel(AuthService authService, PlayerProfileProvider playerProfileProvider, NavigationService navigationService, PopupService popupService)
    {
        _authService = authService;
        _playerProfileProvider = playerProfileProvider;
        _navigationService = navigationService;
        _popupService = popupService;
        NicknameEntry = new ValidatableEntry(ValidateNickname, this);
        EmailEntry = new ValidatableEntry(ValidateEmail, this);
        PasswordEntry = new ValidatableEntry(ValidatePassword, this);
        RepeatPasswordEntry = new ValidatableEntry(ValidateRepeatPassword, this);
        IsRegisterMode = false;
    }
    
    public async void AuthAction()
    {
        if (IsRegisterMode)
        {
            NicknameEntry.Validate();
            EmailEntry.Validate();
            PasswordEntry.Validate();
            RepeatPasswordEntry.Validate();
            bool valid = NicknameEntry.IsValid && EmailEntry.IsValid && PasswordEntry.IsValid && RepeatPasswordEntry.IsValid;

            if (!valid) return;
            var response = await _authService.CreateAccount(NicknameEntry.Value, EmailEntry.Value, PasswordEntry.Value);
            PageReference.ActionButtonRef.IsEnabled = true;
                
            if(!response.Success) 
            {
                AuthError = response.Message;
                return;
            }

            _popupService.ShowOkDialogAsync("Успех",
                "Вам на почту отправлено письмо для подтверждения аккаунта");
        }
        else if (IsLoginMode)
        {
            var response = await _authService.Login(NicknameEntry.Value, PasswordEntry.Value);
            PageReference.ActionButtonRef.IsEnabled = true;

            if (!response.Success)
            {
                AuthError = response.Message;
                Console.WriteLine(AuthError);
                return;
            }

            _authService.GrantAccess(_playerProfileProvider,
                new() { nickname = response.Nickname, accessToken = response.AccessToken });
            _navigationService.NavigateTo<MainPage>();
        }
    }

    public async void InitiateDiscordAuth(string? nickname)
    {

        await _authService.DiscordAuth();
        PageReference.StartDiscordAuthButtonRef.IsEnabled = true;
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
            return (false, "Почта не может быть пустой.");

        string emailRegex = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        if (!Regex.IsMatch(email, emailRegex, RegexOptions.IgnoreCase))
        {
            return (false, "Неверный формат почты.");
        }

        return (true, "");
    }

    public (bool IsValid, string Message) ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return (false, "Ну придумай пароль.");

        if (password.Length < 8)
            return (false, "Пароль должен быть минимум 8 символов.");

        if (!password.Any(char.IsUpper))
            return (false, "Добавь большую букву.");

        if (!password.Any(char.IsLower))
            return (false, "Добавь маленькую букву.");

        if (!password.Any(char.IsDigit))
            return (false, "Добавь цифру.");

        return (true, "");
    }

    public (bool IsValid, string Message) ValidateNickname(string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
            return (false, "А как ты будешь играть без имени?");

        if (nickname.Length < 3)
            return (false, "Минимум 3 символа.");

        if (nickname.Length > 16)
            return (false, "Максимум 16 символов.");

        var regex = new System.Text.RegularExpressions.Regex("^[a-zA-Z0-9_]+$");
        if (!regex.IsMatch(nickname))
            return (false, "Напрудил каких-то не тех буковок.");

        return (true, "");
    }

    private (bool, string) ValidateRepeatPassword(string repeatedPassword)
    {
        if (string.IsNullOrWhiteSpace(repeatedPassword))
            return (false, "Поле не может быть пустым.");

        if (repeatedPassword != PasswordEntry.Value) 
            return (false, "Пароли не совпадают.");

        return (true, "");
    }
}
