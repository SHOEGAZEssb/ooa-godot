using Godot;

namespace oracleofages;

internal sealed record TingleKoolooSparkleSpawn(
    Vector2 Position, int SourceAngle, InteractionSparkleVisual Visual) : RoomEntitySpawn;
