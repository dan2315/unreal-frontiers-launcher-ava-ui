using System;
using System.Collections.Generic;

namespace UnrealFrontiersLauncher;

/// <summary>
/// One progress for the whole launch: three stages (Minecraft, mod loader, modpack) of equal width on a single bar,
/// plus a download speed that moves smoothly and never goes below zero.
/// </summary>
public sealed class LaunchProgress
{
    public static readonly string[] StageNames = ["Майнкрафт", "Модлоадер", "Модпак"];
    public static int StageCount => StageNames.Length;

    private readonly SpeedMeter _speed = new();
    private int _stage;
    private double _stageFraction;
    private bool _finished;

    public int Stage => _stage;

    /// <summary>0..1 over all stages. Never goes backwards while a launch runs.</summary>
    public double Overall => _finished ? 1 : (_stage + _stageFraction) / StageCount;

    public bool Finished => _finished;

    /// <summary>Smoothed bytes per second, always &gt;= 0.</summary>
    public double SpeedBytesPerSecond => _speed.Displayed;

    public void Reset()
    {
        _stage = 0;
        _stageFraction = 0;
        _finished = false;
        _speed.Reset();
    }

    /// <summary>Starts a stage; the stages before it count as done (a stage with nothing to download is skipped).</summary>
    public void BeginStage(int stage)
    {
        if (stage < _stage) return;
        _stage = Math.Clamp(stage, 0, StageCount - 1);
        _stageFraction = 0;
        // Every stage has its own byte counter: the next sample is a new baseline, not a jump.
        _speed.Rebase();
    }

    /// <summary>Bytes done/total of the current stage (the counters of the installers restart in every stage).</summary>
    public void Report(long doneBytes, long totalBytes, DateTime? now = null)
    {
        if (totalBytes > 0)
        {
            // An installer can learn about more files while it runs, which would make the fraction shrink.
            _stageFraction = Math.Max(_stageFraction, Math.Clamp((double)doneBytes / totalBytes, 0, 1));
        }
        _speed.AddSample(now ?? DateTime.UtcNow, doneBytes);
    }

    public void Finish()
    {
        _finished = true;
    }

    /// <summary>Moves the displayed speed towards the measured one. Call it often (a UI timer).</summary>
    public void Tick(DateTime? now = null) => _speed.Tick(now ?? DateTime.UtcNow);

    public static string FormatSpeed(double bytesPerSecond) => $"{Math.Max(0, bytesPerSecond) / 1024 / 1024:F2} MB/s";
}

/// <summary>Measures the download speed over a short window and eases the shown value to it.</summary>
public sealed class SpeedMeter
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(3);
    // Time constant of the easing: about this long to cover two thirds of the way to the measured speed.
    private static readonly TimeSpan Smoothing = TimeSpan.FromSeconds(0.6);

    private readonly Queue<(DateTime Time, long Bytes)> _deltas = new();
    private long? _last;
    private DateTime? _started;
    private DateTime? _lastTick;

    public double Displayed { get; private set; }

    public void Reset()
    {
        _deltas.Clear();
        _last = null;
        _started = null;
        _lastTick = null;
        Displayed = 0;
    }

    public void Rebase() => _last = null;

    public void AddSample(DateTime now, long totalBytes)
    {
        _started ??= now;

        // The first sample, or a counter that went back (a new stage, a retry), only sets the baseline.
        if (_last is { } last && totalBytes >= last)
            _deltas.Enqueue((now, totalBytes - last));
        _last = totalBytes;
        Trim(now);
    }

    /// <summary>Bytes per second over the window, 0 when nothing arrived.</summary>
    public double Desired(DateTime now)
    {
        Trim(now);
        if (_started is not { } started) return 0;

        var span = Math.Min(Window.TotalSeconds, (now - started).TotalSeconds);
        if (span < 0.25) return 0;

        long sum = 0;
        foreach (var (_, bytes) in _deltas) sum += bytes;
        return Math.Max(0, sum / span);
    }

    public void Tick(DateTime now)
    {
        var elapsed = _lastTick is { } previous ? Math.Max(0, (now - previous).TotalSeconds) : 0;
        _lastTick = now;

        var desired = Desired(now);
        var weight = 1 - Math.Exp(-elapsed / Smoothing.TotalSeconds);
        Displayed = Math.Max(0, Displayed + (desired - Displayed) * weight);
        if (Displayed < 1 && desired == 0) Displayed = 0;
    }

    private void Trim(DateTime now)
    {
        while (_deltas.Count > 0 && _deltas.Peek().Time < now - Window)
            _deltas.Dequeue();
    }
}
