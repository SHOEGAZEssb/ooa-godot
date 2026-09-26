namespace oracleofages;

// Source symbols retain their clean-US/Ages values. Keep IDs distinct from table indices,
// masks and counters; byte-oriented asset/state APIs intentionally continue to use integers.
public static class ItemCollisionType
{
    // constants/common/itemCollisionTypes.s: ITEMCOLLISION_LINK
    public const int Link = 0x00;
    // constants/common/itemCollisionTypes.s: ITEMCOLLISION_L1_SWORD
    public const int L1Sword = 0x04;
    // constants/common/itemCollisionTypes.s: ITEMCOLLISION_L2_SWORD
    public const int L2Sword = 0x05;
    // constants/common/itemCollisionTypes.s: ITEMCOLLISION_SWORDSPIN
    public const int SwordSpin = 0x08;
    // constants/common/itemCollisionTypes.s: ITEMCOLLISION_SWORD_HELD
    public const int SwordHeld = 0x09;
    // constants/common/itemCollisionTypes.s: ITEMCOLLISION_EXPERT_PUNCH
    public const int ExpertPunch = 0x0b;
    // constants/common/itemCollisionTypes.s: ITEMCOLLISION_SWITCH_HOOK
    public const int SwitchHook = 0x0d;
    // constants/common/itemCollisionTypes.s: ITEMCOLLISION_SOMARIA_BLOCK
    public const int SomariaBlock = 0x15;
    // constants/common/itemCollisionTypes.s: ITEMCOLLISION_THROWN_OBJECT
    public const int ThrownObject = 0x16;
    // constants/common/itemCollisionTypes.s: ITEMCOLLISION_L1_BOOMERANG
    public const int L1Boomerang = 0x17;
    // constants/common/itemCollisionTypes.s: ITEMCOLLISION_BOMB
    public const int Bomb = 0x18;
    // constants/common/itemCollisionTypes.s: ITEMCOLLISION_SWORD_BEAM
    public const int SwordBeam = 0x19;
    // constants/common/itemCollisionTypes.s: ITEMCOLLISION_MYSTERY_SEED
    public const int MysterySeed = 0x1a;
    // constants/common/itemCollisionTypes.s: ITEMCOLLISION_EMBER_SEED
    public const int EmberSeed = 0x1b;
    // constants/common/itemCollisionTypes.s: ITEMCOLLISION_SCENT_SEED
    public const int ScentSeed = 0x1c;
    // constants/common/itemCollisionTypes.s: ITEMCOLLISION_PEGASUS_SEED
    public const int PegasusSeed = 0x1d;
    // constants/common/itemCollisionTypes.s: ITEMCOLLISION_GALE_SEED
    public const int GaleSeed = 0x1e;
}
