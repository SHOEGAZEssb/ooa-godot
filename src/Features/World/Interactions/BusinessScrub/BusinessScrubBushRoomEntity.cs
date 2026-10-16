using Godot;
using System.Collections.Generic;

namespace oracleofages;

// businessScrub.s:$ce:$80 is an independently allocated always-update slot.
// The related parent's sprite tree holds its tile image; this slot owns the
// source update that copies visible/Y and reads the parent's animParameter.
internal sealed class BusinessScrubBushRoomEntity(BusinessScrubRoomEntity parent)
    : IRoomEntity, IFixedRoomEntity, IUpdatesDuringDialogueRoomEntity,
        IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    public Node2D Node { get; } = new() { Name = "BusinessScrubBushSlot", Visible = false };
    public bool UpdatesDuringDialogue => true;
    public bool UpdatesDuringRoomEntityFreeze => true;
    public void SetTransitionDrawOffset(Vector2 offset) { }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => parent.UpdateBushPosition();
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns) => ScreenTransitionPresentation.Hidden;
}
