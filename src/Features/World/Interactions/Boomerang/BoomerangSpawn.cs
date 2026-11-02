
namespace Oracles;

internal sealed record BoomerangSpawn(Vector2 Position, int Angle, int ZHigh = 0, int SubId = 0,
    int ParentSlot = 0, Action<Vector2>? BreakTile = null) : RoomEntitySpawn;
