using Sentry;

namespace UnrealFrontiersLauncher.Sentry;


public static class SentryIntegration
{
    public static void Initialize()
    {
        SentrySdk.Init(options =>
        {
            // A Sentry Data Source Name (DSN) is required.
            // See https://docs.sentry.io/product/sentry-basics/dsn-explainer/
            // You can set it in the SENTRY_DSN environment variable, or you can set it in code here.
            options.Dsn = "https://b28973de6cfce589e42e9711cdce5db1@o4508672247332864.ingest.de.sentry.io/4508672268763216";

            // When debug is enabled, the Sentry client will emit detailed debugging information to the console.
            // This might be helpful, or might interfere with the normal operation of your application.
            // We enable it here for demonstration purposes when first trying Sentry.
            // You shouldn't do this in your applications unless you're troubleshooting issues with Sentry.
            options.Debug = true;

            // This option is recommended. It enables Sentry's "Release Health" feature.
            options.AutoSessionTracking = true;
        });
        
        SentrySdk.CaptureMessage("Something went wrong");
    }
}