using Godot;
using System;

namespace Oracles;

internal sealed record BlueStalfosEnvironment(OracleRoomData Room, OracleRandom Random,
    Action InitializeRoom, Action BeginBattle, Func<bool> CanSpawnChildren,
    Func<BlueStalfosCharacter, int, bool, BlueStalfosCharacter?> SpawnChild,
    Func<int, BlueStalfosCharacter?> ResolveEnemy,
    Func<BlueStalfosCharacter, bool> Shadow, Func<Vector2, int, int> Puff,
    Func<int, int> PuffParameter, Func<BlueStalfosCharacter, bool> Projectile,
    Action DisableLinkCollisionsAndMenu, Func<BlueStalfosCharacter, bool> Explosion,
    Action RestoreMusic, Action<int> Sound, BossEntryMovement Entry);
