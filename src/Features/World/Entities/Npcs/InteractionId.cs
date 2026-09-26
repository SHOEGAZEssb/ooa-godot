namespace oracleofages;

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
    // constants/common/interactions.s: INTERAC_ROCKDEBRIS2
    public const int RockDebris2 = 0x0c;
    // constants/common/interactions.s: INTERAC_DUNGEON_STUFF
    public const int DungeonStuff = 0x12;
    // constants/common/interactions.s: INTERAC_PUSHBLOCK_TRIGGER
    public const int PushBlockTrigger = 0x13;
    // constants/ages/interactions.s: INTERAC_TOGGLE_FLOOR
    public const int ToggleFloor = 0x15;
    // constants/common/interactions.s: INTERAC_OVERWORLD_KEY_SPRITE
    public const int OverworldKeySprite = 0x18;
    // constants/ages/interactions.s: INTERAC_MINECART_GATE
    public const int MinecartGate = 0x1b;
    // constants/common/interactions.s: INTERAC_DOOR_CONTROLLER
    public const int DoorController = 0x1e;
    // constants/common/interactions.s: INTERAC_DUNGEON_SCRIPT
    public const int DungeonScript = 0x20;
    // constants/ages/interactions.s: INTERAC_DUNGEON_EVENTS
    public const int DungeonEvents = 0x21;
    // constants/ages/interactions.s: INTERAC_FLOOR_COLOR_CHANGER
    public const int FloorColorChanger = 0x22;
    // constants/ages/interactions.s: INTERAC_EXTENDABLE_BRIDGE
    public const int ExtendableBridge = 0x23;
    // constants/ages/interactions.s: INTERAC_TRIGGER_TRANSLATOR
    public const int TriggerTranslator = 0x24;
    // constants/ages/interactions.s: INTERAC_TILE_FILLER
    public const int TileFiller = 0x25;
    // constants/common/interactions.s: INTERAC_BIPIN
    public const int Bipin = 0x28;
    // constants/common/interactions.s: INTERAC_BLOSSOM
    public const int Blossom = 0x2b;
    // constants/ages/interactions.s: INTERAC_OLD_MAN_WITH_RUPEES
    public const int OldManWithRupees = 0x2e;
    // constants/ages/interactions.s: INTERAC_SHOOTING_GALLERY
    public const int ShootingGallery = 0x30;
    // constants/ages/interactions.s: INTERAC_IMPA_IN_CUTSCENE
    public const int ImpaInCutscene = 0x31;
    // constants/ages/interactions.s: INTERAC_SMOG_BOSS
    public const int SmogBoss = 0x33;
    // constants/common/interactions.s: INTERAC_CHILD
    public const int Child = 0x35;
    // constants/ages/interactions.s: INTERAC_NAYRU
    public const int Nayru = 0x36;
    // constants/ages/interactions.s: INTERAC_RALPH
    public const int Ralph = 0x37;
    // constants/ages/interactions.s: INTERAC_PAST_GIRL
    public const int PastGirl = 0x38;
    // constants/ages/interactions.s: INTERAC_MALE_VILLAGER
    public const int MaleVillager = 0x3a;
    // constants/ages/interactions.s: INTERAC_FEMALE_VILLAGER
    public const int FemaleVillager = 0x3b;
    // constants/ages/interactions.s: INTERAC_BOY
    public const int Boy = 0x3c;
    // constants/ages/interactions.s: INTERAC_OLD_LADY
    public const int OldLady = 0x3d;
    // constants/ages/interactions.s: INTERAC_BOY_2
    public const int Boy2 = 0x3f;
    // constants/ages/interactions.s: INTERAC_SOLDIER
    public const int Soldier = 0x40;
    // constants/ages/interactions.s: INTERAC_MISC_MAN
    public const int MiscMan = 0x41;
    // constants/ages/interactions.s: INTERAC_MUSTACHE_MAN
    public const int MustacheMan = 0x42;
    // constants/ages/interactions.s: INTERAC_PAST_GUY
    public const int PastGuy = 0x43;
    // constants/ages/interactions.s: INTERAC_MISC_MAN_2
    public const int MiscMan2 = 0x44;
    // constants/ages/interactions.s: INTERAC_PAST_OLD_LADY
    public const int PastOldLady = 0x45;
    // constants/common/interactions.s: INTERAC_SHOPKEEPER
    public const int Shopkeeper = 0x46;
    // constants/ages/interactions.s: INTERAC_TOKAY
    public const int Tokay = 0x48;
    // constants/ages/interactions.s: INTERAC_FOREST_FAIRY
    public const int ForestFairy = 0x49;
    // constants/ages/interactions.s: INTERAC_RABBIT
    public const int Rabbit = 0x4b;
    // constants/ages/interactions.s: INTERAC_SUBROSIAN
    public const int Subrosian = 0x4e;
    // constants/ages/interactions.s: INTERAC_IMPA_NPC
    public const int ImpaNpc = 0x4f;
    // constants/ages/interactions.s: INTERAC_DUMBBELL_MAN
    public const int DumbbellMan = 0x51;
    // constants/ages/interactions.s: INTERAC_POSTMAN
    public const int Postman = 0x55;
    // constants/ages/interactions.s: INTERAC_PICKAXE_WORKER
    public const int PickaxeWorker = 0x57;
    // constants/ages/interactions.s: INTERAC_HARDHAT_WORKER
    public const int HardhatWorker = 0x58;
    // constants/ages/interactions.s: INTERAC_POE
    public const int Poe = 0x59;
    // constants/ages/interactions.s: INTERAC_OLD_ZORA
    public const int OldZora = 0x5a;
    // constants/ages/interactions.s: INTERAC_TOILET_HAND
    public const int ToiletHand = 0x5b;
    // constants/ages/interactions.s: INTERAC_MASK_SALESMAN
    public const int MaskSalesman = 0x5c;
    // constants/ages/interactions.s: INTERAC_BEAR
    public const int Bear = 0x5d;
    // constants/common/interactions.s: INTERAC_TREASURE
    public const int Treasure = 0x60;
    // constants/ages/interactions.s: INTERAC_LEVER
    public const int Lever = 0x61;
    // constants/ages/interactions.s: INTERAC_ACCESSORY
    public const int Accessory = 0x63;
    // constants/ages/interactions.s: INTERAC_COMEDIAN
    public const int Comedian = 0x65;
    // constants/ages/interactions.s: INTERAC_GORON
    public const int Goron = 0x66;
    // constants/ages/interactions.s: INTERAC_ROSA
    public const int Rosa = 0x68;
    // constants/ages/interactions.s: INTERAC_RAFTON
    public const int Rafton = 0x69;
    // constants/ages/interactions.s: INTERAC_CHEVAL
    public const int Cheval = 0x6a;
    // constants/ages/interactions.s: INTERAC_MISCELLANEOUS_1
    public const int Miscellaneous1 = 0x6b;
    // constants/ages/interactions.s: INTERAC_COMPANION_SCRIPTS
    public const int CompanionScripts = 0x71;
    // constants/ages/interactions.s: INTERAC_KING_MOBLIN_DEFEATED
    public const int KingMoblinDefeated = 0x72;
    // constants/ages/interactions.s: INTERAC_GHINI_HARASSING_MOOSH
    public const int GhiniHarassingMoosh = 0x73;
    // constants/common/interactions.s: INTERAC_SWITCH_TILE_TOGGLER
    public const int SwitchTileToggler = 0x78;
    // constants/common/interactions.s: INTERAC_ROLLER
    public const int Roller = 0x7a;
    // constants/common/interactions.s: INTERAC_ESSENCE
    public const int Essence = 0x7f;
    // constants/ages/interactions.s: INTERAC_BOMB_UPGRADE_FAIRY
    public const int BombUpgradeFairy = 0x83;
    // constants/common/interactions.s: INTERAC_SPARKLE
    public const int Sparkle = 0x84;
    // constants/ages/interactions.s: INTERAC_MAKU_FLOWER
    public const int MakuFlower = 0x86;
    // constants/ages/interactions.s: INTERAC_MAKU_TREE
    public const int MakuTree = 0x87;
    // constants/common/interactions.s: INTERAC_VASU
    public const int Vasu = 0x89;
    // constants/ages/interactions.s: INTERAC_REMOTE_MAKU_CUTSCENE
    public const int RemoteMakuCutscene = 0x8a;
    // constants/ages/interactions.s: INTERAC_GORON_ELDER
    public const int GoronElder = 0x8b;
    // constants/ages/interactions.s: INTERAC_MISC_PUZZLES
    public const int MiscPuzzles = 0x90;
    // constants/ages/interactions.s: INTERAC_PATCH
    public const int Patch = 0x94;
    // constants/ages/interactions.s: INTERAC_CARPENTER
    public const int Carpenter = 0x9a;
    // constants/ages/interactions.s: INTERAC_RAFTWRECK_CUTSCENE
    public const int RaftwreckCutscene = 0x9b;
    // constants/ages/interactions.s: INTERAC_TOKKEY
    public const int Tokkey = 0x9d;
    // constants/ages/interactions.s: INTERAC_WATER_PUSHBLOCK
    public const int WaterPushblock = 0x9e;
    // constants/common/interactions.s: INTERAC_EXCLAMATION_MARK
    public const int ExclamationMark = 0x9f;
    // constants/ages/interactions.s: INTERAC_ZELDA
    public const int Zelda = 0xad;
    // constants/ages/interactions.s: INTERAC_PUSHBLOCK_SYNCHRONIZER
    public const int PushBlockSynchronizer = 0xbd;
    // constants/ages/interactions.s: INTERAC_SYMMETRY_NPC
    public const int SymmetryNpc = 0xbf;
    // constants/ages/interactions.s: INTERAC_PIRATE
    public const int Pirate = 0xc4;
    // constants/common/interactions.s: INTERAC_CREATE_OBJECT_AT_EACH_TILEINDEX
    public const int CreateObjectAtEachTileIndex = 0xc7;
    // constants/ages/interactions.s: INTERAC_TINGLE
    public const int Tingle = 0xc8;
    // constants/ages/interactions.s: INTERAC_TROY
    public const int Troy = 0xca;
    // constants/ages/interactions.s: INTERAC_LINKED_GAME_GHINI
    public const int LinkedGameGhini = 0xcb;
    // constants/ages/interactions.s: INTERAC_PLEN
    public const int Plen = 0xcc;
    // constants/common/interactions.s: INTERAC_BUSINESS_SCRUB
    public const int BusinessScrub = 0xce;
    // constants/ages/interactions.s: INTERAC_GREAT_FAIRY
    public const int GreatFairy = 0xd5;
    // constants/ages/interactions.s: INTERAC_MISCELLANEOUS_2
    public const int Miscellaneous2 = 0xdc;
    // constants/ages/interactions.s: INTERAC_KNOW_IT_ALL_BIRD
    public const int KnowItAllBird = 0xe3;
    // constants/common/interactions.s: INTERAC_RING_HELP_BOOK
    public const int RingHelpBook = 0xe5;
    // constants/ages/interactions.s: INTERAC_RAFT
    public const int Raft = 0xe6;
}
