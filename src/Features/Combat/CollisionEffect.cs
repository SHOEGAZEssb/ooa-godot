namespace oracleofages;

// Source symbols retain their clean-US/Ages values. Keep IDs distinct from table indices,
// masks and counters; byte-oriented asset/state APIs intentionally continue to use integers.
public static class CollisionEffect
{
    // constants/common/collisionEffects.s: COLLISIONEFFECT_NONE
    public const int None = 0x00;
    // constants/common/collisionEffects.s: COLLISIONEFFECT_DAMAGE_LINK
    public const int DamageLink = 0x02;
    // constants/common/collisionEffects.s: COLLISIONEFFECT_05
    public const int Effect05 = 0x05;
    // constants/common/collisionEffects.s: COLLISIONEFFECT_06
    public const int Effect06 = 0x06;
    // constants/common/collisionEffects.s: COLLISIONEFFECT_07
    public const int Effect07 = 0x07;
    // constants/common/collisionEffects.s: COLLISIONEFFECT_SWORD_LOW_KNOCKBACK
    public const int SwordLowKnockback = 0x08;
    // constants/common/collisionEffects.s: COLLISIONEFFECT_SWORD
    public const int Sword = 0x09;
    // constants/common/collisionEffects.s: COLLISIONEFFECT_SWORD_HIGH_KNOCKBACK
    public const int SwordHighKnockback = 0x0a;
    // constants/common/collisionEffects.s: COLLISIONEFFECT_SWORD_NO_KNOCKBACK
    public const int SwordNoKnockback = 0x0b;
    // constants/common/collisionEffects.s: COLLISIONEFFECT_BUMP_LOW_KNOCKBACK
    public const int BumpLowKnockback = 0x0c;
    // constants/common/collisionEffects.s: COLLISIONEFFECT_BUMP
    public const int Bump = 0x0d;
    // constants/common/collisionEffects.s: COLLISIONEFFECT_BUMP_HIGH_KNOCKBACK
    public const int BumpHighKnockback = 0x0e;
    // constants/common/collisionEffects.s: COLLISIONEFFECT_BUMP_WITH_CLINK_HIGH_KNOCKBACK
    public const int BumpWithClinkHighKnockback = 0x14;
    // constants/common/collisionEffects.s: COLLISIONEFFECT_15
    public const int Effect15 = 0x15;
    // constants/common/collisionEffects.s: COLLISIONEFFECT_16
    public const int Effect16 = 0x16;
    // constants/common/collisionEffects.s: COLLISIONEFFECT_17
    public const int Effect17 = 0x17;
    // constants/common/collisionEffects.s: COLLISIONEFFECT_1b
    public const int Effect1b = 0x1b;
    // constants/common/collisionEffects.s: COLLISIONEFFECT_1c
    public const int Effect1c = 0x1c;
    // constants/common/collisionEffects.s: COLLISIONEFFECT_1f
    public const int Effect1f = 0x1f;
    // constants/common/collisionEffects.s: COLLISIONEFFECT_20
    public const int Effect20 = 0x20;
    // constants/common/collisionEffects.s: COLLISIONEFFECT_21
    public const int Effect21 = 0x21;
    // constants/common/collisionEffects.s: COLLISIONEFFECT_STUN
    public const int Stun = 0x22;
    // constants/common/collisionEffects.s: COLLISIONEFFECT_BURN
    public const int Burn = 0x27;
    // constants/common/collisionEffects.s: COLLISIONEFFECT_PEGASUS_SEED
    public const int PegasusSeed = 0x28;
    // constants/common/collisionEffects.s: COLLISIONEFFECT_GALE_SEED
    public const int GaleSeed = 0x29;
    // constants/common/collisionEffects.s: COLLISIONEFFECT_2d
    public const int Effect2d = 0x2d;
    // constants/common/collisionEffects.s: COLLISIONEFFECT_SWITCH_HOOK
    public const int SwitchHook = 0x2e;
    // constants/common/collisionEffects.s: COLLISIONEFFECT_2f
    public const int Effect2f = 0x2f;
    // constants/common/collisionEffects.s: COLLISIONEFFECT_32
    public const int Effect32 = 0x32;
    // constants/common/collisionEffects.s: COLLISIONEFFECT_33
    public const int Effect33 = 0x33;
    // constants/common/collisionEffects.s: COLLISIONEFFECT_34
    public const int Effect34 = 0x34;
    // constants/common/collisionEffects.s: COLLISIONEFFECT_35
    public const int Effect35 = 0x35;
    // constants/common/collisionEffects.s: COLLISIONEFFECT_ELECTRIC_SHOCK
    public const int ElectricShock = 0x36;
    // constants/common/collisionEffects.s: COLLISIONEFFECT_DAMAGE_LINK_WITH_RING_MODIFIER
    public const int DamageLinkWithRingModifier = 0x3c;
}
