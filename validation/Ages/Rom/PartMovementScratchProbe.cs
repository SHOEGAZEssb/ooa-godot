
namespace Oracles;

// Validation-only phase boundary: registered ENEMY probes stage scratch;
// a logical INTERACTION observes it before later phases reuse WRAM.
internal sealed class PartMovementScratchProbe(Action observe)
    : RoomEntityAdapter<Node2D>(new Node2D(), static _ => { }), IFixedRoomEntity,
      IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    public bool UpdatesDuringDialogue => true;
    public bool UpdatesDuringRoomEntityFreeze => true;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => observe();
}
