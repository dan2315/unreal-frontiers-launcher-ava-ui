using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace UnrealFrontiersLauncher;

public static class AppForegroundHelper
{
    private const int SW_RESTORE = 9;

    // Windows-specific native methods
    [DllImport("User32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("User32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("User32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    public static void BringAppToForeground()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            BringAppToForegroundWindows();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            BringAppToForegroundLinux();
        }
        else
        {
            throw new PlatformNotSupportedException("Only Windows and Linux are supported.");
        }
    }

    private static void BringAppToForegroundWindows()
    {
        var window = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var mainWindow = window?.MainWindow;

        if (mainWindow != null)
        {
            IntPtr hWnd = mainWindow.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (hWnd != IntPtr.Zero && GetForegroundWindow() != hWnd)
            {
                ShowWindow(hWnd, SW_RESTORE);
                SetForegroundWindow(hWnd);
            }
        }
    }

    private static void BringAppToForegroundLinux()
    {
        var window = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var mainWindow = window?.MainWindow;

        if (mainWindow != null)
        {
            // Use Avalonia's window management APIs for Linux
            mainWindow.Activate();
        }
        else
        {
            Console.WriteLine("Unable to bring the app to the foreground on Linux.");
        }
    }
}
