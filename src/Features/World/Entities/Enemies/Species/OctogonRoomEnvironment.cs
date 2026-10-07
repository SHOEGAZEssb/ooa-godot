using System;
using Godot;

namespace oracleofages;

internal sealed record OctogonRoomEnvironment(Action<bool> InitializeBossRoom,Func<int> ShutterSignal,
    Action<int> Sound,Action DisableLinkCollisionsAndMenu,Action RestoreMusic,
    Action MarkBothBossRooms,Func<OctogonCharacter,int> CreateShell,Func<int,OctogonCharacter?> Resolve,
    Func<RoomEntitySpawn,bool> Part,Func<Vector2,int,int,int,bool> Interaction,BossEntryMovement Entry);
