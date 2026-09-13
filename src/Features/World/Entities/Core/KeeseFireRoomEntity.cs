using System.Collections.Generic;

namespace oracleofages;

internal sealed class KeeseFireRoomEntity(KeeseFirePart fire)
    : RoomEntityAdapter<KeeseFirePart>(fire, fire.SetTransitionDrawOffset), IFixedRoomEntity, IRoomEntityLifetime, ILinkContactEntity,
        INativePartHealthRoomEntity
{
    public void ClearHealthAndCollision() => Entity.ClearHealthAndCollision();
    public bool Finished => Entity.Finished;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame();
    public void HandleLinkContact(Player player) => Entity.HandleLinkContact(player);
}
