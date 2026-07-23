using Godot;

namespace oracleofages;

public readonly record struct ActiveTerrainInfo(
    TerrainInfo Terrain,
    Vector2 SamplePoint,
    Vector2 TileCenter,
    int PackedPosition);
