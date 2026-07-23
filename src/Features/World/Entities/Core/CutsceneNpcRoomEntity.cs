using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// Script-created character interactions animate with the normal NPC renderer.
// Solidity and talking are opt-in because objectSetVisible82-only actors and
// followers do neither, while initialized NPC scripts call objectMarkSolidPosition.
internal sealed class CutsceneNpcRoomEntity(NpcCharacter npc, bool talkable, bool solid)
    : RoomEntityAdapter<NpcCharacter>(npc, npc.SetTransitionDrawOffset),
        IVariableRoomEntity, IRoomBlocker, ITalkTarget
{
    public void Update(double delta, Player player) => Entity.UpdateNpc(delta, player.Position);
    public bool BlocksLink(Vector2 linkCenter) => solid && Entity.BlocksLinkCenter(linkCenter);
    public NpcCharacter? FindTalkTarget(Player player) =>
        talkable && Entity.CanTalkTo(player) ? Entity : null;
}
