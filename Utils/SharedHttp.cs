using System;
using System.Net.Http;
using System.Threading;

namespace UnrealFrontiersLauncher;

public static class SharedHttp
{
    public static readonly HttpClient Api = new() { Timeout = TimeSpan.FromSeconds(30) };
    public static readonly HttpClient Transfer = new() { Timeout = Timeout.InfiniteTimeSpan };
}
