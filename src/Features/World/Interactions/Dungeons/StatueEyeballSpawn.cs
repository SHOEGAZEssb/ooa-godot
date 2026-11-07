namespace Oracles;

internal sealed record StatueEyeballSpawn(Vector2 Position, bool Puzzle = false)
    : RoomEntitySpawn(UpdateThisFrame: true);
