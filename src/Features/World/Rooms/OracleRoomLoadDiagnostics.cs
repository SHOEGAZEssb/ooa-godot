using Godot;
using System;
using System.Diagnostics;

namespace Oracles;

// Opt-in device timings only; no gameplay state or scheduling lives here.
internal sealed class OracleRoomLoadDiagnostics : IDisposable
{
    private static readonly bool Enabled = OS.IsDebugBuild() &&
        (Godot.FileAccess.FileExists("user://room-load-diagnostics.enabled") ||
         Array.IndexOf(OS.GetCmdlineUserArgs(), "--room-load-diagnostics") >= 0);
    internal static bool IsEnabled => Enabled;
    private readonly string _operation;
    private readonly int _group, _room;
    private readonly long _started = Stopwatch.GetTimestamp();
    private readonly long _allocated = GC.GetAllocatedBytesForCurrentThread();
    private readonly int _gen0 = GC.CollectionCount(0), _gen1 = GC.CollectionCount(1), _gen2 = GC.CollectionCount(2);
    private long _previous;

    private OracleRoomLoadDiagnostics(string operation, int group, int room)
    {
        _operation = operation; _group = group; _room = room;
        _previous = _started;
    }

    internal static OracleRoomLoadDiagnostics? Begin(string operation, int group, int room, string? asset = null) =>
        Enabled ? new(asset is null ? operation : operation + "-" + asset, group, room) : null;

    internal void Mark(string stage)
    {
        long now = Stopwatch.GetTimestamp();
        GD.Print(FormattableString.Invariant($"ROOM_LOAD operation={_operation} room={_group:x1}:{_room:x2} stage={stage} ms={Stopwatch.GetElapsedTime(_previous, now).TotalMilliseconds:F3}"));
        // Exclude the diagnostic output itself from the following stage.
        _previous = Stopwatch.GetTimestamp();
    }

    public void Dispose() => GD.Print(FormattableString.Invariant(
        $"ROOM_LOAD operation={_operation} room={_group:x1}:{_room:x2} stage=total ms={Stopwatch.GetElapsedTime(_started).TotalMilliseconds:F3} allocated_bytes={GC.GetAllocatedBytesForCurrentThread() - _allocated} gc0={GC.CollectionCount(0) - _gen0} gc1={GC.CollectionCount(1) - _gen1} gc2={GC.CollectionCount(2) - _gen2}"));
}
