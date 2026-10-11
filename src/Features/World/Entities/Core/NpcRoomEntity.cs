using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class NpcRoomEntity(NpcCharacter npc)
    : NpcCharacterRoomEntityAdapter(
        RequireOrdinaryGeneric(npc), npc.SetTransitionDrawOffset),
    IFixedRoomEntity, IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity
{
    private Func<bool>? _pendingDialogue;
    public NpcCharacter Npc => Entity;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        // Link sets pressedAButton in its earlier update. The generic script
        // consumes it and calls showText in this interaction's own slot,
        // before its final animation update. The next pass freezes for text.
        if (_pendingDialogue?.Invoke() == true)
            _pendingDialogue = null;
        Entity.FaceLinkAndAnimateOneUpdate(frame.Player, preventPassing: false);
    }
    internal void QueueDialogue(Func<bool> tryShowDialogue) => _pendingDialogue = tryShowDialogue;
    public bool BlocksLink(Vector2 linkCenter) => Entity.BlocksLinkCenter(linkCenter);
    public NpcCharacter? FindTalkTarget(Player player) =>
        _pendingDialogue is null && Entity.CanTalkTo(player) ? Entity : null;
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
