using Godot;
using System.Collections.Generic;

namespace oracleofages;

// The placed $87:$00 becomes subid $06 in its existing native slot.
internal sealed class MakuSeedTreeRoomEntity(NpcCharacter actor, MakuSeedEvent owner)
    : NpcCharacterRoomEntityAdapter(actor, actor.SetTransitionDrawOffset), IFixedRoomEntity,
        IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
        IPlayerInteractable, IRoomBlocker
{
    public bool Finished => !Entity.Active;
    public bool UpdatesDuringDialogue => true;
    public bool UpdatesDuringRoomEntityFreeze => true;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => owner.AdvanceTree();
    public bool TryInteract(Player player) => owner.TryInteractTree(player);
    public bool BlocksLink(Vector2 center) => Entity.BlocksLinkCenter(center);
}
