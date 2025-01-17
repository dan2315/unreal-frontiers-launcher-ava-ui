using System;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Svg.Skia;
using Microsoft.Extensions.DependencyInjection;

namespace UnrealFrontiersLauncher
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
        {
            var serviceProvider = ConfigureServices();
            
            GC.KeepAlive(typeof(SvgImageExtension).Assembly);
            GC.KeepAlive(typeof(Avalonia.Svg.Skia.Svg).Assembly);
            
            // Pass serviceProvider to App's constructor
            return AppBuilder.Configure<App>()
                             .UsePlatformDetect()
                             .LogToTrace()
                             .With(new Win32PlatformOptions())
                             .AfterSetup(_ =>
                             {
                                 var app = (App)App.Current;
                                 app.IncludeServices(serviceProvider);
                             });
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddSingleton<AuthService>();
            services.AddSingleton<GameLauncher>();
            services.AddSingleton<InstanceManager>();
            services.AddSingleton<PlayerProfileProvider>();
            
            services.AddSingleton<MainWindow>();
            services.AddSingleton<PopupService>(); 

            services.AddTransient<EntryModal>();
            services.AddSingleton<AuthPage>();
            services.AddTransient<MainPage>();
            services.AddSingleton<NavigationService>();


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
