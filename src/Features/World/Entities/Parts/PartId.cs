namespace oracleofages;

// Source symbols retain their clean-US/Ages values. Keep IDs distinct from table indices,
// masks and counters; byte-oriented asset/state APIs intentionally continue to use integers.
public static class PartId
{
    // constants/common/parts.s: PART_RESPAWNABLE_BUSH
    public const int RespawnableBush = 0x0f;
    // constants/common/parts.s: PART_OWL_STATUE
    public const int OwlStatue = 0x13;
    // constants/common/parts.s: PART_ZORA_FIRE
    public const int ZoraFire = 0x19;
    // constants/common/parts.s: PART_FALLING_FIRE
    public const int FallingFire = 0x23;
    // constants/common/parts.s: PART_SMALL_FAIRY
    public const int SmallFairy = 0x28;
    // constants/common/parts.s: PART_GOPONGA_PROJECTILE
    public const int GopongaProjectile = 0x31;
    // constants/common/parts.s: PART_WALL_ARROW_SHOOTER
    public const int WallArrowShooter = 0x25;
}
