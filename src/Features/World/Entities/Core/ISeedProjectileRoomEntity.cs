using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal interface ISeedProjectileRoomEntity
{
    bool CollisionEnabled { get; }
    int CollisionZ { get; }
    int SeedItem { get; }
    Vector2? ScentTarget { get; }
    Rect2 CollisionBounds { get; }
    void OnCollision(
        SeedHitResult result,
        ISeedBurnTarget? burnTarget,
        ISeedBounceTarget? bounceTarget);
}
