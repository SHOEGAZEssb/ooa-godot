using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class CollisionPerformanceProbe(int id, bool enabled = false) :
    IRoomEntity, ISeedProjectileRoomEntity, ISeedHittableRoomEntity, IPlayerProjectileRoomEntity
{
    public Node2D Node { get; } = new();
    internal Action<Vector2>? Hit { get; set; }
    public bool CollisionEnabled => enabled;
    public int CollisionZ => 0;
    public int SeedItem => ItemId.EmberSeed;
    public SeedRecord Record => default;
    public int CollisionType => 0;
    public int Damage => 1;
    public Vector2? ScentTarget => null;
    public Rect2 CollisionBounds => new(id, 0, 0, 0);
    public void SetTransitionDrawOffset(Vector2 offset) { }
    public void QueueNativeCollision(SeedCollisionResponse response) { }
    public void OnCollision(SeedHitResult result, ISeedBurnTarget? burnTarget,
        ISeedBounceTarget? bounceTarget, ICollection<RoomEntitySpawn> spawns, bool beforeItemUpdate) { }
    public void OnEnemyCollision(ICollection<RoomEntitySpawn> spawns) { }
    public SeedHitResult ApplySeedHit(Rect2 hitbox, Vector2 sourcePosition,
        int seedItem, ICollection<RoomEntitySpawn> spawns)
    {
        Hit?.Invoke(sourcePosition);
        return SeedHitResult.None;
    }
}
