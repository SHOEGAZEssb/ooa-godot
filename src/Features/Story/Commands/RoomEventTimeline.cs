using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Convenience timeline for finite event sequences made from waits, gates,
/// and single-update script actions.
/// </summary>
internal sealed class RoomEventTimeline
{

    private readonly RoomEventTimelineQueue<Step> _timeline = new();

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
