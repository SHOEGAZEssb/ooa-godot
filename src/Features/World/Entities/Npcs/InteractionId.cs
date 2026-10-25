namespace Oracles;

// Source symbols retain their clean-US/Ages values. Keep IDs distinct from table indices,
// masks and counters; byte-oriented asset/state APIs intentionally continue to use integers.
public static class InteractionId
{
    // constants/common/interactions.s: INTERAC_GRASSDEBRIS
    public const int GrassDebris = 0x00;
    // constants/common/interactions.s: INTERAC_REDGRASSDEBRIS
    public const int RedGrassDebris = 0x01;
    // constants/common/interactions.s: INTERAC_SPLASH
    public const int Splash = 0x03;
    // constants/common/interactions.s: INTERAC_PUFF
    public const int Puff = 0x05;
    // constants/common/interactions.s: INTERAC_ROCKDEBRIS
    public const int RockDebris = 0x06;
    // constants/common/interactions.s: INTERAC_SNOWDEBRIS
    public const int SnowDebris = 0x09;
    // constants/common/interactions.s: INTERAC_0b
    public const int Id0b = 0x0b;
    // constants/common/interactions.s: INTERAC_FARORE
    public const int Farore = 0x10;
    // constants/common/interactions.s: INTERAC_FARORE_MAKECHEST
    public const int FaroreMakeChest = 0x11;
    // constants/common/interactions.s: INTERAC_ROCKDEBRIS2
    public const int RockDebris2 = 0x0c;
    // constants/common/interactions.s: INTERAC_DUNGEON_STUFF
    public const int DungeonStuff = 0x12;
    // constants/common/interactions.s: INTERAC_PUSHBLOCK_TRIGGER
    public const int PushBlockTrigger = 0x13;
    // constants/common/interactions.s: INTERAC_OVERWORLD_KEY_SPRITE
    public const int OverworldKeySprite = 0x18;
    // constants/common/interactions.s: INTERAC_DOOR_CONTROLLER
    public const int DoorController = 0x1e;
    // constants/common/interactions.s: INTERAC_DUNGEON_SCRIPT
    public const int DungeonScript = 0x20;
    // constants/common/interactions.s: INTERAC_BIPIN
    public const int Bipin = 0x28;
    // constants/common/interactions.s: INTERAC_BLOSSOM
    public const int Blossom = 0x2b;
    // constants/common/interactions.s: INTERAC_CHILD
    public const int Child = 0x35;
    // constants/common/interactions.s: INTERAC_SHOPKEEPER
    public const int Shopkeeper = 0x46;
    // constants/common/interactions.s: INTERAC_TREASURE
    public const int Treasure = 0x60;
    // constants/common/interactions.s: INTERAC_SWITCH_TILE_TOGGLER
    public const int SwitchTileToggler = 0x78;
    // constants/common/interactions.s: INTERAC_ROLLER
    public const int Roller = 0x7a;
    // constants/common/interactions.s: INTERAC_ESSENCE
    public const int Essence = 0x7f;
    // constants/common/interactions.s: INTERAC_SPARKLE
    public const int Sparkle = 0x84;
    // constants/common/interactions.s: INTERAC_VASU
    public const int Vasu = 0x89;
    // constants/common/interactions.s: INTERAC_EXCLAMATION_MARK
    public const int ExclamationMark = 0x9f;
    // constants/common/interactions.s: INTERAC_CREATE_OBJECT_AT_EACH_TILEINDEX
    public const int CreateObjectAtEachTileIndex = 0xc7;
    // constants/common/interactions.s: INTERAC_BUSINESS_SCRUB
    public const int BusinessScrub = 0xce;
    // constants/common/interactions.s: INTERAC_RING_HELP_BOOK
    public const int RingHelpBook = 0xe5;
}
