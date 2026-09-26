namespace oracleofages;

// Source symbols retain their clean-US/Ages values. Keep IDs distinct from table indices,
// masks and counters; byte-oriented asset/state APIs intentionally continue to use integers.
public static class SoundId
{
    // constants/common/music.s: SND_FAIRY_HEAL
    public const int SndFairyHeal = 0x8c;
    // constants/common/music.s: SND_DIMITRI
    public const int SndDimitri = 0xc4;
    // constants/common/music.s: MUS_NONE
    public const int MusNone = 0x00;
    // constants/common/music.s: MUS_TITLESCREEN
    public const int MusTitlescreen = 0x01;
    // constants/common/music.s: MUS_MINIGAME
    public const int MusMinigame = 0x02;
    // constants/common/music.s: MUS_OVERWORLD
    public const int MusOverworld = 0x03;
    // constants/common/music.s: MUS_ESSENCE
    public const int MusEssence = 0x06;
    // constants/common/music.s: MUS_NAYRU
    public const int MusNayru = 0x08;
    // constants/common/music.s: MUS_GAMEOVER
    public const int MusGameOver = 0x09;
    // constants/common/music.s: MUS_ESSENCE_ROOM
    public const int MusEssenceRoom = 0x0d;
    // constants/common/music.s: MUS_FAIRY_FOUNTAIN
    public const int MusFairyFountain = 0x0f;
    // constants/common/music.s: MUS_GET_ESSENCE
    public const int MusGetEssence = 0x10;
    // constants/common/music.s: MUS_FILE_SELECT
    public const int MusFileSelect = 0x11;
    // constants/common/music.s: MUS_SPIRITS_GRAVE
    public const int MusSpiritsGrave = 0x13;
    // constants/common/music.s: MUS_ROOM_OF_RITES
    public const int MusRoomOfRites = 0x1d;
    // constants/common/music.s: MUS_MAKU_TREE
    public const int MusMakuTree = 0x1e;
    // constants/common/music.s: MUS_SADNESS
    public const int MusSadness = 0x1f;
    // constants/common/music.s: MUS_DISASTER
    public const int MusDisaster = 0x21;
    // constants/common/music.s: MUS_MAPLE_THEME
    public const int MusMapleTheme = 0x2b;
    // constants/common/music.s: MUS_MAPLE_GAME
    public const int MusMapleGame = 0x2c;
    // constants/common/music.s: MUS_MINIBOSS
    public const int MusMiniboss = 0x2d;
    // constants/common/music.s: MUS_BOSS
    public const int MusBoss = 0x2e;
    // constants/common/music.s: MUS_LADX_SIDEVIEW
    public const int MusLadxSideview = 0x2f;
    // constants/common/music.s: MUS_CRAZY_DANCE
    public const int MusCrazyDance = 0x31;
    // constants/common/music.s: MUS_RALPH
    public const int MusRalph = 0x35;
    // constants/common/music.s: MUS_INTRO_1
    public const int MusIntro1 = 0x3f;
    // constants/common/music.s: MUS_INTRO_2
    public const int MusIntro2 = 0x40;
    // constants/common/music.s: MUS_BLACK_TOWER_ENTRANCE
    public const int MusBlackTowerEntrance = 0x46;
    // constants/common/music.s: SND_GETITEM
    public const int SndGetItem = 0x4c;
    // constants/common/music.s: SND_SOLVEPUZZLE
    public const int SndSolvePuzzle = 0x4d;
    // constants/common/music.s: SND_DAMAGE_ENEMY
    public const int SndDamageEnemy = 0x4e;
    // constants/common/music.s: SND_CHARGE_SWORD
    public const int SndChargeSword = 0x4f;
    // constants/common/music.s: SND_CLINK
    public const int SndClink = 0x50;
    // constants/common/music.s: SND_THROW
    public const int SndThrow = 0x51;
    // constants/common/music.s: SND_BOMB_LAND
    public const int SndBombLand = 0x52;
    // constants/common/music.s: SND_JUMP
    public const int SndJump = 0x53;
    // constants/common/music.s: SND_OPENMENU
    public const int SndOpenMenu = 0x54;
    // constants/common/music.s: SND_CLOSEMENU
    public const int SndCloseMenu = 0x55;
    // constants/common/music.s: SND_SELECTITEM
    public const int SndSelectItem = 0x56;
    // constants/common/music.s: SND_GAINHEART
    public const int SndGainHeart = 0x57;
    // constants/common/music.s: SND_CLINK2
    public const int SndClink2 = 0x58;
    // constants/common/music.s: SND_FALLINHOLE
    public const int SndFallInHole = 0x59;
    // constants/common/music.s: SND_ERROR
    public const int SndError = 0x5a;
    // constants/common/music.s: SND_ENERGYTHING
    public const int SndEnergyThing = 0x5c;
    // constants/common/music.s: SND_SWORDBEAM
    public const int SndSwordBeam = 0x5d;
    // constants/common/music.s: SND_GETSEED
    public const int SndGetSeed = 0x5e;
    // constants/common/music.s: SND_DAMAGE_LINK
    public const int SndDamageLink = 0x5f;
    // constants/common/music.s: SND_RUPEE
    public const int SndRupee = 0x61;
    // constants/common/music.s: SND_BOSS_DAMAGE
    public const int SndBossDamage = 0x63;
    // constants/common/music.s: SND_LINK_DEAD
    public const int SndLinkDead = 0x64;
    // constants/common/music.s: SND_LINK_FALL
    public const int SndLinkFall = 0x65;
    // constants/common/music.s: SND_TEXT
    public const int SndText = 0x66;
    // constants/common/music.s: SND_BOSS_DEAD
    public const int SndBossDead = 0x67;
    // constants/common/music.s: SND_SLASH
    public const int SndSlash = 0x6a;
    // constants/common/music.s: SND_SWORDSPIN
    public const int SndSwordSpin = 0x6b;
    // constants/common/music.s: SND_OPENCHEST
    public const int SndOpenChest = 0x6c;
    // constants/common/music.s: SND_CUTGRASS
    public const int SndCutGrass = 0x6d;
    // constants/common/music.s: SND_ENTERCAVE
    public const int SndEnterCave = 0x6e;
    // constants/common/music.s: SND_EXPLOSION
    public const int SndExplosion = 0x6f;
    // constants/common/music.s: SND_DOORCLOSE
    public const int SndDoorClose = 0x70;
    // constants/common/music.s: SND_MOVEBLOCK
    public const int SndMoveBlock = 0x71;
    // constants/common/music.s: SND_LIGHTTORCH
    public const int SndLightTorch = 0x72;
    // constants/common/music.s: SND_KILLENEMY
    public const int SndKillEnemy = 0x73;
    // constants/common/music.s: SND_SWORDSLASH
    public const int SndSwordSlash = 0x74;
    // constants/common/music.s: SND_UNKNOWN5
    public const int SndUnknown5 = 0x75;
    // constants/common/music.s: SND_SHIELD
    public const int SndShield = 0x76;
    // constants/common/music.s: SND_DROPESSENCE
    public const int SndDropEssence = 0x77;
    // constants/common/music.s: SND_BOOMERANG
    public const int SndBoomerang = 0x78;
    // constants/common/music.s: SND_BIG_EXPLOSION
    public const int SndBigExplosion = 0x79;
    // constants/common/music.s: SND_MYSTERY_SEED
    public const int SndMysterySeed = 0x7b;
    // constants/common/music.s: SND_AQUAMENTUS_HOVER
    public const int SndAquamentusHover = 0x7c;
    // constants/common/music.s: SND_OPEN_GATE
    public const int SndOpenGate = 0x7d;
    // constants/common/music.s: SND_SWITCH
    public const int SndSwitch = 0x7e;
    // constants/common/music.s: SND_MOVE_BLOCK_2
    public const int SndMoveBlock2 = 0x7f;
    // constants/common/music.s: SND_STRONG_POUND
    public const int SndStrongPound = 0x81;
    // constants/common/music.s: SND_MAGIC_POWDER
    public const int SndMagicPowder = 0x83;
    // constants/common/music.s: SND_MENU_MOVE
    public const int SndMenuMove = 0x84;
    // constants/common/music.s: SND_SCENT_SEED
    public const int SndScentSeed = 0x85;
    // constants/common/music.s: SND_SPLASH
    public const int SndSplash = 0x87;
    // constants/common/music.s: SND_LINK_SWIM
    public const int SndLinkSwim = 0x88;
    // constants/common/music.s: SND_TEXT_2
    public const int SndText2 = 0x89;
    // constants/common/music.s: SND_FILLED_HEART_CONTAINER
    public const int SndFilledHeartContainer = 0x8b;
    // constants/common/music.s: SND_TELEPORT
    public const int SndTeleport = 0x8d;
    // constants/common/music.s: SND_ENEMY_JUMP
    public const int SndEnemyJump = 0x8f;
    // constants/common/music.s: SND_GALE_SEED
    public const int SndGaleSeed = 0x90;
    // constants/common/music.s: SND_FAIRYCUTSCENE
    public const int SndFairyCutscene = 0x91;
    // constants/common/music.s: SND_WARP_START
    public const int SndWarpStart = 0x95;
    // constants/common/music.s: SND_POOF
    public const int SndPoof = 0x98;
    // constants/common/music.s: SND_BASEBALL
    public const int SndBaseball = 0x99;
    // constants/common/music.s: SND_PICKUP
    public const int SndPickup = 0x9c;
    // constants/common/music.s: SND_CHICKEN
    public const int SndChicken = 0xa0;
    // constants/common/music.s: SND_COMPASS
    public const int SndCompass = 0xa2;
    // constants/common/music.s: SND_LAND
    public const int SndLand = 0xa3;
    // constants/common/music.s: SND_BEAM
    public const int SndBeam = 0xa4;
    // constants/common/music.s: SND_BREAK_ROCK
    public const int SndBreakRock = 0xa5;
    // constants/common/music.s: SND_STRIKE
    public const int SndStrike = 0xa6;
    // constants/common/music.s: SND_VERAN_FAIRY_ATTACK
    public const int SndVeranFairyAttack = 0xa8;
    // constants/common/music.s: SND_DIG
    public const int SndDig = 0xa9;
    // constants/common/music.s: SND_WAVE
    public const int SndWave = 0xaa;
    // constants/common/music.s: SND_SWORD_OBTAINED
    public const int SndSwordObtained = 0xab;
    // constants/common/music.s: SND_SHOCK
    public const int SndShock = 0xac;
    // constants/common/music.s: SND_TUNE_OF_ECHOES
    public const int SndTuneOfEchoes = 0xad;
    // constants/common/music.s: SND_TUNE_OF_CURRENTS
    public const int SndTuneOfCurrents = 0xae;
    // constants/common/music.s: SND_TUNE_OF_AGES
    public const int SndTuneOfAges = 0xaf;
    // constants/common/music.s: SND_OPENING
    public const int SndOpening = 0xb0;
    // constants/common/music.s: SND_MAKUDISAPPEAR
    public const int SndMakuDisappear = 0xb2;
    // constants/common/music.s: SND_RUMBLE
    public const int SndRumble = 0xb3;
    // constants/common/music.s: SND_FADEOUT
    public const int SndFadeOut = 0xb4;
    // constants/common/music.s: SND_RUMBLE2
    public const int SndRumble2 = 0xb8;
    // constants/common/music.s: SND_FLOODGATES
    public const int SndFloodgates = 0xc2;
    // constants/common/music.s: SND_MOOSH
    public const int SndMoosh = 0xc5;
    // constants/common/music.s: SND_DING
    public const int SndDing = 0xc8;
    // constants/common/music.s: SND_SEEDSHOOTER
    public const int SndSeedShooter = 0xcb;
    // constants/common/music.s: SND_WHISTLE
    public const int SndWhistle = 0xcc;
    // constants/common/music.s: SND_GORON_DANCE_B
    public const int SndGoronDanceB = 0xcd;
    // constants/common/music.s: SND_MAKU_TREE_PAST
    public const int SndMakuTreePast = 0xce;
    // constants/common/music.s: SND_PIRATE_BELL
    public const int SndPirateBell = 0xd0;
    // constants/common/music.s: SND_TIMEWARP_INITIATED
    public const int SndTimewarpInitiated = 0xd1;
    // constants/common/music.s: SND_LIGHTNING
    public const int SndLightning = 0xd2;
    // constants/common/music.s: SND_TIMEWARP_COMPLETED
    public const int SndTimewarpCompleted = 0xd4;
    // constants/common/music.s: SNDCTRL_STOPMUSIC
    public const int SndCtrlStopMusic = 0xf0;
    // constants/common/music.s: SNDCTRL_STOPSFX
    public const int SndCtrlStopSfx = 0xf1;
    // constants/common/music.s: SNDCTRL_DISABLE
    public const int SndCtrlDisable = 0xf5;
    // constants/common/music.s: SNDCTRL_ENABLE
    public const int SndCtrlEnable = 0xf6;
    // constants/common/music.s: SNDCTRL_FAST_FADEIN
    public const int SndctrlFastFadein = 0xf7;
    // constants/common/music.s: SNDCTRL_MEDIUM_FADEIN
    public const int SndctrlMediumFadein = 0xf8;
    // constants/common/music.s: SNDCTRL_SLOW_FADEIN
    public const int SndctrlSlowFadein = 0xf9;
    // constants/common/music.s: SNDCTRL_FAST_FADEOUT
    public const int SndCtrlFastFadeOut = 0xfa;
    // constants/common/music.s: SNDCTRL_MEDIUM_FADEOUT
    public const int SndCtrlMediumFadeOut = 0xfb;
    // constants/common/music.s: SNDCTRL_SLOW_FADEOUT
    public const int SndCtrlSlowFadeOut = 0xfc;
}
