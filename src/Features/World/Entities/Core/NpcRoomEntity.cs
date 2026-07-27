using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class NpcRoomEntity(NpcCharacter npc)
    : RoomEntityAdapter<NpcCharacter>(
        RequireOrdinaryGeneric(npc), npc.SetTransitionDrawOffset),
    IVariableRoomEntity, IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity
{
    public NpcCharacter Npc => Entity;
    public void Update(double delta, Player player) => Entity.UpdateNpc(delta, player.Position);
    public bool BlocksLink(Vector2 linkCenter) => Entity.BlocksLinkCenter(linkCenter);
    public NpcCharacter? FindTalkTarget(Player player) => Entity.CanTalkTo(player) ? Entity : null;

    private static NpcCharacter RequireOrdinaryGeneric(NpcCharacter npc)
    {
        if (npc.Record.Implementation !=
            NpcImplementationClassification.OrdinaryGeneric)
        {
            throw new InvalidOperationException(
                $"NPC {npc.Record.Group}:{npc.Record.Room:x2} " +
                $"${npc.Record.Id:x2}:${npc.Record.SubId:x2} " +
                $"var03=${npc.Record.Var03:x2} cannot use the ordinary NPC " +
                $"adapter with classification {npc.Record.Implementation}.");
        }
        return npc;
    }
}
