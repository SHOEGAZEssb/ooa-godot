using Godot;

namespace oracleofages;

internal sealed record BlueStalfosProjectileSpawn(Vector2 Position, int Z, int ParentSlot, int Angle, int SubId = 0) : RoomEntitySpawn;
