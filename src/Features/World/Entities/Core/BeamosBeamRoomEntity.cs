using System.Collections.Generic;

namespace Oracles;

internal sealed class BeamosBeamRoomEntity(BeamosBeamPart beam)
    : RoomEntityAdapter<BeamosBeamPart>(beam, beam.SetTransitionDrawOffset), IFixedRoomEntity,
        IRoomEntityLifetime, ILinkContactEntity, IPostObjectLinkContactRoomEntity, INativePartHealthRoomEntity,
        IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    public bool UpdatesDuringDialogue => Entity.State == 0;
    public bool UpdatesDuringRoomEntityFreeze => Entity.State == 0;
    public bool Finished => Entity.Finished;
    public void ClearHealthAndCollision() => Entity.ClearHealthAndCollision();
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame(frame.Counter);
    public void HandleLinkContact(Player player) => Entity.HandleLinkContact(player);
}
