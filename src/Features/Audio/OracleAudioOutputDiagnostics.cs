using Godot;
using System;
using System.Diagnostics;

namespace Oracles;

/// <summary>Opt-in operational audio diagnostics for debug device builds.</summary>
internal sealed class OracleAudioOutputDiagnostics
{
    private const string EnableFile = "user://audio-diagnostics.enabled";
    private long _lastFill, _lastReport;
    private double _maximumFillGap, _maximumTick;
    private int _discarded;

    internal static OracleAudioOutputDiagnostics? CreateIfEnabled()
    {
        if (!OS.IsDebugBuild() || !Godot.FileAccess.FileExists(EnableFile)) return null;
        GD.Print($"AUDIO_DIAGNOSTICS enabled: source_rate={OracleSoundEngine.SampleRate}, " +
            $"device_rate={AudioServer.GetMixRate()}, output_latency_ms={AudioServer.GetOutputLatency() * 1000:F2}.");
        return new OracleAudioOutputDiagnostics();
    }

    internal void ObserveTick(long started) =>
        _maximumTick = Math.Max(_maximumTick, Stopwatch.GetElapsedTime(started).TotalMilliseconds);

    internal void ObserveFill(int skips, int queued, int pending, int reserve, int discarded, long updates)
    {
        long now = Stopwatch.GetTimestamp();
        if (_lastFill != 0)
            _maximumFillGap = Math.Max(_maximumFillGap, Stopwatch.GetElapsedTime(_lastFill, now).TotalMilliseconds);
        _lastFill = now;
        _discarded += discarded;
        // At most one report per second, so reporting underruns cannot turn
        // into an unbounded stream of per-frame logging during a stall.
        if (_lastReport != 0 && Stopwatch.GetElapsedTime(_lastReport, now).TotalSeconds < 1) return;
        _lastReport = now;
        GD.Print($"AUDIO_DIAGNOSTICS updates={updates}, skips={skips}, queued={queued}, pending={pending}, " +
            $"reserve={reserve}, discarded={_discarded}, max_fill_gap_ms={_maximumFillGap:F2}, max_tick_ms={_maximumTick:F2}.");
        _maximumFillGap = _maximumTick = 0;
        _discarded = 0;
    }
}
