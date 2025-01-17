using System;
using System.Linq;
using System.Reflection;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;

namespace UnrealFrontiersLauncher
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            CreateAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder CreateAvaloniaApp()
        {
            var builder = AppBuilder.Configure(() => new App(ConfigureServices()));

            builder.UsePlatformDetect()
                   .LogToTrace()
                   .With(new Win32PlatformOptions());

            return builder;
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
