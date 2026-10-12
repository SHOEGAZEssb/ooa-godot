using Godot;
using System;

namespace oracleofages;

internal sealed record PlasmarineEnvironment(OracleRandom Random,Action InitializeRoom,Action BeginBattle,
    Func<PlasmarineCharacter,bool> Projectile,Action DisableLinkCollisionsAndMenu,
    Func<Vector2,bool> Explosion,Action<int> Sound,BossEntryMovement Entry);
