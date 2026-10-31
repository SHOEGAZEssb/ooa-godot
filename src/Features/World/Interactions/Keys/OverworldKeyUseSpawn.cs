using Godot;

namespace Oracles;

internal sealed record OverworldKeyUseSpawn(
    Vector2 Position,
    OverworldKeyholeDatabaseRecord Visual,
    ConstantsRecord Constants) : RoomEntitySpawn;
