using Godot;
using System.Collections.Generic;

namespace Oracles;

internal sealed class PatchRoomEntity(NpcCharacter npc)
    : NpcCharacterRoomEntityAdapter(npc, npc.SetTransitionDrawOffset), IRoomBlocker, ITalkTarget,
      IRoomEntityUpdateFreeze, IFixedRoomEntity,
      IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private PatchEvent? _owner;
    internal NpcCharacter Npc => Entity;
    internal void Bind(PatchEvent owner) => _owner = owner;
    public bool UpdatesDuringDialogue => Entity.Record.SubId < 2 || _owner?.SlotInitializationPending(Entity.Record.SubId) == true;
    public bool UpdatesDuringRoomEntityFreeze => UpdatesDuringDialogue || _owner?.KeepsInteractionsRunning == true;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.Active) _owner?.UpdateActorSlot(Entity.Record.SubId);
    }
    public bool FreezesRoomEntities => _owner?.FreezesObjects == true;
    public bool BlocksLink(Vector2 center) => Entity.Record.SubId < 2 && Entity.BlocksLinkCenter(center);
    public NpcCharacter? FindTalkTarget(Player player) => Entity.Record.SubId < 2 && Entity.CanTalkTo(player) ? Entity : null;
}
