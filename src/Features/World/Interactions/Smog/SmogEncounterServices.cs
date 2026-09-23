using Godot;
using System;

namespace oracleofages;

// Every callback reaches a live world owner; the adapter reads Link's
// native-state gates directly from the active Player owner.
internal sealed record SmogEncounterServices(
    Func<int> RoomFlags, Func<bool> EntryBusy, Func<int> EnemyCount,
    Action LockLinkAndMenu, Action EnableLinkCollisionsAndMenu,
    Action<bool> SetResetFlag, Action<SmogEnemySpawn> Spawn,
    Action<Vector2I> Puff, Func<int,byte> Collision, Func<Vector2I,byte> Tile,
    Func<int,int,bool> SetTile, Action<int> MergeClouds,
    Action PlayResetSound, Action DecrementEnemyCount);
