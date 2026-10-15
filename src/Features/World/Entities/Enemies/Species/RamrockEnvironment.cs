using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed record RamrockEnvironment(OracleRoomData Room, OracleRuntimeState Memory, OracleRandom Random,
    Action InitializeRoom, Action BeginBattle, Func<bool> EntryActive,
    Func<RamrockCharacter, int, RamrockCharacter?> SpawnHand,
    Func<int, RamrockCharacter?> ResolveEnemy, Action<int> Palette,
    Func<RamrockCharacter, bool> Shadow, Action<Vector2, int> Puff,
    Func<RamrockPartSpawn, bool> SpawnPart, Func<IReadOnlyList<BombEffect>> Bombs,
    Func<Vector2, Vector2> ToScreen,
    Action DisableLinkCollisionsAndMenu, Func<RamrockCharacter, bool> Explosion,
    Action<int> Sound, BossEntryMovement Entry);
