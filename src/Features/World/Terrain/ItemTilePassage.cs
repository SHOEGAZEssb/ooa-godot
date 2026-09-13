using Godot;

namespace oracleofages;

/// <summary>Per-item cache and byte elevation for itemCheckCanPassSolidTile.</summary>
internal sealed class ItemTilePassage
{
    private readonly BombRecord _passableTiles = new BombDatabase().Data;
    private readonly ItemCliffDatabase _cliffs = new();
    private int _lastPosition = -1;
    private int _lastTile = -1;
    private int _elevation;

    internal bool CanPass(OracleRoomData room, Vector2 point, int angle)
    {
        int position = room.GetPackedPosition(point), tile = room.GetMetatile(point);
        if (position == _lastPosition && tile == _lastTile) return true;
        _lastPosition = position;
        _lastTile = tile;
        if (_passableTiles.CanPassSolidTile(room, point)) return true;
        if (_cliffs.TryGetDelta(room.ActiveCollisions, (byte)tile, angle / 4, out byte delta))
        {
            _elevation = (_elevation + delta) & 0xff;
            if ((_elevation & 0x80) == 0) return true;
        }
        _lastPosition = _lastTile = -1;
        return false;
    }
}
