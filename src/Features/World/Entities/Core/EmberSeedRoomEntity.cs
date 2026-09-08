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
    public int CollisionZ => Entity.CollisionZ;
    public int SeedItem => Entity.SeedItem;
    internal SeedLaunchKind LaunchKind => Entity.LaunchKind;
    internal bool IsFlamePart => Entity.State == EmberState.Burning;
    public Vector2? ScentTarget => Entity.ScentTarget;
    public Rect2 CollisionBounds => Entity.CollisionBounds;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        Entity.GalePlayer = frame.Player;
        Entity.UpdateFrame(frame.Counter, spawns);
    }
    public void OnCollision(
        SeedHitResult result,
        ISeedBurnTarget? burnTarget,
        ISeedBounceTarget? bounceTarget,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.OnCollision(result, burnTarget, bounceTarget, spawns);
}
