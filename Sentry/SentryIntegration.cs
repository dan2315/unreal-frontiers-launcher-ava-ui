using System;
using Sentry;

namespace UnrealFrontiersLauncher.Sentry;

public static class SentryIntegration
{
    private const string DefaultDsn = "https://b28973de6cfce589e42e9711cdce5db1@o4508672247332864.ingest.de.sentry.io/4508672268763216";

    public static void Initialize()
    {
        SentrySdk.Init(options =>
        {
            options.Dsn = Environment.GetEnvironmentVariable("SENTRY_DSN") ?? DefaultDsn;
            options.Release = Autoupdater.CurrentVersion.ToString();
            options.AutoSessionTracking = true;
#if DEBUG
            options.Debug = true;
#endif
        });
    }
}
