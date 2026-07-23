using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Runs queued room-event steps with the original one-command-per-update
/// cadence. A completed step yields until the next update before its successor
/// starts, matching interactionRunScript's command boundaries.
/// </summary>
internal sealed class RoomEventTimelineQueue<TStep> where TStep : class, IRoomEventTimelineStep
{
    private readonly Queue<TStep> _pending = new();
    private TStep? _current;

    public bool HasWork => _current is not null || _pending.Count != 0;

    public void Enqueue(TStep step) => _pending.Enqueue(step);

    /// <summary>Returns false only when no current or pending step remains.</summary>
    public bool AdvanceFrame(Func<TStep, bool> updateStep)
    {
        if (_current is null)
        {
            if (_pending.Count == 0)
                return false;
            _current = _pending.Dequeue();
            _current.Counter = Math.Max(1, _current.DurationFrames);
        }

        if (updateStep(_current))
            _current = null;
        return true;
    }

    public void Clear()
    {
        _pending.Clear();
        _current = null;
    }
}
