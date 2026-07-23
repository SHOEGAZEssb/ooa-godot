using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed record PuzzlePuffSpawn(Vector2 Position, int Sound)
    : RoomEntitySpawn(UpdateThisFrame: true);
