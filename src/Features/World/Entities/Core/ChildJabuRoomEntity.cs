using Godot;
using System.Collections.Generic;

namespace oracleofages;

// childJabu.s: $0e/$06 radii survive genericNpcScript's initcollisions.
internal sealed class ChildJabuRoomEntity : NpcCharacterRoomEntityAdapter,
    IFixedRoomEntity, IRoomBlocker, ITalkTarget, IUpdatesDuringDialogueRoomEntity,
    IUpdatesDuringRoomEntityFreeze, IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    internal ChildJabuRoomEntity(NpcRecord record) : this(NpcCharacter.CreateFromRecord(record)) { }
    private ChildJabuRoomEntity(NpcCharacter actor) : base(actor, actor.SetTransitionDrawOffset)
    {
        Entity.SetCollisionRadii(0x0e, 0x06);
        Entity.ShowInitializedPresentation();
        Entity.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex);
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.AnimateAsNpcOneUpdate(frame.Player);
    public bool UpdatesDuringDialogue=>true;
    public bool UpdatesDuringRoomEntityFreeze=>true;
    public void UpdateDuringScreenTransition(RoomEntityFrame frame)=>Entity.AnimateAsNpcOneUpdate(frame.Player);
    public bool BlocksLink(Vector2 center) => Entity.BlocksLinkCenter(center);
    public NpcCharacter? FindTalkTarget(Player player) => Entity.CanTalkTo(player) ? Entity : null;
}
