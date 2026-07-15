using System;
using System.Collections.Generic;

namespace oracleofages;

internal interface IRoomEventTimelineStep
{
    int DurationFrames { get; }
    int Counter { get; set; }
}

/// <summary>
/// Runs queued room-event steps with the original one-command-per-update
/// cadence. A completed step yields until the next update before its successor
/// starts, matching interactionRunScript's command boundaries.
/// </summary>
internal sealed class RoomEventTimeline<TStep> where TStep : class, IRoomEventTimelineStep
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

/// <summary>
/// Convenience timeline for finite event sequences made from waits, gates,
/// and single-update script actions.
/// </summary>
internal sealed class RoomEventTimeline
{
    private sealed class Step(
        int durationFrames,
        Func<Step, bool> update) : IRoomEventTimelineStep
    {
        public int DurationFrames { get; } = durationFrames;
        public int Counter { get; set; }
        public Func<Step, bool> Update { get; } = update;
    }

    private readonly RoomEventTimeline<Step> _timeline = new();

    public bool Active => _timeline.HasWork;

    public void Wait(
        int frames,
        Action<int>? counterChanged = null,
        Action? elapsed = null)
    {
        Enqueue(frames, step =>
        {
            step.Counter--;
            counterChanged?.Invoke(step.Counter);
            if (step.Counter != 0)
                return false;
            elapsed?.Invoke();
            return true;
        });
    }

    public void WaitUntil(Func<bool> condition, Action? completed = null)
    {
        Enqueue(0, _ =>
        {
            if (!condition())
                return false;
            completed?.Invoke();
            return true;
        });
    }

    public void Do(Action action)
    {
        Enqueue(0, _ =>
        {
            action();
            return true;
        });
    }

    /// <summary>Preserves a script-command boundary with no runtime mutation.</summary>
    public void Yield() => Do(static () => { });

    public bool AdvanceFrame() => _timeline.AdvanceFrame(step => step.Update(step));

    public void Clear() => _timeline.Clear();

    private void Enqueue(int durationFrames, Func<Step, bool> update) =>
        _timeline.Enqueue(new Step(durationFrames, update));
}
