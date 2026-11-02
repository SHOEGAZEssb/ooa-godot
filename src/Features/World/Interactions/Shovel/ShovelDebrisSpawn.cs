
namespace Oracles;

internal sealed record ShovelDebrisSpawn(Vector2 Position, Vector2I Direction) : RoomEntitySpawn(UpdateThisFrame: true);
