using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class WildTokayMeatRoomEntity(WildTokayMeat meat)
    : RoomEntityAdapter<WildTokayMeat>(meat, meat.SetTransitionDrawOffset),
        IFixedRoomEntity, IBraceletInteractableRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.Finished;
    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame(frame.Player);
    public bool TryUseBracelet(Player player, Vector2I releaseDirection) =>
        Entity.TryUseBracelet(player, releaseDirection);
}

internal sealed record WildTokayMeatSpawn : RoomEntitySpawn;
