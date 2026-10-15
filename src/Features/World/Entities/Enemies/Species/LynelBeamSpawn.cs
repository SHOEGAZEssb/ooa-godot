using Godot;

namespace oracleofages;

internal sealed record LynelBeamSpawn(Vector2 Position, int Angle, int ZHigh = 0) : RoomEntitySpawn;
