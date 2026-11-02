
namespace Oracles;

internal sealed record PuzzlePuffSpawn(
    Vector2 Position,
    int Sound,
    bool Flickers = false,
    int ZHigh = 0,
    bool AlwaysUpdates = true)
    : RoomEntitySpawn(UpdateThisFrame: true);
