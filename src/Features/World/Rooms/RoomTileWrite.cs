using Godot;

namespace oracleofages;

internal readonly record struct RoomTileWrite(Vector2 Position, byte Tile, byte? Collision);
