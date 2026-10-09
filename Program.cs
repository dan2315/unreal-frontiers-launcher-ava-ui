using System;
using Avalonia;
using Avalonia.ReactiveUI;
using Avalonia.Svg.Skia;
using Microsoft.Extensions.DependencyInjection;
using UnrealFrontiersLauncher.Sentry;
using UnrealFrontiersLauncher.Windows;
using Velopack;

namespace UnrealFrontiersLauncher
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            VelopackApp.Build()
                .OnAfterInstallFastCallback(_ => UrlSchemeRegistration.Register())
                .OnAfterUpdateFastCallback(_ => UrlSchemeRegistration.Register())
                .OnBeforeUninstallFastCallback(_ => UrlSchemeRegistration.Unregister())
                .Run();

            LegacyDataMigration.MigrateFromInnoInstall();
            SentryIntegration.Initialize();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
        {
            var serviceProvider = ConfigureServices();
            
            GC.KeepAlive(typeof(SvgImageExtension).Assembly);
            GC.KeepAlive(typeof(Avalonia.Svg.Skia.Svg).Assembly);
            
            return AppBuilder.Configure<App>()
                             .UsePlatformDetect()
                             .LogToTrace()
                             .UseReactiveUI()
                             .AfterSetup(_ =>
                             {
                                 var app = App.Current;
                                 app.IncludeServices(serviceProvider);
                             });
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddSingleton<HttpClientService>();
            services.AddSingleton<AuthService>();
            services.AddSingleton<ServerService>();
            services.AddSingleton<GameLauncher>();
            services.AddSingleton<InstanceManager>();
            services.AddSingleton<PlayerProfileProvider>();
            
            services.AddSingleton<MainWindow>();
            services.AddSingleton<PopupService>(); 

            services.AddTransient<EntryModal>();
            services.AddSingleton<AutoupdatePage>();
            services.AddSingleton<AuthPage>();
            services.AddSingleton<MainPage>();
            services.AddSingleton<NavigationService>();  
            services.AddTransient<DeepLinkHandler>();


            // // Add logging
            // services.AddLogging(logging =>
            // {
            //     logging.AddDebug(); // Add debug logging
            //     logging.AddConsole(); // Optional: Log to console
            // });

            return services.BuildServiceProvider();
        }
    }
}
