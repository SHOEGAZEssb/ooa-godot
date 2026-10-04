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
    public SeedRecord Record => Entity.Record;
    public int CollisionType => Entity.CollisionType;
    internal SeedLaunchKind LaunchKind => Entity.LaunchKind;
    // Only collisionEffect0c's attached burning enemy is PART$12. A landed
    // Ember seed stays ITEM$20 throughout emberSeedBurn and retains its slot.
    internal bool IsFlamePart => Entity.IsBurningEnemy;
    public Vector2? ScentTarget => Entity.ScentTarget;
    public Rect2 CollisionBounds => Entity.CollisionBounds;
    public void QueueNativeCollision(SeedCollisionResponse response) => Entity.QueueNativeCollision(response);
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        Entity.GalePlayer = frame.Player;
        Entity.UpdateFrame(frame.Counter, spawns);
    }
    public void OnCollision(
        SeedHitResult result,
        ISeedBurnTarget? burnTarget,
        ISeedBounceTarget? bounceTarget,
        ICollection<RoomEntitySpawn> spawns, bool beforeItemUpdate) =>
        Entity.OnCollision(result, burnTarget, bounceTarget, spawns, beforeItemUpdate);
}
