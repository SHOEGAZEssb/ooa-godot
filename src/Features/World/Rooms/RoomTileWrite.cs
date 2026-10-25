using Godot;

namespace Oracles;

internal readonly record struct RoomTileWrite(Vector2 Position, byte Tile, byte? Collision);
