using Godot;
using System;

namespace oracleofages;

internal sealed record SmogRoomEnvironment(
    Func<bool> Frozen, Func<bool> TextActive, Func<int> RoomFlags, Func<int> EnemyCount,
    Func<int> NextRandom, Action BeginBoss, Action<int,Vector2> ShowText,
    Action<SmogEnemySpawn> SpawnEnemy, Func<Vector2,bool> CreatePuff, Func<bool> PartSlotAvailable,
    Action<int,int> WriteInteractionCounter, Action<int,int> SetTile,
    Action DisableLinkCollisionsAndMenu, Action RestoreRoomMusic,
    Func<bool,int>? InitializeBossRoom = null, BossEntryMovement? Entry = null,
    Func<int>? FrameCounter = null,
    Action<Vector2, int>? WriteFailedProjectile = null);

internal sealed record SmogEnemySpawn(Vector2 Position, int SubId, int Phase = 0, int Direction = 0) : RoomEntitySpawn;
