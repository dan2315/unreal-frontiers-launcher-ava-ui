using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace UnrealFrontiersLauncher;

public class App : Application
{
    public static new App Current => (App)Application.Current;

    public IServiceProvider Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this); 
    }

    public void IncludeServices(IServiceProvider serviceProvider)
    {
        Services = serviceProvider;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            CrashLog.InstallUiThreadHandler();

            Services.GetRequiredService<PopupService>();

            var navigation = Services.GetRequiredService<NavigationService>();

            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.Init(navigation);
            desktop.MainWindow = mainWindow;

            var autoupdatePage = Services.GetRequiredService<AutoupdatePage>();
            navigation.RegisterPage(autoupdatePage);
            navigation.NavigateTo(autoupdatePage);

            Dispatcher.UIThread.Post(() => _ = StartAsync(navigation, autoupdatePage), DispatcherPriority.Background);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task StartAsync(NavigationService navigation, AutoupdatePage autoupdatePage)
    {
        _ = Services.GetRequiredService<DeepLinkHandler>().InitializeAsync(Environment.GetCommandLineArgs());

        var authPage = Services.GetRequiredService<AuthPage>();
        navigation.RegisterPage(authPage);
        var mainPage = Services.GetRequiredService<MainPage>();
        navigation.RegisterPage(mainPage);

        Autoupdater.OnSetStatus?.Invoke("Connecting to server...");
        var backendReachable = await BackendConnectionDetector.ConnectionStatus();

        while (!backendReachable)
        {
            if (await OfflineMode.OfferAsync(Services.GetRequiredService<PopupService>()))
            {
                OfflineMode.Enter(Services.GetRequiredService<PlayerProfileProvider>());
                navigation.NavigateTo(mainPage);
                return;
            }

            Autoupdater.OnSetStatus?.Invoke("Connecting to server...");
            backendReachable = await BackendConnectionDetector.ConnectionStatus();
        }

        autoupdatePage.TryAutoupdateOrOtherwise(() => _ = authPage.TryAutoAuth());
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
