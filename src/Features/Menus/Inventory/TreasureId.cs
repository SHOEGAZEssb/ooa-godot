namespace oracleofages;

// Source symbols retain their clean-US/Ages values. Keep IDs distinct from table indices,
// masks and counters; byte-oriented asset/state APIs intentionally continue to use integers.
public static class TreasureId
{
    // constants/common/treasure.s: TREASURE_NONE
    public const int None = 0x00;
    // constants/common/treasure.s: TREASURE_SHIELD
    public const int Shield = 0x01;
    // constants/common/treasure.s: TREASURE_PUNCH
    public const int Punch = 0x02;
    // constants/common/treasure.s: TREASURE_BOMBS
    public const int Bombs = 0x03;
    // constants/common/treasure.s: TREASURE_CANE_OF_SOMARIA
    public const int CaneOfSomaria = 0x04;
    // constants/common/treasure.s: TREASURE_SWORD
    public const int Sword = 0x05;
    // constants/common/treasure.s: TREASURE_BOOMERANG
    public const int Boomerang = 0x06;
    // constants/common/treasure.s: TREASURE_ROD_OF_SEASONS
    public const int RodOfSeasons = 0x07;
    // constants/common/treasure.s: TREASURE_MAGNET_GLOVES
    public const int MagnetGloves = 0x08;
    // constants/common/treasure.s: TREASURE_SWITCH_HOOK
    public const int SwitchHook = 0x0a;
    // constants/common/treasure.s: TREASURE_SWITCH_HOOK_CHAIN
    public const int SwitchHookChain = 0x0b;
    // constants/common/treasure.s: TREASURE_BIGGORON_SWORD
    public const int BiggoronSword = 0x0c;
    // constants/common/treasure.s: TREASURE_BOMBCHUS
    public const int Bombchus = 0x0d;
    // constants/common/treasure.s: TREASURE_FLUTE
    public const int Flute = 0x0e;
    // constants/common/treasure.s: TREASURE_SHOOTER
    public const int Shooter = 0x0f;
    // constants/common/treasure.s: TREASURE_HARP
    public const int Harp = 0x11;
    // constants/common/treasure.s: TREASURE_SLINGSHOT
    public const int Slingshot = 0x13;
    // constants/common/treasure.s: TREASURE_SHOVEL
    public const int Shovel = 0x15;
    // constants/common/treasure.s: TREASURE_BRACELET
    public const int Bracelet = 0x16;
    // constants/common/treasure.s: TREASURE_FEATHER
    public const int Feather = 0x17;
    // constants/common/treasure.s: TREASURE_SEED_SATCHEL
    public const int SeedSatchel = 0x19;
    // constants/common/treasure.s: TREASURE_1a
    public const int Id1a = 0x1a;
    // constants/common/treasure.s: TREASURE_EMBER_SEEDS
    public const int EmberSeeds = 0x20;
    // constants/common/treasure.s: TREASURE_SCENT_SEEDS
    public const int ScentSeeds = 0x21;
    // constants/common/treasure.s: TREASURE_PEGASUS_SEEDS
    public const int PegasusSeeds = 0x22;
    // constants/common/treasure.s: TREASURE_GALE_SEEDS
    public const int GaleSeeds = 0x23;
    // constants/common/treasure.s: TREASURE_MYSTERY_SEEDS
    public const int MysterySeeds = 0x24;
    // constants/common/treasure.s: TREASURE_TUNE_OF_ECHOES
    public const int TuneOfEchoes = 0x25;
    // constants/common/treasure.s: TREASURE_TUNE_OF_CURRENTS
    public const int TuneOfCurrents = 0x26;
    // constants/common/treasure.s: TREASURE_TUNE_OF_AGES
    public const int TuneOfAges = 0x27;
    // constants/common/treasure.s: TREASURE_RUPEES
    public const int Rupees = 0x28;
    // constants/common/treasure.s: TREASURE_HEART_REFILL
    public const int HeartRefill = 0x29;
    // constants/common/treasure.s: TREASURE_HEART_CONTAINER
    public const int HeartContainer = 0x2a;
    // constants/common/treasure.s: TREASURE_HEART_PIECE
    public const int HeartPiece = 0x2b;
    // constants/common/treasure.s: TREASURE_RING_BOX
    public const int RingBox = 0x2c;
    // constants/common/treasure.s: TREASURE_RING
    public const int Ring = 0x2d;
    // constants/common/treasure.s: TREASURE_FLIPPERS
    public const int Flippers = 0x2e;
    // constants/common/treasure.s: TREASURE_POTION
    public const int Potion = 0x2f;
    // constants/common/treasure.s: TREASURE_GASHA_SEED
    public const int GashaSeed = 0x34;
    // constants/common/treasure.s: TREASURE_MAKU_SEED
    public const int MakuSeed = 0x36;
    // constants/common/treasure.s: TREASURE_ESSENCE
    public const int Essence = 0x40;
    // constants/common/treasure.s: TREASURE_TRADEITEM
    public const int TradeItem = 0x41;
    // constants/common/treasure.s: TREASURE_GRAVEYARD_KEY
    public const int GraveyardKey = 0x42;
    // constants/common/treasure.s: TREASURE_CROWN_KEY
    public const int CrownKey = 0x43;
    // constants/common/treasure.s: TREASURE_OLD_MERMAID_KEY
    public const int OldMermaidKey = 0x45;
    // constants/common/treasure.s: TREASURE_BOMB_FLOWER
    public const int BombFlower = 0x49;
    // constants/common/treasure.s: TREASURE_MERMAID_SUIT
    public const int MermaidSuit = 0x4a;
    // constants/common/treasure.s: TREASURE_SLATE
    public const int Slate = 0x4b;
    // constants/common/treasure.s: TREASURE_TUNI_NUT
    public const int TuniNut = 0x4c;
    // constants/common/treasure.s: TREASURE_TOKAY_EYEBALL
    public const int TokayEyeball = 0x4f;
    // constants/common/treasure.s: TREASURE_FAIRY_POWDER
    public const int FairyPowder = 0x51;
    // constants/common/treasure.s: TREASURE_CHEVAL_ROPE
    public const int ChevalRope = 0x52;
    // constants/common/treasure.s: TREASURE_BOMB_FLOWER_LOWER_HALF
    public const int BombFlowerLowerHalf = 0x58;
    // constants/common/treasure.s: TREASURE_GORON_LETTER
    public const int GoronLetter = 0x59;
    // constants/common/treasure.s: TREASURE_LAVA_JUICE
    public const int LavaJuice = 0x5a;
    // constants/common/treasure.s: TREASURE_BROTHER_EMBLEM
    public const int BrotherEmblem = 0x5b;
    // constants/common/treasure.s: TREASURE_GORON_VASE
    public const int GoronVase = 0x5c;
    // constants/common/treasure.s: TREASURE_GORONADE
    public const int Goronade = 0x5d;
    // constants/common/treasure.s: TREASURE_ROCK_BRISKET
    public const int RockBrisket = 0x5e;
    // constants/common/treasure.s: TREASURE_60
    public const int Id60 = 0x60;
    // constants/common/treasure.s: TREASURE_SATCHEL_UPGRADE
    public const int SatchelUpgrade = 0x62;
    // constants/common/treasure.s: TREASURE_67
    public const int Id67 = 0x67;
}
