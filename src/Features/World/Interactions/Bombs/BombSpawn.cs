using System;

namespace Oracles;

internal sealed record BombSpawn(
    Player Player,
    BombRecord Record,
    int Group,
    Action<BombEffect> HeldExplosion) : RoomEntitySpawn;
