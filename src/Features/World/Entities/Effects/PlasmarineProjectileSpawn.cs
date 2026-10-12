using Godot;

namespace oracleofages;

internal sealed record PlasmarineProjectileSpawn(Vector2 Position,int ColorIndex,int ParentSlot):RoomEntitySpawn;
