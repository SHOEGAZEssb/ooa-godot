namespace oracleofages;

// Source symbols retain their clean-US/Ages values. Keep IDs distinct from table indices,
// masks and counters; byte-oriented asset/state APIs intentionally continue to use integers.
public static class EnemyId
{
    // constants/common/enemies.s: ENEMY_STUB_00
    public const int Stub00 = 0x00;
    // constants/common/enemies.s: ENEMY_RIVER_ZORA
    public const int RiverZora = 0x08;
    // constants/common/enemies.s: ENEMY_OCTOROK
    public const int Octorok = 0x09;
    // constants/common/enemies.s: ENEMY_BOOMERANG_MOBLIN
    public const int BoomerangMoblin = 0x0a;
    // constants/common/enemies.s: ENEMY_LEEVER
    public const int Leever = 0x0b;
    // constants/common/enemies.s: ENEMY_ARROW_MOBLIN
    public const int ArrowMoblin = 0x0c;
    // constants/common/enemies.s: ENEMY_BLADE_TRAP
    public const int BladeTrap = 0x0e;
    // constants/common/enemies.s: ENEMY_ROPE
    public const int Rope = 0x10;
    // constants/ages/enemies.s: ENEMY_EYESOAR_CHILD
    public const int EyesoarChild = 0x11;
    // constants/common/enemies.s: ENEMY_GIBDO
    public const int Gibdo = 0x12;
    // constants/common/enemies.s: ENEMY_SPARK
    public const int Spark = 0x13;
    // constants/common/enemies.s: ENEMY_SPIKED_BEETLE
    public const int SpikedBeetle = 0x14;
    // constants/common/enemies.s: ENEMY_BEAMOS
    public const int Beamos = 0x16;
    // constants/common/enemies.s: ENEMY_GHINI
    public const int Ghini = 0x17;
    // constants/common/enemies.s: ENEMY_BUZZBLOB
    public const int Buzzblob = 0x18;
    // constants/common/enemies.s: ENEMY_SAND_CRAB
    public const int SandCrab = 0x1a;
    // constants/common/enemies.s: ENEMY_SPINY_BEETLE
    public const int SpinyBeetle = 0x1b;
    // constants/common/enemies.s: ENEMY_ARMOS
    public const int Armos = 0x1d;
    // constants/common/enemies.s: ENEMY_MASKED_MOBLIN
    public const int MaskedMoblin = 0x20;
    // constants/common/enemies.s: ENEMY_ARROW_DARKNUT
    public const int ArrowDarknut = 0x21;
    // constants/common/enemies.s: ENEMY_ARROW_SHROUDED_STALFOS
    public const int ArrowShroudedStalfos = 0x22;
    // constants/common/enemies.s: ENEMY_POLS_VOICE
    public const int PolsVoice = 0x23;
    // constants/common/enemies.s: ENEMY_LIKE_LIKE
    public const int LikeLike = 0x24;
    // constants/common/enemies.s: ENEMY_GOPONGA_FLOWER
    public const int GopongaFlower = 0x25;
    // constants/common/enemies.s: ENEMY_WALLMASTER
    public const int Wallmaster = 0x28;
    // constants/common/enemies.s: ENEMY_CHEEP_CHEEP
    public const int CheepCheep = 0x2c;
    // constants/common/enemies.s: ENEMY_PODOBOO_TOWER
    public const int PodobooTower = 0x2d;
    // constants/common/enemies.s: ENEMY_TEKTITE
    public const int Tektite = 0x30;
    // constants/common/enemies.s: ENEMY_STALFOS
    public const int Stalfos = 0x31;
    // constants/common/enemies.s: ENEMY_KEESE
    public const int Keese = 0x32;
    // constants/common/enemies.s: ENEMY_BABY_CUCCO
    public const int BabyCucco = 0x33;
    // constants/common/enemies.s: ENEMY_ZOL
    public const int Zol = 0x34;
    // constants/common/enemies.s: ENEMY_CUCCO
    public const int Cucco = 0x36;
    // constants/common/enemies.s: ENEMY_GREAT_FAIRY
    public const int GreatFairy = 0x38;
    // constants/common/enemies.s: ENEMY_FIRE_KEESE
    public const int FireKeese = 0x39;
    // constants/common/enemies.s: ENEMY_GIANT_CUCCO
    public const int GiantCucco = 0x3b;
    // constants/common/enemies.s: ENEMY_SWORD_MOBLIN
    public const int SwordMoblin = 0x3d;
    // constants/common/enemies.s: ENEMY_PEAHAT
    public const int Peahat = 0x3e;
    // constants/ages/enemies.s: ENEMY_GIANT_GHINI_CHILD
    public const int GiantGhiniChild = 0x3f;
    // constants/common/enemies.s: ENEMY_CROW
    public const int Crow = 0x41;
    // constants/ages/enemies.s: ENEMY_SHADOW_HAG_BUG
    public const int ShadowHagBug = 0x42;
    // constants/common/enemies.s: ENEMY_GEL
    public const int Gel = 0x43;
    // constants/ages/enemies.s: ENEMY_COLOR_CHANGING_GEL
    public const int ColorChangingGel = 0x47;
    // constants/common/enemies.s: ENEMY_SWORD_DARKNUT
    public const int SwordDarknut = 0x48;
    // constants/common/enemies.s: ENEMY_SWORD_SHROUDED_STALFOS
    public const int SwordShroudedStalfos = 0x49;
    // constants/common/enemies.s: ENEMY_SWORD_MASKED_MOBLIN
    public const int SwordMaskedMoblin = 0x4a;
    // constants/common/enemies.s: ENEMY_BALL_AND_CHAIN_SOLDIER
    public const int BallAndChainSoldier = 0x4b;
    // constants/common/enemies.s: ENEMY_HARDHAT_BEETLE
    public const int HardhatBeetle = 0x4d;
    // constants/common/enemies.s: ENEMY_ARM_MIMIC
    public const int ArmMimic = 0x4e;
    // constants/common/enemies.s: ENEMY_MOLDORM
    public const int Moldorm = 0x4f;
    // constants/common/enemies.s: ENEMY_FIREBALL_SHOOTER
    public const int FireballShooter = 0x50;
    // constants/common/enemies.s: ENEMY_BEETLE
    public const int Beetle = 0x51;
    // constants/common/enemies.s: ENEMY_FLYING_TILE
    public const int FlyingTile = 0x52;
    // constants/ages/enemies.s: ENEMY_HARMLESS_HARDHAT_BEETLE
    public const int HarmlessHardhatBeetle = 0x5f;
    // constants/ages/enemies.s: ENEMY_VINE_SPROUT
    public const int VineSprout = 0x62;
    // constants/ages/enemies.s: ENEMY_TARGET_CART_CRYSTAL
    public const int TargetCartCrystal = 0x63;
    // constants/ages/enemies.s: ENEMY_LINK_MIMIC
    public const int LinkMimic = 0x64;
    // constants/ages/enemies.s: ENEMY_GIANT_GHINI
    public const int GiantGhini = 0x70;
    // constants/ages/enemies.s: ENEMY_SWOOP
    public const int Swoop = 0x71;
    // constants/ages/enemies.s: ENEMY_SUBTERROR
    public const int Subterror = 0x72;
    // constants/ages/enemies.s: ENEMY_ARMOS_WARRIOR
    public const int ArmosWarrior = 0x73;
    // constants/ages/enemies.s: ENEMY_SMASHER
    public const int Smasher = 0x74;
    // constants/ages/enemies.s: ENEMY_PUMPKIN_HEAD
    public const int PumpkinHead = 0x78;
    // constants/ages/enemies.s: ENEMY_HEAD_THWOMP
    public const int HeadThwomp = 0x79;
    // constants/ages/enemies.s: ENEMY_SHADOW_HAG
    public const int ShadowHag = 0x7a;
    // constants/ages/enemies.s: ENEMY_EYESOAR
    public const int Eyesoar = 0x7b;
    // constants/ages/enemies.s: ENEMY_SMOG
    public const int Smog = 0x7c;
    // constants/ages/enemies.s: ENEMY_KING_MOBLIN
    public const int KingMoblin = 0x7f;
}
