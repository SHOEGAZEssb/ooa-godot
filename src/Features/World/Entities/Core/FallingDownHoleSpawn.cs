using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed record FallingDownHoleSpawn(Vector2 Position) : RoomEntitySpawn;
