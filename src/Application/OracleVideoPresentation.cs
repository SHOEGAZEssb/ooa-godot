using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Completed CGB frames, independently of CPU polling and LCD writes.</summary>
internal sealed class OracleVideoPresentation(Action<bool> blank)
{
    private const long FrameClocks = OracleExecutionClock.LcdFrameClocks;
    private readonly List<Action> _pending = [];
    private readonly List<Action> _uploaded = [];
    private bool _loading;
    private bool _lcdEnabled;
    private bool _blankPending;
    private long _nextFrame = long.MaxValue;

    internal void Stage(IEnumerable<Action> actions, bool loading)
    {
        _pending.AddRange(actions);
        _loading |= loading;
    }

    internal void SetLcd(bool enabled, long clock)
    {
        AdvanceTo(clock);
        if (_lcdEnabled == enabled) return;
        _lcdEnabled = enabled;
        // Pinned Gambatte memory.cpp: LCDC write and BLIT event. Disabling
        // schedules a four-line settle, then a blank complete frame. Enabling
        // discards the first frame; a pending blank is presented at its end.
        _nextFrame = enabled
            ? clock + 144 * 912 + (_blankPending ? 0 : FrameClocks)
            : clock + 4 * 912;
    }

    internal void CompleteLoading()
    {
        // bank0.s:vblankInterrupt uploads queued graphics, palettes and OAM
        // after the old frame completes. They belong to the next scanout.
        _uploaded.AddRange(_pending);
        _pending.Clear();
        _loading = false;
    }

    private static void Publish(List<Action> actions)
    {
        foreach (Action action in actions) action();
        actions.Clear();
    }

    internal void AdvanceTo(long clock)
    {
        while (_nextFrame <= clock)
        {
            if (_lcdEnabled || _blankPending)
            {
                blank(_blankPending);
                Publish(_uploaded);
                if (_loading && _blankPending)
                {
                    // LCD-off loaders may install controls behind the blank;
                    // the application cannot poll again until loading ends.
                    Publish(_pending);
                }
            }
            _blankPending = !_lcdEnabled;
            _nextFrame += FrameClocks;
        }
    }
}
