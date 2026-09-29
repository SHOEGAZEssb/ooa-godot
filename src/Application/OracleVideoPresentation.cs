using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Completed CGB frames, independently of CPU polling and LCD writes.</summary>
internal sealed class OracleVideoPresentation(Action<bool> blank)
{
    private const long FrameClocks = OracleExecutionClock.LcdFrameClocks;
    private readonly List<Action> _pending = [];
    private bool _loading;
    private bool _loadingLcdChanged;
    private bool _lcdEnabled;
    private bool _blankPending;
    private long _nextFrame = long.MaxValue;

    internal void Stage(IEnumerable<Action> actions, bool loading)
    {
        _pending.AddRange(actions);
        _loading |= loading;
        if (loading) _loadingLcdChanged = false;
    }

    internal void SetLcd(bool enabled, long clock)
    {
        AdvanceTo(clock);
        if (_lcdEnabled == enabled) return;
        if (_loading) _loadingLcdChanged = true;
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
        // LCD-enabled menu loads finish their queued tile uploads in VBlank.
        // Their next input poll must see the destination controls immediately.
        if (_loading && !_loadingLcdChanged) Publish();
        _loading = false;
    }

    private void Publish()
    {
        foreach (Action action in _pending) action();
        _pending.Clear();
        _loading = false;
    }

    internal void AdvanceTo(long clock)
    {
        while (_nextFrame <= clock)
        {
            if (_lcdEnabled || _blankPending)
            {
                blank(_blankPending);
                if (!_loading || _blankPending)
                {
                    Publish();
                }
            }
            _blankPending = !_lcdEnabled;
            _nextFrame += FrameClocks;
        }
    }
}
