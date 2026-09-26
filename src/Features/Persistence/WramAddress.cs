namespace oracleofages;

// Source symbols retain their clean-US/Ages values. Keep IDs distinct from table indices,
// masks and counters; byte-oriented asset/state APIs intentionally continue to use integers.
public static class WramAddress
{
    // include/wram.s: wSoundFadeCounter
    public const int wSoundFadeCounter = 0xc014;
    // include/wram.s: wSoundFadeDirection
    public const int wSoundFadeDirection = 0xc015;
    // include/wram.s: wSoundDisabled
    public const int wSoundDisabled = 0xc01b;
    // include/wram.s: wChannel7TriggerOnNextSound
    public const int wChannel7TriggerOnNextSound = 0xc01c;
    // include/wram.s: wMusicMuted
    public const int wMusicMuted = 0xc023;
    // include/wram.s: wSoundVolume
    public const int wSoundVolume = 0xc024;
    // include/wram.s: wBigBuffer
    public const int wBigBuffer = 0xc300;
    // include/wram.s: wFileStart
    public const int wFileStart = 0xc5b0;
    // include/wram.s: wUnappraisedRings
    public const int wUnappraisedRings = 0xc5c0;
    // include/wram.s: wUnappraisedRingsEnd
    public const int wUnappraisedRingsEnd = 0xc600;
    // include/wram.s: wLinkName
    public const int wLinkName = 0xc602;
    // include/wram.s: wc608
    public const int wc608 = 0xc608;
    // include/wram.s: wKidName
    public const int wKidName = 0xc609;
    // include/wram.s: wChildStatus
    public const int wChildStatus = 0xc60f;
    // include/wram.s: wAnimalCompanion
    public const int wAnimalCompanion = 0xc610;
    // include/wram.s: wFileIsLinkedGame
    public const int wFileIsLinkedGame = 0xc612;
    // include/wram.s: wFileIsHeroGame
    public const int wFileIsHeroGame = 0xc613;
    // include/wram.s: wFileIsCompleted
    public const int wFileIsCompleted = 0xc614;
    // include/wram.s: wRingsObtained
    public const int wRingsObtained = 0xc616;
    // include/wram.s: wDeathCounter
    public const int wDeathCounter = 0xc61e;
    // include/wram.s: wTotalEnemiesKilled
    public const int wTotalEnemiesKilled = 0xc620;
    // include/wram.s: wPlaytimeCounter
    public const int wPlaytimeCounter = 0xc622;
    // include/wram.s: wTotalRupeesCollected
    public const int wTotalRupeesCollected = 0xc627;
    // include/wram.s: wTextSpeed
    public const int wTextSpeed = 0xc629;
    // include/wram.s: wDeathRespawnBuffer
    public const int wDeathRespawnBuffer = 0xc62b;
    // include/wram.s: wMinimapGroup
    public const int wMinimapGroup = 0xc63a;
    // include/wram.s: wMinimapRoom
    public const int wMinimapRoom = 0xc63b;
    // include/wram.s: wMinimapDungeonMapPosition
    public const int wMinimapDungeonMapPosition = 0xc63c;
    // include/wram.s: wMinimapDungeonFloor
    public const int wMinimapDungeonFloor = 0xc63d;
    // include/wram.s: wPortalGroup
    public const int wPortalGroup = 0xc63e;
    // include/wram.s: wPortalRoom
    public const int wPortalRoom = 0xc63f;
    // include/wram.s: wPortalPos
    public const int wPortalPos = 0xc640;
    // include/wram.s: wMapleKillCounter
    public const int wMapleKillCounter = 0xc641;
    // include/wram.s: wBoughtShopItems1
    public const int wBoughtShopItems1 = 0xc642;
    // include/wram.s: wBoughtShopItems2
    public const int wBoughtShopItems2 = 0xc643;
    // include/wram.s: wMapleState
    public const int wMapleState = 0xc644;
    // include/wram.s: wCompanionStates
    public const int wCompanionStates = 0xc646;
    // include/wram.s: wDimitriState
    public const int wDimitriState = 0xc647;
    // include/wram.s: wMooshState
    public const int wMooshState = 0xc648;
    // include/wram.s: wCompanionTutorialTextShown
    public const int wCompanionTutorialTextShown = 0xc649;
    // include/wram.s: wGashaSpotFlags
    public const int wGashaSpotFlags = 0xc64c;
    // include/wram.s: wGashaSpotKillCounters
    public const int wGashaSpotKillCounters = 0xc64f;
    // include/wram.s: wGashaMaturity
    public const int wGashaMaturity = 0xc65f;
    // include/wram.s: wDungeonSmallKeys
    public const int wDungeonSmallKeys = 0xc672;
    // include/wram.s: wDungeonBossKeys
    public const int wDungeonBossKeys = 0xc682;
    // include/wram.s: wDungeonCompasses
    public const int wDungeonCompasses = 0xc684;
    // include/wram.s: wDungeonMaps
    public const int wDungeonMaps = 0xc686;
    // include/wram.s: wInventoryB
    public const int wInventoryB = 0xc688;
    // include/wram.s: wInventoryA
    public const int wInventoryA = 0xc689;
    // include/wram.s: wInventoryStorage
    public const int wInventoryStorage = 0xc68a;
    // include/wram.s: wObtainedTreasureFlags
    public const int wObtainedTreasureFlags = 0xc69a;
    // include/wram.s: wLinkHealth
    public const int wLinkHealth = 0xc6aa;
    // include/wram.s: wLinkMaxHealth
    public const int wLinkMaxHealth = 0xc6ab;
    // include/wram.s: wNumHeartPieces
    public const int wNumHeartPieces = 0xc6ac;
    // include/wram.s: wNumRupees
    public const int wNumRupees = 0xc6ad;
    // include/wram.s: wShieldLevel
    public const int wShieldLevel = 0xc6af;
    // include/wram.s: wNumBombs
    public const int wNumBombs = 0xc6b0;
    // include/wram.s: wMaxBombs
    public const int wMaxBombs = 0xc6b1;
    // include/wram.s: wSwordLevel
    public const int wSwordLevel = 0xc6b2;
    // include/wram.s: wNumBombchus
    public const int wNumBombchus = 0xc6b3;
    // include/wram.s: wSeedSatchelLevel
    public const int wSeedSatchelLevel = 0xc6b4;
    // include/wram.s: wFluteIcon
    public const int wFluteIcon = 0xc6b5;
    // include/wram.s: wSwitchHookLevel
    public const int wSwitchHookLevel = 0xc6b6;
    // include/wram.s: wSelectedHarpSong
    public const int wSelectedHarpSong = 0xc6b7;
    // include/wram.s: wBraceletLevel
    public const int wBraceletLevel = 0xc6b8;
    // include/wram.s: wNumEmberSeeds
    public const int wNumEmberSeeds = 0xc6b9;
    // include/wram.s: wNumGashaSeeds
    public const int wNumGashaSeeds = 0xc6be;
    // include/wram.s: wEssencesObtained
    public const int wEssencesObtained = 0xc6bf;
    // include/wram.s: wTradeItem
    public const int wTradeItem = 0xc6c0;
    // include/wram.s: wTuniNutState
    public const int wTuniNutState = 0xc6c2;
    // include/wram.s: wNumSlates
    public const int wNumSlates = 0xc6c3;
    // Clean US treasureDisplayData1, not hack-base's relocated RAM symbols.
    // $c700 onward belongs exclusively to the present room-flag table.
    // include/wram.s: wSatchelSelectedSeeds
    public const int wSatchelSelectedSeeds = 0xc6c4;
    // include/wram.s: wShooterSelectedSeeds
    public const int wShooterSelectedSeeds = 0xc6c5;
    // include/wram.s: wRingBoxContents
    public const int wRingBoxContents = 0xc6c6;
    // include/wram.s: wActiveRing
    public const int wActiveRing = 0xc6cb;
    // include/wram.s: wRingBoxLevel
    public const int wRingBoxLevel = 0xc6cc;
    // include/wram.s: wNumUnappraisedRingsBcd
    public const int wNumUnappraisedRingsBcd = 0xc6cd;
    // include/wram.s: wNumRingsAppraised
    public const int wNumRingsAppraised = 0xc6ce;
    // include/wram.s: wGlobalFlags
    public const int wGlobalFlags = 0xc6d0;
    // include/wram.s: wChildStage
    public const int wChildStage = 0xc6e0;
    // include/wram.s: wNextChildStage
    public const int wNextChildStage = 0xc6e1;
    // include/wram.s: wc6e2
    public const int wc6e2 = 0xc6e2;
    // include/wram.s: wChildPersonality
    public const int wChildPersonality = 0xc6e4;
    // include/wram.s: wMakuMapTextPresent
    public const int wMakuMapTextPresent = 0xc6e6;
    // include/wram.s: wMakuMapTextPast
    public const int wMakuMapTextPast = 0xc6e7;
    // include/wram.s: wMakuTreeState
    public const int wMakuTreeState = 0xc6e8;
    // include/wram.s: wJabuWaterLevel
    public const int wJabuWaterLevel = 0xc6e9;
    // include/wram.s: wMakuTreeSeedSatchelXPosition
    public const int wMakuTreeSeedSatchelXPosition = 0xc6eb;
    // include/wram.s: wPirateShipRoom
    public const int wPirateShipRoom = 0xc6ec;
    // include/wram.s: wPirateShipY
    public const int wPirateShipY = 0xc6ed;
    // include/wram.s: wPirateShipX
    public const int wPirateShipX = 0xc6ee;
    // include/wram.s: wPirateShipAngle
    public const int wPirateShipAngle = 0xc6ef;
    // include/wram.s: wShortSecretIndex
    public const int wShortSecretIndex = 0xc6fb;
    // include/wram.s: wGroup0RoomFlags
    public const int wGroup0RoomFlags = 0xc700;
    // include/wram.s: wTextNumberSubstitution
    public const int wTextNumberSubstitution = 0xcba8;
    // include/wram.s: wRoomPack
    public const int wRoomPack = 0xcc31;
    // include/wram.s: wRoomStateModifier
    public const int wRoomStateModifier = 0xcc32;
    // include/wram.s: wActiveCollisions
    public const int wActiveCollisions = 0xcc33;
    // include/wram.s: wSeedTreeRefilledBitset
    public const int wSeedTreeRefilledBitset = 0xcc4d;
    // include/wram.s: wLinkRaisedFloorOffset
    public const int wLinkRaisedFloorOffset = 0xcc69;
    // include/wram.s: wPegasusSeedCounter
    public const int wPegasusSeedCounter = 0xcc6c;
    // include/wram.s: wWarpsDisabled
    public const int wWarpsDisabled = 0xcc6e;
    // include/wram.s: wcca2
    public const int wcca2 = 0xcca2;
    // include/wram.s: wUpgradesObtained
    public const int wUpgradesObtained = 0xcca8;
    // include/wram.s: wLever1PullDistance
    public const int wLever1PullDistance = 0xccab;
    // include/wram.s: wLever2PullDistance
    public const int wLever2PullDistance = 0xccac;
    // include/wram.s: wDisableWarps
    public const int wDisableWarps = 0xccb2;
    // include/wram.s: wDiggingUpEnemiesForbidden
    public const int wDiggingUpEnemiesForbidden = 0xccde;
    // include/wram.s: wLastToggleBlocksState
    public const int wLastToggleBlocksState = 0xcd2c;
    // include/wram.s: wDeleteEnergyBeads
    public const int wDeleteEnergyBeads = 0xcd2d;
    // include/wram.s: wLinkTimeWarpTile
    public const int wLinkTimeWarpTile = 0xcddc;
    // include/wram.s: wSentBackByStrangeForce
    public const int wSentBackByStrangeForce = 0xcdde;
    // include/wram.s: wcddf
    public const int wcddf = 0xcddf;
    // include/wram.s: wcde0
    public const int wcde0 = 0xcde0;
    // include/wram.s: wTmpcec0; shared movement, torch and placement scratch.
    public const int wTmpcec0 = 0xcec0;
    // include/wram.s: wTmpcfc0; this union's fields have subsystem-specific meanings.
    public const int wTmpcfc0 = 0xcfc0;
}
