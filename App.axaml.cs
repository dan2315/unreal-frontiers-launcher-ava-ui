using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace UnrealFrontiersLauncher;

public class App : Application
{
    public static new App Current => (App)Application.Current;

    public IServiceProvider Services { get; private set; }

    private bool _internetConnection = false;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this); 
    }

    public void IncludeServices(IServiceProvider serviceProvider)
    {
        Services = serviceProvider;
    }

    public async override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            Services.GetRequiredService<PopupService>();

            var navigation = Services.GetRequiredService<NavigationService>();

            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.Init(navigation);
            desktop.MainWindow = mainWindow;
            
            var authPage = Services.GetRequiredService<AuthPage>();
            navigation.RegisterPage(authPage);
            var mainPage = Services.GetRequiredService<MainPage>();
            navigation.RegisterPage(mainPage);
            var entryModal = Services.GetRequiredService<EntryModal>(); 
            navigation.RegisterPage(entryModal);
            var autoupdatePage = Services.GetRequiredService<AutoupdatePage>();
            navigation.RegisterPage(autoupdatePage);
            
            _ = Services.GetRequiredService<DeepLinkHandler>().InitializeAsync(Environment.GetCommandLineArgs());
            
            _internetConnection = await BackendConnectionDetector.ConnectionStatus();

            if (!_internetConnection)
            {
                Services.GetRequiredService<UserProvider>().SetProfile(User.Default);
                navigation.NavigateTo(mainPage);
                return;
            }

            navigation.NavigateTo(autoupdatePage);
            autoupdatePage.TryAutoupdate();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
