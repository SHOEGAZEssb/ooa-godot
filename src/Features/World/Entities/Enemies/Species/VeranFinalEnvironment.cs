using Godot;
using System;

namespace Oracles;

internal sealed record VeranFinalEnvironment(VeranFinalDatabase Data,RoomEntityManager Entities,OracleRoomData Room,
    Func<VeranFinalCharacter,int,int,bool> SpawnPart,Action<VeranFinalCharacter,int> SpawnBee,
    Func<VeranFinalCharacter,bool> DeathFade,Action BeginEscape);
