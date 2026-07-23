using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal abstract class BlackTowerNpcRoomEntity(
    NpcCharacter npc,
    Action<Vector2> transitionOffset)
    : RoomEntityAdapter<NpcCharacter>(npc, transitionOffset),
        IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity
{
    public NpcCharacter Npc => Entity;
    public virtual bool BlocksLink(Vector2 linkCenter) =>
        Entity.BlocksLinkCenter(linkCenter);
    public virtual NpcCharacter? FindTalkTarget(Player player) =>
        Entity.CanTalkTo(player) ? Entity : null;

    protected static Vector2I DirectionVector(int direction) => direction switch
    {
        0 => Vector2I.Up,
        1 => Vector2I.Right,
        2 => Vector2I.Down,
        3 => Vector2I.Left,
        _ => throw new ArgumentOutOfRangeException(nameof(direction))
    };
}
