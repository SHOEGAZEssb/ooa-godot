using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal interface ISeedProjectileRoomEntity
{
    bool CollisionEnabled { get; }
    Rect2 CollisionBounds { get; }
    void OnCollision(SeedHitResult result, ISeedBurnTarget? burnTarget);
}
