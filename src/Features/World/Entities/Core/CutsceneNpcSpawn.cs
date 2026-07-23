using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed record CutsceneNpcSpawn(
    NpcRecord Record,
    string Name,
    bool Talkable = false,
    bool Solid = false)
    : RoomEntitySpawn;
