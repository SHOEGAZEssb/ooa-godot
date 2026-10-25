using Godot;

namespace Oracles;

internal sealed record LynelBeamSpawn(Vector2 Position, int Angle, int ZHigh = 0) : RoomEntitySpawn;
