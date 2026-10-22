using Godot;
using System;
using System.Diagnostics;

namespace oracleofages;

// Opt-in CPU attribution. Does not advance text or force a rendered frame.
internal sealed class DialogueLoadDiagnostics : IDisposable
{
    private readonly string _operation;
    private readonly string _text;
    private readonly long _started = Stopwatch.GetTimestamp();
    private readonly long _allocated = GC.GetAllocatedBytesForCurrentThread();
    private readonly int _gc0 = GC.CollectionCount(0), _gc1 = GC.CollectionCount(1), _gc2 = GC.CollectionCount(2);
    private long _previous;

    private DialogueLoadDiagnostics(string operation, int? textId)
    {
        _operation = operation;
        _text = textId is int id ? $"TX_{id:x4}" : "direct";
        _previous = _started;
    }

    internal static DialogueLoadDiagnostics? Begin(string operation, int? textId) =>
        OracleRoomLoadDiagnostics.IsEnabled ? new(operation, textId) : null;

    internal void Mark(string stage)
    {
        GD.Print(FormattableString.Invariant($"DIALOGUE_LOAD operation={_operation} text={_text} stage={stage} ms={Stopwatch.GetElapsedTime(_previous).TotalMilliseconds:F3}"));
        _previous = Stopwatch.GetTimestamp();
    }

    public void Dispose() => GD.Print(FormattableString.Invariant(
        $"DIALOGUE_LOAD operation={_operation} text={_text} stage=total ms={Stopwatch.GetElapsedTime(_started).TotalMilliseconds:F3} allocated_bytes={GC.GetAllocatedBytesForCurrentThread() - _allocated} gc0={GC.CollectionCount(0) - _gc0} gc1={GC.CollectionCount(1) - _gc1} gc2={GC.CollectionCount(2) - _gc2}"));
}
