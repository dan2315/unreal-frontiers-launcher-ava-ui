using System;
using UnrealFrontiersLauncher;

public class LaunchProgressTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ThreeStagesShareOneBar()
    {
        var progress = new LaunchProgress();

        progress.BeginStage(0);
        progress.Report(50, 100, T0);
        Assert.Equal(0.5 / 3, progress.Overall, 6);

        progress.BeginStage(1);
        Assert.Equal(1.0 / 3, progress.Overall, 6);
        progress.Report(100, 100, T0);
        Assert.Equal(2.0 / 3, progress.Overall, 6);

        progress.BeginStage(2);
        progress.Report(25, 100, T0);
        Assert.Equal((2 + 0.25) / 3, progress.Overall, 6);

        progress.Finish();
        Assert.Equal(1, progress.Overall);
    }

    [Fact]
    public void StageWithNothingToDownloadIsSkippedForward()
    {
        var progress = new LaunchProgress();
        progress.BeginStage(0);
        progress.BeginStage(1);
        progress.BeginStage(2);

        Assert.Equal(2.0 / 3, progress.Overall, 6);
    }

    [Fact]
    public void BarNeverGoesBackWhenAnInstallerLearnsOfMoreFiles()
    {
        var progress = new LaunchProgress();
        progress.BeginStage(2);
        progress.Report(80, 100, T0);
        var before = progress.Overall;

        progress.Report(90, 1000, T0);

        Assert.True(progress.Overall >= before);
    }

    [Fact]
    public void SpeedNeverGoesNegativeWhenTheByteCounterRestarts()
    {
        var meter = new SpeedMeter();
        var now = T0;
        long bytes = 0;
        for (var i = 0; i < 20; i++)
        {
            now = now.AddMilliseconds(100);
            bytes += 1_000_000;
            meter.AddSample(now, bytes);
            meter.Tick(now);
        }

        // A new stage: its counter starts from zero again.
        for (var i = 0; i < 20; i++)
        {
            now = now.AddMilliseconds(100);
            bytes = 500_000 * (i + 1) - 10_000_000 + 10_000_000;
            meter.AddSample(now, i == 0 ? 0 : bytes);
            meter.Tick(now);
            Assert.True(meter.Displayed >= 0);
            Assert.True(meter.Desired(now) >= 0);
        }
    }

    [Fact]
    public void SpeedMovesTowardsTheMeasuredValueInsteadOfJumping()
    {
        var meter = new SpeedMeter();
        var now = T0;
        long bytes = 0;
        meter.AddSample(now, bytes);

        // 10 MB/s for a few seconds.
        double first = 0;
        for (var i = 1; i <= 40; i++)
        {
            now = now.AddMilliseconds(100);
            bytes += 1_000_000;
            meter.AddSample(now, bytes);
            meter.Tick(now);
            if (i == 5) first = meter.Displayed;
        }

        Assert.InRange(first, 1, 9_000_000);
        Assert.InRange(meter.Displayed, 8_000_000, 11_000_000);
    }

    [Fact]
    public void SpeedFallsToZeroWhenNothingArrives()
    {
        var meter = new SpeedMeter();
        var now = T0;
        long bytes = 0;
        meter.AddSample(now, bytes);
        for (var i = 0; i < 20; i++)
        {
            now = now.AddMilliseconds(100);
            bytes += 1_000_000;
            meter.AddSample(now, bytes);
            meter.Tick(now);
        }

        for (var i = 0; i < 200; i++)
        {
            now = now.AddMilliseconds(100);
            meter.Tick(now);
        }

        Assert.Equal(0, meter.Displayed);
    }

    [Fact]
    public void FormatSpeed_ClampsNegativeValues()
    {
        Assert.Equal("0.00 MB/s", LaunchProgress.FormatSpeed(-5_000_000));
    }
}
