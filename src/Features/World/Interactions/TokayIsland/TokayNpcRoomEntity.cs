using System.Collections.Generic;
using Godot;

namespace oracleofages;

internal sealed class TokayNpcRoomEntity(TokayCharacter npc)
    : NpcCharacterRoomEntityAdapter(npc, npc.SetTransitionDrawOffset),
      IFixedRoomEntity, IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity,
      IUpdatesDuringDialogueRoomEntity
{
    public NpcCharacter Npc => Entity;
    public bool UpdatesDuringDialogue => Entity.Record.SubId is 0x05 or 0x0e or 0x0f or 0x10 or 0x11;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        npc.UpdateNative(frame.Player);
    public bool BlocksLink(Vector2 center) => Entity.BlocksLinkCenter(center);
    public NpcCharacter? FindTalkTarget(Player player) =>
        Entity.CanTalkTo(player) ? Entity : null;
}
