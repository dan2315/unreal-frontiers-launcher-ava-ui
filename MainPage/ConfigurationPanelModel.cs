public class LaunchConfiguration
{
    public LaunchConfiguration(){}
    public LaunchConfiguration(int defaultRamValue)
    {
        Ram = defaultRamValue;
    }

    public int Ram {set; get;}
    public bool NeedToClose {set; get;}
    public bool NeedToOpenConsole {set; get;}
}