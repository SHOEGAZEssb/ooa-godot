using System;

namespace oracleofages;

// bank0.s setTile / loadTilesToRam.s updateChangedTileQueue. One empty
// ring position distinguishes full from empty, leaving 31 usable entries.
internal sealed class ChangedTileQueue
{
    private const int RingSize = 0x20;
    private const int RingIndexMask = RingSize - 1;
    // loadTilesToRam.s:updateChangedTileQueue, before @handleSingleEntry.
    private const int GraphicsWritesPerUpdate = 4;
    private const int GraphicsBlockedScrollModes = 0x0e;

    private readonly ChangedTileWrite[] _entries = new ChangedTileWrite[RingSize];
    private int _head, _tail;
    internal int Count => (_tail - _head) & RingIndexMask;
    internal void Clear() => _head = _tail = 0;

    internal bool TryWrite(byte position, byte tile, Action<ChangedTileWrite> updateLayoutAndCollision)
    {
        int next = (_tail + 1) & RingIndexMask;
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
        if ((scrollMode & GraphicsBlockedScrollModes) != 0) return;
        for (int i = 0; i < GraphicsWritesPerUpdate && _head != _tail; i++)
        {
            _head = (_head + 1) & RingIndexMask;
            updateGraphics(_entries[_head]);
        }
    }
}

internal readonly record struct ChangedTileWrite(byte Position, byte Tile);
