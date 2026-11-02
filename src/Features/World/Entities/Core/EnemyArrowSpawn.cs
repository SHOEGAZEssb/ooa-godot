
namespace Oracles;

internal sealed record EnemyArrowSpawn(Vector2 Position, int Angle, int SubId = 0)
    : RoomEntitySpawn(UpdateThisFrame: true);
