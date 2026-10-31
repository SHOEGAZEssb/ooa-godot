using Godot;

namespace Oracles;

internal sealed record EmberSeedSpawn(
    Vector2 LinkPosition,
    Vector2I Direction,
    SeedRecord Record,
    int Group,
    SeedLaunchKind LaunchKind = SeedLaunchKind.Satchel,
    int Angle = ObjectAngle.Up,
    int LinkZFixed = 0)
    : RoomEntitySpawn;
