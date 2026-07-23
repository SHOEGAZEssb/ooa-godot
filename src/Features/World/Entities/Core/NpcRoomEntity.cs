using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class NpcRoomEntity(NpcCharacter npc)
    : RoomEntityAdapter<NpcCharacter>(npc, npc.SetTransitionDrawOffset),
    IVariableRoomEntity, IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity
{
    public NpcCharacter Npc => Entity;
    public void Update(double delta, Player player) => Entity.UpdateNpc(delta, player.Position);
    public bool BlocksLink(Vector2 linkCenter) => Entity.BlocksLinkCenter(linkCenter);
    public NpcCharacter? FindTalkTarget(Player player) => Entity.CanTalkTo(player) ? Entity : null;
}
