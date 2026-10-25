using Godot;

namespace Oracles;

internal sealed record TingleKoolooSparkleSpawn(
    Vector2 Position, int SourceAngle, InteractionSparkleVisual Visual) : RoomEntitySpawn;
