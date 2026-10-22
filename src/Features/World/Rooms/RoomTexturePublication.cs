using System;
using System.Collections.Generic;

namespace oracleofages;

// Main-thread presentation transaction for a synchronous room preparation.
// Logical palette/VRAM/layout writes still happen at their original boundaries.
internal sealed class RoomTexturePublication
{
    private int _depth;
    private readonly List<OracleRoomData> _pending = new();

    internal RoomTexturePublicationScope Defer()
    {
        _depth++;
        return new RoomTexturePublicationScope(this);
    }

    internal bool TryDefer(OracleRoomData room)
    {
        if (_depth == 0) return false;
        if (!_pending.Contains(room)) _pending.Add(room);
        return true;
    }

    internal void Complete()
    {
        if (_depth == 0) throw new InvalidOperationException("Room texture publication scope is not active.");
        if (--_depth != 0) return;
        try
        {
            foreach (OracleRoomData room in _pending) room.RefreshPaletteTextures();
        }
        finally { _pending.Clear(); }
    }
}
