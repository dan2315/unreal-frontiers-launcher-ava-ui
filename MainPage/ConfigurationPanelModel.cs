public class LaunchConfiguration
{
    public LaunchConfiguration(){}
    public LaunchConfiguration(int defaultRamValue)
    {
        Ram = defaultRamValue;
    }

    public int Ram {set; get;}
    // Obsolete: console/close behaviour moved to global Preferences.
    // Kept for launch.json compatibility, no longer read at launch.
    public bool NeedToClose {set; get;}
    public bool NeedToOpenConsole {set; get;}
}