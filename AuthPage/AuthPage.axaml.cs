using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;

namespace UnrealFrontiersLauncher
{
    public partial class AuthPage : UserControl
    {
        private readonly AuthService _authService;
        private readonly PlayerProfileProvider _playerProfileProvider;
        private readonly NavigationService _navigationService;
        private readonly PopupService _popupService;

        public Button ActionButtonRef => ActionButton;

        public AuthPage(IServiceProvider serviceProvider)
        {
            _authService = serviceProvider.GetRequiredService<AuthService>();
            _playerProfileProvider = serviceProvider.GetRequiredService<PlayerProfileProvider>();
            _navigationService = serviceProvider.GetRequiredService<NavigationService>();
            _popupService = serviceProvider.GetRequiredService<PopupService>();

            var viewModel = new AuthPageViewModel(_authService, _playerProfileProvider, _navigationService, _popupService);
            viewModel.PageReference = this;
            DataContext = viewModel;

            InitializeComponent();

            // Every new error (even the same text again) flashes and fades out; a running animation is replaced.
            viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(AuthPageViewModel.AuthErrorSeq)) return;

                _errorFlash?.Cancel();
                _errorFlash = new CancellationTokenSource();
                _ = UiAnimations.FlashFadeAsync(ErrorBox, _errorFlash.Token);
            };

            PasswordEye.IsCheckedChanged += (_, _) => ShowPassword(PasswordEye.IsChecked == true);

            // Enter in any field presses the main button. Tunnelling: the text box would otherwise take the key first.
            AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        }

        private CancellationTokenSource? _errorFlash;

        private void ShowPassword(bool show)
        {
            PasswordBox.RevealPassword = show;
            EyeShown.IsVisible = !show;
            EyeHidden.IsVisible = show;
            ToolTip.SetTip(PasswordEye, show ? "Скрыть пароль" : "Показать пароль");
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || e.Source is not TextBox || !ActionButton.IsEffectivelyVisible) return;

            e.Handled = true;
            ((AuthPageViewModel) DataContext!).AuthAction();
        }

        public async Task TryAutoAuth()
        {
            while (true)
            {
                (bool success, string? nickname) = await _authService.TryAuthWithTokens();
                if (success)
                {
                    _authService.GrantAccess(_playerProfileProvider, new() { nickname = nickname });
                    _navigationService.NavigateTo<MainPage>();
                    return;
                }

                if (!_authService.BackendUnreachable)
                {
                    _navigationService.NavigateTo<AuthPage>();
                    return;
                }

                if (await OfflineMode.OfferAsync(_popupService))
                {
                    OfflineMode.Enter(_playerProfileProvider);
                    _navigationService.NavigateTo<MainPage>();
                    return;
                }
            }
        }

        public void AuthAction(object? sender, RoutedEventArgs  e)
        {
            ((AuthPageViewModel) DataContext!).AuthAction();
        }

        public void OnSocialClicked(object? sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string provider })
                ((AuthPageViewModel) DataContext!).StartSocial(provider);
        }

        public void BackToForm(object? sender, RoutedEventArgs e)
        {
            ((AuthPageViewModel) DataContext!).BackToForm();
        }

        public void OpenRegister(object? sender, RoutedEventArgs e)
        {
            ((AuthPageViewModel) DataContext!).OpenRegister();
        }

        public void OpenForgotPassword(object? sender, PointerPressedEventArgs e)
        {
            ((AuthPageViewModel) DataContext!).OpenForgotPassword();
        }

        public struct AccessData
        {
            public string nickname;
            public string? accessToken;
            public string? refreshToken;
        }

        public static implicit operator Window(AuthPage v)
        {
            throw new NotImplementedException();
        }
    }
}
