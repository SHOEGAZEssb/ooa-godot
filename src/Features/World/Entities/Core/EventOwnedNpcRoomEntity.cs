using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Retains a placed actor slot for a room event to discover and control.
/// </summary>
internal sealed class EventOwnedNpcRoomEntity(NpcCharacter npc)
    : RoomEntityAdapter<NpcCharacter>(
        RequireEventOwned(npc), npc.SetTransitionDrawOffset),
        IVariableRoomEntity, IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity
{
    public NpcCharacter Npc => Entity;
    public void Update(double delta, Player player) =>
        Entity.UpdateNpc(delta, player.Position);
    public bool BlocksLink(Vector2 linkCenter) =>
        Entity.BlocksLinkCenter(linkCenter);
    public NpcCharacter? FindTalkTarget(Player player) =>
        Entity.CanTalkTo(player) ? Entity : null;

    private static NpcCharacter RequireEventOwned(NpcCharacter npc)
    {
        if (npc.Record.Implementation !=
            NpcImplementationClassification.EventOwned)
        {
            throw new InvalidOperationException(
                $"NPC {npc.Record.Group}:{npc.Record.Room:x2} " +
                $"${npc.Record.Id:x2}:${npc.Record.SubId:x2} " +
                $"var03=${npc.Record.Var03:x2} cannot use the event-owned " +
                $"adapter with classification {npc.Record.Implementation}.");
        }
        return npc;
    }
}
