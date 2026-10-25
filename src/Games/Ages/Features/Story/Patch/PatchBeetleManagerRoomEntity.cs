using Godot;
using System.Collections.Generic;

namespace Oracles;

// patch.s:patch_subid03 owns a real, invisible INTERACTION slot.
internal sealed partial class PatchBeetleManagerRoomEntity(PatchEvent owner) : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    public Node2D Node => this;
    public bool Finished => owner.ManagerFinished;
    public bool UpdatesDuringDialogue => owner.ManagerInitializationPending;
    public bool UpdatesDuringRoomEntityFreeze => UpdatesDuringDialogue || owner.KeepsInteractionsRunning;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => owner.UpdateManager();
    public void SetTransitionDrawOffset(Vector2 offset) { }
}
