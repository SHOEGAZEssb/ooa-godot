using Godot;
using System;

namespace oracleofages;

internal sealed record AnglerFishEnvironment(OracleRoomData Room,OracleRandom Random,
    Action<bool> InitializeBossRoom,Func<AnglerFishCharacter,AnglerFishCharacter?> Antenna,
    Func<AnglerFishCharacter,bool> Bubble,Action<int,int> Tile,Action DisableLink,Action BeginBattle,
    Action DisableCollisionsAndMenu,Func<Vector2,bool> Explosion,Action RestoreMusic,
    Action<int> Sound,Func<Vector2,bool> ScentDrop,Func<Vector2,bool> Deflation,
    BossEntryMovement Entry);
