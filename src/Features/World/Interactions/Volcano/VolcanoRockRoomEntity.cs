using System.Collections.Generic;

namespace oracleofages;

internal sealed class VolcanoRockRoomEntity(VolcanoRock rock)
    : RoomEntityAdapter<VolcanoRock>(rock, rock.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime, ILinkContactEntity
{
    public bool Finished => Entity.Finished;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame(frame, spawns);
    public void HandleLinkContact(Player player) => Entity.HandleLinkContact(player);
}
