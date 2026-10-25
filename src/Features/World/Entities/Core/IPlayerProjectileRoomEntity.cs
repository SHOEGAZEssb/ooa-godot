using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal interface IPlayerProjectileRoomEntity
{
    bool CollisionEnabled { get; }
    Rect2 CollisionBounds { get; }
    int Damage { get; }
    void OnEnemyCollision(ICollection<RoomEntitySpawn> spawns);
}
