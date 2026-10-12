using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using UnrealFrontiersLauncher.ViewModels;

namespace UnrealFrontiersLauncher;
public partial class MainWindow : Window 
{
    public NavigationService _navigationService;
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();

        if (AppStaticData.IsTestMode)
        {
            Title = $"Unreal Frontiers Launcher DEV - {AppStaticData.TestBanner}";
            TitleText.Text = $"Unreal Frontiers Launcher DEV  ·  {AppStaticData.TestBanner}";
            TitleText.Foreground = new SolidColorBrush(Color.Parse("#FFB347"));
        }

    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            OnMaximizeClick(sender, e);
            return;
        }
        BeginMoveDrag(e);
    }

    public void Init(NavigationService navigationService) 
    {
        _navigationService = navigationService;
    }

    public void GetToAuthPage(object? sender, RoutedEventArgs args)
    {
        if (_navigationService.CurrentContent is AuthPage authPage)
        {
            Close();
        }
        else
        {
            _navigationService.NavigateTo<AuthPage>();
        }
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}