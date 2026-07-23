using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class Room149NpcRoomEntity(
    NpcCharacter npc,
    Room149FamilyInteraction family,
    Action<RoomEntityFrame> updateFrame)
    : RoomEntityAdapter<NpcCharacter>(npc, npc.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomBlocker, ITalkTarget, IRoomSaveStateEntity
{
    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) => updateFrame(frame);

    public bool BlocksLink(Vector2 linkCenter) =>
        Entity.BlocksLinkCenter(linkCenter);

    public NpcCharacter? FindTalkTarget(Player player) =>
        Entity.CanTalkTo(player) ? Entity : null;

    public void RefreshSaveState() => family.RefreshSaveState();
}
