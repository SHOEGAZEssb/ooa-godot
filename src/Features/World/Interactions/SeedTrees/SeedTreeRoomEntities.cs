using System.Collections.Generic;

namespace oracleofages;

internal sealed class SeedTreeControllerRoomEntity(
    SeedTreeController controller)
    : RoomEntityAdapter<SeedTreeController>(
        controller, controller.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.Finished;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame();

}

internal sealed class SeedOnTreeRoomEntity(SeedOnTree seed)
    : RoomEntityAdapter<SeedOnTree>(
        seed, seed.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime,
        ILinkSwordCollectibleRoomEntity
{
    public bool Finished => Entity.Finished;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player);

    public bool TryCollectWithSword(Godot.Rect2 hitbox) =>
        Entity.TryCollectWithSword(hitbox);

}
