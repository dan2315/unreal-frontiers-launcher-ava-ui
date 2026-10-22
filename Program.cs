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
#if DEV_BUILD
            // The DEV build must not take over (or remove) the production launcher's uflauncher:// link.
            VelopackApp.Build().Run();
#else
            VelopackApp.Build()
                .OnAfterInstallFastCallback(_ => UrlSchemeRegistration.Register())
                .OnAfterUpdateFastCallback(_ => UrlSchemeRegistration.Register())
                .OnBeforeUninstallFastCallback(_ => UrlSchemeRegistration.Unregister())
                .Run();
#endif

            try
            {
                _ = AppStaticData.ServerURL;
            }
            catch (Exception exception)
            {
                Environment.Exit(FatalStartup.Fail(exception));
            }

            CrashLog.Install();
            LegacyDataMigration.MigrateFromInnoInstall();
            InstallLocation.ApplyPendingChange();
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
#if WINDOWS
            services.AddSingleton<Platforms.ISystemInfo, Windows.SystemInfo>();
#elif LINUX
            services.AddSingleton<Platforms.ISystemInfo, Linux.SystemInfo>();
#endif

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
