using System.Collections.Generic;
using Godot;

namespace oracleofages;

internal sealed class TokayNpcRoomEntity(TokayCharacter npc)
    : NpcCharacterRoomEntityAdapter(npc, npc.SetTransitionDrawOffset),
      IFixedRoomEntity, IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity,
      IUpdatesDuringDialogueRoomEntity
{
    public NpcCharacter Npc => Entity;
    // $0b/$0f/$10 test their visibility predicates only in state0. Publishing
    // room flag80 or wDimitriState bit1 must
    // retain a live actor until its own scriptend/update, including a peer
    // still waiting behind tokayRunSubid0f's additional scroll gate.
    public bool RefreshVisibility => Entity.Record.SubId is not (0x0b or 0x0f or 0x10);
    public bool UpdatesDuringDialogue => Entity.Record.SubId is 0x05 or 0x0e or 0x0f or 0x10 or 0x11;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        npc.UpdateNative(frame.Player);
    public bool BlocksLink(Vector2 center) => Entity.BlocksLinkCenter(center);
    public NpcCharacter? FindTalkTarget(Player player) =>
        Entity.CanTalkTo(player) ? Entity : null;
}
