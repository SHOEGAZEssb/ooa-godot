using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class KingMoblinBombRoomEntity(KingMoblinBomb actor)
    : RoomEntityAdapter<KingMoblinBomb>(actor,actor.SetTransitionDrawOffset), IFixedRoomEntity, IRoomEntityLifetime, IBraceletInteractableRoomEntity, IBraceletChildRoomEntity, IPostObjectLinkContactRoomEntity
{
    public bool ReservedBraceletChildActive => Entity.ReservedBraceletChildActive;
    public bool Finished => Entity.IsDead;
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame(frame);
    public bool TryUseBracelet(Player player,Vector2I direction) => Entity.TryUseBracelet(player,direction);
    public void UpdateBraceletChild(Player player) => Entity.UpdateBraceletChild(player);
    public void HandleLinkContact(Player player) => Entity.HandleLinkContact(player);
}
