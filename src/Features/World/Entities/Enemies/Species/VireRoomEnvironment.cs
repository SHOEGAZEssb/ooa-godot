using System;
using Godot;

namespace oracleofages;

internal sealed record VireRoomEnvironment(
    Action<bool> InitializeBossRoom, Action BeginBattle, Action DisableLinkCollisionsAndMenu,
    Action EnableLinkCollisionsAndMenu, Action RestoreMusic, Action<int> Sound,
    Action<int,Vector2> ShowText, Func<bool> Linked,
    Func<Vector2> Camera, Func<Vector2,int,int,int> Puff, Func<int,int> PuffParameter,
    Func<VireProjectileSpawn,bool> Projectile, Func<int,bool> CanAllocateEnemies,
    Func<VireCharacter,int,bool> Bat, Func<int,VireCharacter?> Parent,
    Func<RoomEntitySpawn,bool> Part, BossEntryMovement Entry);
