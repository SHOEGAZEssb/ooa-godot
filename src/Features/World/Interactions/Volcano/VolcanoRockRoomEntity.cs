using System.Collections.Generic;

namespace Oracles;

internal sealed class VolcanoRockRoomEntity(VolcanoRock rock)
    : RoomEntityAdapter<VolcanoRock>(rock, rock.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime, ILinkContactEntity,
        IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    public bool UpdatesDuringDialogue => Entity.State == 0;
    public bool UpdatesDuringRoomEntityFreeze => Entity.State == 0;
    public bool Finished => Entity.Finished;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame(frame, spawns);
    public void HandleLinkContact(Player player) => Entity.HandleLinkContact(player);
}
