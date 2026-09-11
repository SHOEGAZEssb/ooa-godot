using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Finite native-event sequence. A completed step yields until the following
/// update; this cadence is distinct from interactionRunScript's carry dispatch.
/// </summary>
internal sealed class RoomEventTimeline
{
    private readonly Queue<Step> _pending = new();
    private Step? _current;

    public bool Active => _current is not null || _pending.Count != 0;

    public void Wait(
        int frames,
        Action<int>? counterChanged = null,
        Action? elapsed = null)
    {
        _pending.Enqueue(new Step(Math.Max(1, frames), step =>
        {
            step.Counter--;
            counterChanged?.Invoke(step.Counter);
            if (step.Counter != 0)
                return false;
            elapsed?.Invoke();
            return true;
        }));
    }

    public void WaitUntil(Func<bool> condition, Action? completed = null) =>
        _pending.Enqueue(new Step(0, _ =>
        {
            if (!condition())
                return false;
            completed?.Invoke();
            return true;
        }));

    public void Do(Action action) =>
        _pending.Enqueue(new Step(0, _ =>
        {
            action();
            return true;
        }));

    public void Yield() => Do(static () => { });

    public bool AdvanceFrame()
    {
        if (_current is null && !_pending.TryDequeue(out _current))
            return false;
        if (_current.Update(_current))
            _current = null;
        return true;
    }

    public void Clear()
    {
        _pending.Clear();
        _current = null;
    }

    private sealed class Step(int counter, Func<Step, bool> update)
    {
        public int Counter { get; set; } = counter;
        public Func<Step, bool> Update { get; } = update;
    }
}
