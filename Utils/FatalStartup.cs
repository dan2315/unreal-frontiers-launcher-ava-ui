using System;
using System.IO;
using System.Runtime.InteropServices;

namespace UnrealFrontiersLauncher;

// Reports a configuration error before the UI exists. Does not depend on AppStaticData, which may be what failed.
public static class FatalStartup
{
    private const uint MessageBoxOkIconError = 0x10;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr window, string text, string caption, uint type);

    public static int Fail(Exception exception)
    {
        var message = (exception is TypeInitializationException { InnerException: { } inner } ? inner : exception).Message;
        Console.Error.WriteLine($"Unreal Frontiers launcher cannot start: {message}");

        try
        {
            File.AppendAllText(
                Path.Combine(Path.GetTempPath(), "UnrealFrontiers-startup-error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception)
        {
        }

        if (OperatingSystem.IsWindows() && Environment.GetEnvironmentVariable("UF_NO_DIALOG") != "1")
        {
            MessageBoxW(IntPtr.Zero, message, "Unreal Frontiers launcher", MessageBoxOkIconError);
        }

        return 1;
    }
}
