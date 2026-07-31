using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot : GameRoot
{
    private int _neutralInputFrames;
    private ValidationCutsceneTrace? _enterPastCommandTrace;
    private ValidationCombatEffectAudit _combatEffectAudit = null!;

    public override void _Ready()
    {
        base._Ready();
        _sound.AttachPlayRequestAudit();
        _combatEffectAudit = new ValidationCombatEffectAudit();
        _combat.SetEffectObserver(_combatEffectAudit);
        // Validation advances component entry points synchronously rather than
        // through GameRoot's live application scheduler.
        _player.ApplicationUpdateOwned = false;
        _dialogue.ApplicationUpdateOwned = false;
        ResetValidationInput();
        _scene.ProcessMode = ProcessModeEnum.Disabled;
    }

    public override void _Process(double delta)
    {
        // Scene entry can retain a just-pressed input edge for the remainder
        // of that real frame. Let it expire without advancing gameplay, since
        // the suite performs many original-engine updates synchronously.
        if (AnyValidationInputJustPressed())
        {
            _neutralInputFrames = 0;
            return;
        }
        if (++_neutralInputFrames < 2)
            return;

        SetProcess(false);
        _scene.ProcessMode = ProcessModeEnum.Inherit;
        _entities.GameButtonJustPressedSource = static () => false;
        RunValidation();
    }

    private void RunValidation()
    {
        try
        {
            ValidateAll();
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"Validation failed.\n{exception}");
            GetTree().Quit(1);
        }
    }

    private static void FailIf(
        [DoesNotReturnIf(true)] bool condition,
        string message)
    {
        if (condition)
            throw new InvalidOperationException(message);
    }

    private static void ResetValidationInput()
    {
        // The runner can be entered through a scene change while an editor or
        // joypad event is still marked just-pressed for the current frame.
        // Explicit frame simulations must start from neutral WRAM-style input.
        foreach (string action in new[]
        {
            "attack", "item", "move_up", "move_right", "move_down", "move_left",
            "map", "inventory"
        })
        {
            Input.ActionRelease(action);
        }
    }

    private static bool AnyValidationInputJustPressed() =>
        Input.IsActionJustPressed("attack") || Input.IsActionJustPressed("item") ||
        Input.IsActionJustPressed("move_up") || Input.IsActionJustPressed("move_right") ||
        Input.IsActionJustPressed("move_down") || Input.IsActionJustPressed("move_left") ||
        Input.IsActionJustPressed("map") || Input.IsActionJustPressed("inventory");

    private void ValidateAll()
    {
        ValidateGameplaySceneGraph();
        ValidateApplicationFixedUpdateScheduler();
        ValidateGeneratedTableReader();
        ValidateMenuLifecycleFoundation();
        _world.ValidateRepresentativeRooms();
        ValidateOracleObjectMath();
        ValidateOracleRandom();
        ValidateRoomEventTimeline();
        ValidateCutsceneCommandSchema();
        ValidateCutsceneDefaultDeny();
        ValidateSaveDataFoundation();
        ValidateSaveStore();
        ValidateTreasureInterpreter();
        ValidateDungeonCollectibles();
        ValidateRoomTileChanges();
        ValidateExplicitSavePersistence();
        ValidateMenuPresentationData();
        ValidateMainMenu();
        ValidateNewGameIntro();
        ValidateSoundEngine();
        ValidateGraphicsCache();
        ValidateBackgroundPaletteState();
        ValidateDebugFlagMenu();
        ValidateDebugCollision();
        ValidateDebugRoomWarp();
        ValidateDebugMapleShortcut();
        ValidateDeathRespawnCheckpoints();

        LoadValidationRoom(0, 0x11);
        ValidateStartupTransition();
        LoadValidationRoom(0, 0x22);
        ValidateSymmetryTransition();

        ValidateSigns();
        ValidateNpcImplementationManifest();
        ValidateNpcs();
        ValidateRooms171And181();
        ValidateDekuForestSoldierCutscene();
        ValidateDekuForestPalaceCutscene();
        ValidateRoom173SoldierPair();
        ValidateRoom174PastOldLady();
        ValidateRooms182And192NpcInteractions();
        ValidateRoom183MiscManAndDrops();
        ValidateRoom184StoneRabbitsAndSoldier();
        ValidateRooms193And194NpcInteractions();
        ValidateRoom22fPostman();
        ValidateRoom23eToiletHand();
        ValidateRoom2e9ShootingGallery();
        ValidateRoom39eInteractions();
        ValidateRoom3aeInteractions();
        ValidateRoom20eNpcInteractions();
        ValidateTroyHouseRooms();
        ValidateRooms145And3fcNpcInteractions();
        ValidateRoom148NpcInteractions();
        ValidateRoom149FamilyInteractions();
        ValidateRoom157NpcInteractions();
        ValidateRoom158NpcInteractions();
        ValidateRoom175NpcInteractions();
        ValidateRoom176NpcInteractions();
        ValidateRoom186NpcInteractions();
        ValidateLowerBlackTowerInteractions();
        ValidateNpcFlagVisibility();
        ValidateGraveyardGhostKidsCutscene();
        ValidateBipinBlossomNaming();
        ValidateImpaIntroEncounter();
        ValidateMakuTreeDisappearanceCutscene();
        ValidateMakuSproutRescueCutscene();
        ValidateMakuTreeSavedCutscene();
        ValidateRoom056Comedian();
        ValidateRoom07cPoe();
        ValidateRoom22ePoe();
        ValidateRoom2e6MaskSalesman();
        ValidateNayruIntroCutscene();
        ValidateRalphPortalDepartureEvent();
        ValidateAnimations();
        ValidateLinkItemGeneratedData();
        ValidateSwordBush();
        ValidateAirborneSwordRendering();
        ValidateShield();
        ValidateShovel();
        ValidateBombs();
        ValidateSeedSatchel();
        ValidateHarp();
        ValidateSeedTrees();
        ValidateRoom180OwlStatue();
        ValidateGashaSpots();
        ValidateMapleEvents();
        ValidateObjectSpeedTable();
        ValidateEnemyBehaviorTables();
        ValidateEnemyPlacementRules();
        ValidateEnemyObjectPlacementOrder();
        ValidateHardhatAndSpinyBeetles();
        ValidateSpikedBeetles();
        ValidateKeese();
        ValidatePeahat();
        ValidateGraveyardCrowsAndDropProducers();
        ValidateOctoroks();
        ValidateArrowMoblins();
        ValidateHostileProjectileLifecycle();
        ValidateEnemySwordKnockback();
        ValidateEnemyDamageBlink();
        ValidateEnemyHazards();
        ValidateStalfos();
        ValidateZolsAndGels();
        ValidateItemDrops();
        ValidateTimePortals();
        ValidateEnterPastEvent();
        ValidateHouseWarp();
        ValidateCaveWarps();
        ValidateMakuTreeSouthExitReveal();
        ValidateTerrain();
        ValidateLinkTerrainEffects();
        ValidateHealth();
        ValidatePlayerDamageAndDeath();
        ValidateChests();
        ValidateInventoryFoundation();
        ValidateInventoryMenu();
        ValidateRingFunctionality();
        ValidateBraceletChestAndPushGate();
        ValidatePushBlocks();
        ValidateDungeonMechanics();
        ValidateSpiritsGraveEntranceInteractions();
        ValidateOverworldKeyholeAndGraveyardGate();
        ValidateDarkRoomInteractions();
        ValidateDungeonKeyDoors();
        ValidateSpiritsGrave();
        ValidateMapScreen();
        ValidateLynnaShopInteractions();
        ValidateVasuShopInteractions();
        ValidateRemoteMakuFirstEssenceCutscene();
        ValidateRemoteMakuSecondEssenceCutscene();
        ValidateRemoteMakuHarpCutscene();
        ValidateFairiesWoodsSequence();
        ValidateGameOverRestart();
        ValidateSaveAndQuitToTitle();
        ValidateRoom083Interactions();
        ValidateDebugSavestates();
        ValidateWingDungeon();

        GD.Print("Validated all gameplay and world-data scenarios.");
    }
}
