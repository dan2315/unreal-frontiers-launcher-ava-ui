using System.Runtime.InteropServices;

namespace UnrealFrontiersLauncher.Windows;
public class SystemInfo
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysicalMemory;
        public ulong AvailablePhysicalMemory;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtualMemory;
        public ulong AvailableVirtualMemory;
        public ulong AvailableExtendedVirtualMemory;

        public MemoryStatusEx()
        {
            Length = (uint)Marshal.SizeOf(typeof(MemoryStatusEx));
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx lpBuffer);

    private static MemoryStatusEx memoryStatusEx;
    public static (ulong value, string richValue) GetRamInfo()
    {
        if (memoryStatusEx == null) {
            memoryStatusEx = new MemoryStatusEx();
        }
        if (GlobalMemoryStatusEx(memoryStatusEx))
        {
            return (memoryStatusEx.TotalPhysicalMemory, $"{memoryStatusEx.TotalPhysicalMemory / (1024 * 1024 * 1024)} GB");
        }
        else
        {
            return (0 ,"Error retrieving RAM info");
        }
    }
}
