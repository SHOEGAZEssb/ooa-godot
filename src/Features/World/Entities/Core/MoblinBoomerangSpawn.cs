using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed record MoblinBoomerangSpawn(
    BoomerangMoblinCharacter Owner,
    Vector2 Position,
    int Angle) : RoomEntitySpawn(UpdateThisFrame: true);
