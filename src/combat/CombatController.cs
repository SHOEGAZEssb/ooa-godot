using Godot;
using System;

namespace oracleofages;

public sealed class CombatController
{
    private readonly Node _worldRoot;
    private readonly RoomSession _rooms;
    private readonly RoomView _roomView;
    private readonly RoomEntityManager _entities;
    private readonly Func<long> _animationTick;

    public CombatController(
        Node worldRoot,
        RoomSession rooms,
        RoomView roomView,
        RoomEntityManager entities,
        Func<long> animationTick)
    {
        _worldRoot = worldRoot;
        _rooms = rooms;
        _roomView = roomView;
        _entities = entities;
        _animationTick = animationTick;
    }

    public bool ApplySwordHit(Player player, Rect2 hitbox)
    {
        bool hitEnemy = _entities.ApplySwordHit(hitbox, player.Position);
        Vector2 offset = player.FacingVector == Vector2I.Up ? new Vector2(0, -14)
            : player.FacingVector == Vector2I.Right ? new Vector2(13, 0)
            : player.FacingVector == Vector2I.Down ? new Vector2(0, 13)
            : new Vector2(-14, 0);
        Vector2 point = player.Position + offset;
        int tileX = Mathf.FloorToInt(point.X / OracleRoomData.MetatileSize);
        int tileY = Mathf.FloorToInt(point.Y / OracleRoomData.MetatileSize);
        Rect2 tileBounds = new(
            tileX * OracleRoomData.MetatileSize,
            tileY * OracleRoomData.MetatileSize,
            OracleRoomData.MetatileSize,
            OracleRoomData.MetatileSize);
        if (!hitbox.Intersects(tileBounds) ||
            !_rooms.CurrentRoom.ReplaceMetatile(point, 0xc5, 0x3a, _animationTick()))
            return hitEnemy;

        _worldRoot.AddChild(new BushCutEffect
        {
            Position = tileBounds.GetCenter(),
            ZIndex = 12
        });
        _roomView.QueueRedraw();
        return true;
    }
}
