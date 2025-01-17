using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using UnrealFrontiersLauncher.ViewModels;

namespace UnrealFrontiersLauncher;
public partial class MainWindow : Window 
{
    public NavigationService _navigationService;
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();

        PointerPressed += (s, e) => BeginMoveDrag(e);
    }

    public void Init(NavigationService navigationService) 
    {
        _navigationService = navigationService;
    }

    public void GetToPreviousPage(object? sender, RoutedEventArgs args)
    {
        _navigationService.GetBack();
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}