using System;

namespace oracleofages;

// bank0.s setTile / loadTilesToRam.s updateChangedTileQueue. One empty
// ring position distinguishes full from empty, leaving 31 usable entries.
internal sealed class ChangedTileQueue
{
    private readonly ChangedTileWrite[] _entries = new ChangedTileWrite[32];
    private int _head, _tail;
    internal int Count => (_tail - _head) & 0x1f;
    internal void Clear() => _head = _tail = 0;

    internal bool TryWrite(byte position, byte tile, Action<ChangedTileWrite> updateLayoutAndCollision)
    {
        int next = (_tail + 1) & 0x1f;
        if (next == _head) return false;
        _tail = next;
        var write = new ChangedTileWrite(position,tile);
        _entries[_tail] = write;
        // Logical terrain changes immediately, before its queued graphics.
        updateLayoutAndCollision(write);
        return true;
    }

    internal void UpdateGraphics(byte scrollMode, Action<ChangedTileWrite> updateGraphics)
    {
        if ((scrollMode & 0x0e) != 0) return;
        for (int i = 0; i < 4 && _head != _tail; i++)
        {
            _head = (_head + 1) & 0x1f;
            updateGraphics(_entries[_head]);
        }
    }
}

internal readonly record struct ChangedTileWrite(byte Position, byte Tile);
