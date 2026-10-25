namespace Oracles;

internal sealed record GoronCaveNpcSpawn(NpcRecord Record, bool Interactive = true) : RoomEntitySpawn;
