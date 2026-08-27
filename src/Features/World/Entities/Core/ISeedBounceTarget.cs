using Godot;

namespace oracleofages;

/// <summary>
/// PART_ROTATABLE_SEED_THING's collision-effect-$2a contract. The current
/// animation parameter is the reflector orientation consumed by func_50f4.
/// </summary>
internal interface ISeedBounceTarget
{
    int SeedBounceOrientation { get; }
    bool IntersectsSeed(Rect2 hitbox);
}
