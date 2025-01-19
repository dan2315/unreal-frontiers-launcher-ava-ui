namespace UnrealFrontiersLauncher.Platforms;

public interface ISystemInfo
{ 
    (ulong value, string richValue) GetRamInfo();
}