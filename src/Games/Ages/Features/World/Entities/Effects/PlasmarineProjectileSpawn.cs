using Godot;

namespace Oracles;

internal sealed record PlasmarineProjectileSpawn(Vector2 Position,int ColorIndex,int ParentSlot):RoomEntitySpawn;
