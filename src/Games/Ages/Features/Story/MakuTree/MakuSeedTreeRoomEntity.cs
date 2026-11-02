
namespace Oracles;

// The placed $87:$00 becomes subid $06 in its existing native slot.
internal sealed class MakuSeedTreeRoomEntity(NpcCharacter actor, MakuSeedEvent owner)
    : NpcCharacterRoomEntityAdapter(actor, actor.SetTransitionDrawOffset), IFixedRoomEntity,
        IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
        IPlayerInteractable, IRoomBlocker
{
    private bool _initialized;
    public bool Finished => !Entity.Active;
    public bool UpdatesDuringDialogue => true;
    public bool UpdatesDuringRoomEntityFreeze => true;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized) { _initialized = true; owner.InitializeTreeSlot(); }
        owner.AdvanceTree();
    }
    public bool TryInteract(Player player) => owner.TryInteractTree(player);
    public bool BlocksLink(Vector2 center) => Entity.BlocksLinkCenter(center);
}
