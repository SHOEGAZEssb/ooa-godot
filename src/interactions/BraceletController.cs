using Godot;

namespace oracleofages;

public sealed class BraceletController
{
    private readonly RoomSession _rooms;
    private readonly BreakableTileDatabase _breakables;
    private readonly RoomView _roomView;
    private readonly System.Func<long> _animationTick;

    public BraceletController(
        RoomSession rooms,
        BreakableTileDatabase breakables,
        RoomView roomView,
        System.Func<long> animationTick)
    {
        _rooms = rooms;
        _breakables = breakables;
        _roomView = roomView;
        _animationTick = animationTick;
    }

    public bool TryUse(Player player)
    {
        OracleRoomData room = _rooms.CurrentRoom;
        Vector2 tilePoint = player.Position + (Vector2)player.FacingVector * 8.0f;
        byte tile = room.GetMetatile(tilePoint);
        if (!_breakables.TryGet(room.ActiveCollisions, tile, out BreakableTileDatabase.BreakableTileRecord record) ||
            !record.AllowsSource(BreakableTileDatabase.SourceBracelet) ||
            record.Replacement == 0)
        {
            return false;
        }

        byte replacement = GetReplacement(room, tilePoint, tile, record);
        if (!room.ReplaceMetatile(tilePoint, tile, replacement, _animationTick()))
            return false;

        _roomView.QueueRedraw();
        return true;
    }

    private static byte GetReplacement(
        OracleRoomData room,
        Vector2 tilePoint,
        byte tile,
        BreakableTileDatabase.BreakableTileRecord record)
    {
        if (tile == 0x10 && room.ActiveCollisions is 1 or 2)
        {
            byte original = room.GetOriginalMetatile(tilePoint);
            if (original != tile && room.GetCollision(original) < 0x20)
                return original;
        }

        return (byte)record.Replacement;
    }
}
