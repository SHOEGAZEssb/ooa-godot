namespace oracleofages;

// Source symbols retain their clean-US/Ages values. Keep IDs distinct from table indices,
// masks and counters; byte-oriented asset/state APIs intentionally continue to use integers.
public static class EnemyCollisionMode
{
    // constants/common/enemyCollisionModes.s: ENEMYCOLLISION_00
    public const int Mode00 = 0x00;
    // constants/common/enemyCollisionModes.s: ENEMYCOLLISION_ITEM
    public const int Item = 0x01;
    // constants/common/enemyCollisionModes.s: ENEMYCOLLISION_DORMANT
    public const int Dormant = 0x02;
    // constants/common/enemyCollisionModes.s: ENEMYCOLLISION_PROJECTILE
    public const int Projectile = 0x06;
    // constants/common/enemyCollisionModes.s: ENEMYCOLLISION_PROJECTILE_WITH_RING_MOD
    public const int ProjectileWithRingMod = 0x07;
    // constants/common/enemyCollisionModes.s: ENEMYCOLLISION_BURNABLE_ENEMY
    public const int BurnableEnemy = 0x11;
    // constants/common/enemyCollisionModes.s: ENEMYCOLLISION_BLADE_TRAP
    public const int BladeTrap = 0x13;
    // constants/common/enemyCollisionModes.s: ENEMYCOLLISION_SWITCHHOOK_DAMAGE_ENEMY
    public const int SwitchHookDamageEnemy = 0x14;
    // constants/common/enemyCollisionModes.s: ENEMYCOLLISION_EYESOAR_CHILD
    public const int EyesoarChild = 0x15;
    // constants/common/enemyCollisionModes.s: ENEMYCOLLISION_GIBDO
    public const int Gibdo = 0x16;
    // constants/common/enemyCollisionModes.s: ENEMYCOLLISION_KEESE
    public const int Keese = 0x1f;
    // constants/common/enemyCollisionModes.s: ENEMYCOLLISION_DARKNUT
    public const int Darknut = 0x20;
    // constants/common/enemyCollisionModes.s: ENEMYCOLLISION_ZOL
    public const int Zol = 0x29;
    // constants/common/enemyCollisionModes.s: ENEMYCOLLISION_FIRE_KEESE
    public const int FireKeese = 0x2b;
    // constants/common/enemyCollisionModes.s: ENEMYCOLLISION_PEAHAT_VULNERABLE
    public const int PeahatVulnerable = 0x2e;
    // constants/common/enemyCollisionModes.s: ENEMYCOLLISION_MOLDORM
    public const int Moldorm = 0x3a;
    // constants/common/enemyCollisionModes.s: ENEMYCOLLISION_BUSH
    public const int Bush = 0x40;
    // constants/common/enemyCollisionModes.s: ENEMYCOLLISION_STANDARD_MINIBOSS
    public const int StandardMiniboss = 0x44;
    // constants/common/enemyCollisionModes.s: ENEMYCOLLISION_SMASHER
    public const int Smasher = 0x45;
    // constants/common/enemyCollisionModes.s: ENEMYCOLLISION_EYESOAR_VULNERABLE
    public const int EyesoarVulnerable = 0x4c;
    // constants/common/enemyCollisionModes.s: ENEMYCOLLISION_SMOG
    public const int Smog = 0x4d;
    // constants/common/enemyCollisionModes.s: ENEMYCOLLISION_KING_MOBLIN
    public const int KingMoblin = 0x50;
    // constants/common/enemyCollisionModes.s: ENEMYCOLLISION_SPIKED_BEETLE_FLIPPED
    public const int SpikedBeetleFlipped = 0x51;
    // constants/common/enemyCollisionModes.s: ENEMYCOLLISION_ROCK
    public const int Rock = 0x52;
    // constants/common/enemyCollisionModes.s: ENEMYCOLLISION_STALFOS_BLOCKED_WITH_SWORD
    public const int StalfosBlockedWithSword = 0x55;
    // constants/common/enemyCollisionModes.s: ENEMYCOLLISION_DARKNUT_BLOCKED_WITH_SWORD
    public const int DarknutBlockedWithSword = 0x56;
    // constants/common/enemyCollisionModes.s: ENEMYCOLLISION_PEAHAT
    public const int Peahat = 0x58;
    // constants/common/enemyCollisionModes.s: ENEMYCOLLISION_ARMOS_WARRIOR_PROTECTED
    public const int ArmosWarriorProtected = 0x60;
    // constants/common/enemyCollisionModes.s: ENEMYCOLLISION_ARMOS_WARRIOR_SHIELD
    public const int ArmosWarriorShield = 0x61;
    // constants/common/enemyCollisionModes.s: ENEMYCOLLISION_ARMOS_WARRIOR_SWORD
    public const int ArmosWarriorSword = 0x62;
    // constants/common/enemyCollisionModes.s: ENEMYCOLLISION_SMASHER_BALL
    public const int SmasherBall = 0x63;
    // constants/common/enemyCollisionModes.s: ENEMYCOLLISION_EYESOAR
    public const int Eyesoar = 0x6d;
    // constants/common/enemyCollisionModes.s: ENEMYCOLLISION_SPIKED_BALL
    public const int SpikedBall = 0x74;
}
