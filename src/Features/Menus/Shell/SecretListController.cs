using Godot;
using System;

namespace oracleofages;

internal sealed class SecretListController(SecretListScreen screen, OracleMenuLifecycle lifecycle,
    OracleSaveData save, Action<int> playSound) : IOracleMenuLifecycleClient
{
    private readonly SecretListDatabase _data = new();
    private readonly FixedUpdateAccumulator _updates = new();
    private int _cursor, _scroll, _scrollSpeed, _scrollY, _printedCursor;
    public string MenuName => "MENU_SECRET_LIST";
    internal bool IsActive => lifecycle.IsOwnedBy(this);
    internal SecretListScreen? Screen => IsActive ? screen : null;
    internal int Cursor => _cursor;
    internal int Scroll => _scroll;
    internal bool Open() => lifecycle.TryBeginOpening(this);
    internal void Update(double delta)
    {
        if (!IsActive) return;
        if (!lifecycle.IsOpenFor(this))
        {
            lifecycle.Update(this, delta);
            if (!lifecycle.IsOpenFor(this)) return;
        }
        int updates = _updates.Consume(delta);
        for (int i = 0; i < updates && lifecycle.IsOpenFor(this); i++)
        {
            if (_scrollSpeed != 0)
            {
                _scrollY += _scrollSpeed;
                if ((_scrollY & 15) == 0) { _scroll += _scrollSpeed / 2; _scrollSpeed = 0; }
            }
            else
            {
                if (Input.IsActionJustPressed("item") || Input.IsActionJustPressed("map") || Input.IsActionJustPressed("inventory"))
                { playSound(SoundId.SndCloseMenu); lifecycle.BeginClosing(this); return; }
                int keys = lifecycle.DirectionInputWithAutofire();
                int next = _cursor + ((keys & 4) != 0 ? -1 : (keys & 8) != 0 ? 1 : 0);
                if (next != _cursor && (uint)next < _data.Entries(save).Count)
                {
                    _cursor = next; playSound(SoundId.SndMenuMove);
                    if (_cursor - _scroll < 1 && _scroll > 0) _scrollSpeed = -2;
                    else if (_cursor - _scroll >= 3 && _scroll < _data.Entries(save).Count - 4) _scrollSpeed = 2;
                }
                if (_printedCursor != _cursor)
                {
                    var entry = _data.Entries(save)[_cursor]; _printedCursor = _cursor;
                    screen.SetSecret(_data.Text(entry, save), save.HasGlobalFlag(entry.Flag));
                }
            }
            screen.Cursor = _cursor; screen.ScrollY = _scrollY; screen.QueueRedraw();
        }
    }
    public void OpenAtWhite()
    {
        _cursor = _scroll = _scrollSpeed = 0; _scrollY = 0x30; _printedCursor = -1; _updates.Reset();
        screen.Cursor = 0; screen.ScrollY = _scrollY;
        playSound(SoundId.SndOpenMenu);
        screen.Initialize(_data, save); screen.Visible = true;
    }
    public void CloseAtWhite()
    {
        screen.Visible = false;
    }
    public void LifecycleClosed() { }
}
