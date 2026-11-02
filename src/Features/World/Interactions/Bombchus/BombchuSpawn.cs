
namespace Oracles;

internal sealed record BombchuSpawn(Player Player, int Group, Vector2 Position, int ZFixed) : RoomEntitySpawn;
