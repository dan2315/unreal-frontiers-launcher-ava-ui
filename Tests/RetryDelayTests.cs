using System;
using System.Net;
using System.Net.Http;
using UnrealFrontiersLauncher;
using UnrealFrontiersLauncher.Packs;

public class RetryDelayTests
{
    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public void BusyServer_GetsLongerBackoff(HttpStatusCode status)
    {
        var busy = new HttpRequestException("busy", null, status);

        Assert.Equal(TimeSpan.FromSeconds(3), PackInstaller.RetryDelay(busy, 1));
        Assert.Equal(TimeSpan.FromSeconds(12), PackInstaller.RetryDelay(busy, 3));
    }

    [Fact]
    public void OtherErrors_KeepTheShortBackoff()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(500), PackInstaller.RetryDelay(new HttpRequestException("reset"), 1));
        Assert.Equal(TimeSpan.FromSeconds(2), PackInstaller.RetryDelay(new System.IO.IOException(), 3));
        Assert.Equal(TimeSpan.FromMilliseconds(500), PackInstaller.RetryDelay(new HttpRequestException("nf", null, HttpStatusCode.NotFound), 1));
    }
}
