using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class EmberSeedRoomEntity(EmberSeedEffect seed)
    : RoomEntityAdapter<EmberSeedEffect>(seed, seed.SetTransitionDrawOffset),
        IFixedRoomEntity, ISeedProjectileRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.Finished;
    public bool CollisionEnabled => Entity.CollisionEnabled;
    public int SeedItem => Entity.SeedItem;
    internal SeedLaunchKind LaunchKind => Entity.LaunchKind;
    public Vector2? ScentTarget => Entity.ScentTarget;
    public Rect2 CollisionBounds => Entity.CollisionBounds;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Counter, spawns);
    public void OnCollision(SeedHitResult result, ISeedBurnTarget? burnTarget) =>
        Entity.OnCollision(result, burnTarget);
}
