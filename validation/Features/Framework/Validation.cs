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
    private int _executedValidationCount;
    private string? _validationFilter;
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
        foreach (string argument in OS.GetCmdlineUserArgs())
        {
            const string prefix = "--validate-only=";
            if (argument.StartsWith(prefix, StringComparison.Ordinal))
                _validationFilter = argument[prefix.Length..];
        }
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

    private void RunIsolatedValidation(Action validation)
    {
        if (_validationFilter is not null &&
            !string.Equals(
                validation.Method.Name,
                _validationFilter,
                StringComparison.Ordinal))
        {
            return;
        }

        _executedValidationCount++;
        ReinitializeGameplayForValidation();
        _sound.AttachPlayRequestAudit();
        _combatEffectAudit.Clear();
        _combat.SetEffectObserver(_combatEffectAudit);
        OracleGraphicsCache.SetObserver(null);
        _enterPastCommandTrace = null;
        _player.ApplicationUpdateOwned = false;
        _dialogue.ApplicationUpdateOwned = false;
        _entities.GameButtonJustPressedSource = static () => false;
        ResetValidationInput();

        try
        {
            validation();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Isolated validation {validation.Method.Name} failed.",
                exception);
        }
    }

    private void ValidateRepresentativeRooms() =>
        _world.ValidateRepresentativeRooms();

    private void ValidateStartupTransitionFromRoom011()
    {
        LoadValidationRoom(0, 0x11);
        ValidateStartupTransition();
    }

    private void ValidateSymmetryTransitionFromRoom022()
    {
        LoadValidationRoom(0, 0x22);
        ValidateSymmetryTransition();
    }

    private void ValidateAll()
    {
        RunIsolatedValidation(ValidateGameplaySceneGraph);
        RunIsolatedValidation(ValidateApplicationFixedUpdateScheduler);
        RunIsolatedValidation(ValidateGeneratedTableReader);
        RunIsolatedValidation(ValidateMenuLifecycleFoundation);
        RunIsolatedValidation(ValidateRepresentativeRooms);
        RunIsolatedValidation(ValidateOracleObjectMath);
        RunIsolatedValidation(ValidateOracleRandom);
        RunIsolatedValidation(ValidateRoomEventTimeline);
        RunIsolatedValidation(ValidateCutsceneCommandSchema);
        RunIsolatedValidation(ValidateCutsceneDefaultDeny);
        RunIsolatedValidation(ValidateSaveDataFoundation);
        RunIsolatedValidation(ValidateSaveStore);
        RunIsolatedValidation(ValidateTreasureInterpreter);
        RunIsolatedValidation(ValidateDungeonCollectibles);
        RunIsolatedValidation(ValidateRoomTileChanges);
        RunIsolatedValidation(ValidateExplicitSavePersistence);
        RunIsolatedValidation(ValidateMenuPresentationData);
        RunIsolatedValidation(ValidateFrontendIntro);
        RunIsolatedValidation(ValidateMainMenu);
        RunIsolatedValidation(ValidateNewGameIntro);
        RunIsolatedValidation(ValidateSoundEngine);
        RunIsolatedValidation(ValidateGraphicsCache);
        RunIsolatedValidation(ValidateBackgroundPaletteState);
        RunIsolatedValidation(ValidateDebugFlagMenu);
        RunIsolatedValidation(ValidateDebugCollision);
        RunIsolatedValidation(ValidateDebugRoomWarp);
        RunIsolatedValidation(ValidateDebugMapleShortcut);
        RunIsolatedValidation(ValidateDeathRespawnCheckpoints);
        RunIsolatedValidation(ValidateStartupTransitionFromRoom011);
        RunIsolatedValidation(ValidateSymmetryTransitionFromRoom022);
        RunIsolatedValidation(ValidateSigns);
        RunIsolatedValidation(ValidateNpcImplementationManifest);
        RunIsolatedValidation(ValidateNpcs);
        RunIsolatedValidation(ValidateRooms171And181);
        RunIsolatedValidation(ValidateDekuForestSoldierCutscene);
        RunIsolatedValidation(ValidateDekuForestPalaceCutscene);
        RunIsolatedValidation(ValidateRoom173SoldierPair);
        RunIsolatedValidation(ValidateRoom174PastOldLady);
        RunIsolatedValidation(ValidateRooms182And192NpcInteractions);
        RunIsolatedValidation(ValidateRoom183MiscManAndDrops);
        RunIsolatedValidation(ValidateRoom184StoneRabbitsAndSoldier);
        RunIsolatedValidation(ValidateRooms193And194NpcInteractions);
        RunIsolatedValidation(ValidateRoom22fPostman);
        RunIsolatedValidation(ValidateRoom23eToiletHand);
        RunIsolatedValidation(ValidateRoom2e9ShootingGallery);
        RunIsolatedValidation(ValidateRoom39eInteractions);
        RunIsolatedValidation(ValidateRoom3aeInteractions);
        RunIsolatedValidation(ValidateRoom20eNpcInteractions);
        RunIsolatedValidation(ValidateTroyHouseRooms);
        RunIsolatedValidation(ValidateRooms145And3fcNpcInteractions);
        RunIsolatedValidation(ValidateRoom148NpcInteractions);
        RunIsolatedValidation(ValidateRoom149FamilyInteractions);
        RunIsolatedValidation(ValidateRoom157NpcInteractions);
        RunIsolatedValidation(ValidateRoom158NpcInteractions);
        RunIsolatedValidation(ValidateRoom175NpcInteractions);
        RunIsolatedValidation(ValidateRoom176NpcInteractions);
        RunIsolatedValidation(ValidateRoom186NpcInteractions);
        RunIsolatedValidation(ValidateLowerBlackTowerInteractions);
        RunIsolatedValidation(ValidateNpcFlagVisibility);
        RunIsolatedValidation(ValidateGraveyardGhostKidsCutscene);
        RunIsolatedValidation(ValidateBipinBlossomNaming);
        RunIsolatedValidation(ValidateImpaIntroEncounter);
        RunIsolatedValidation(ValidateMakuTreeDisappearanceCutscene);
        RunIsolatedValidation(ValidateMakuSproutRescueCutscene);
        RunIsolatedValidation(ValidateRoom05bCompanionTutorial);
        RunIsolatedValidation(ValidateRooms079And089Interactions);
        RunIsolatedValidation(ValidateRoom06aRickyGloves);
        RunIsolatedValidation(ValidateRickyRiding);
        RunIsolatedValidation(ValidateRoom098RickyGlovesPickup);
        RunIsolatedValidation(ValidateRoom06bMooshGoodbye);
        RunIsolatedValidation(ValidateRoom06cMooshRescue);
        RunIsolatedValidation(ValidateMakuTreeSavedCutscene);
        RunIsolatedValidation(ValidateRoom056Comedian);
        RunIsolatedValidation(ValidateRoom07cPoe);
        RunIsolatedValidation(ValidateRoom22ePoe);
        RunIsolatedValidation(ValidateRoom20fCheval);
        RunIsolatedValidation(ValidateRoom179RalphAfterCheval);
        RunIsolatedValidation(ValidateRoom197RalphAfterRafton);
        RunIsolatedValidation(ValidateRooms21eAnd21fRafton);
        RunIsolatedValidation(ValidateRaft);
        RunIsolatedValidation(ValidateRaftwreckCutscene);
        RunIsolatedValidation(ValidateTokayTheftCutscene);
        RunIsolatedValidation(ValidateTokayIslandInteractions);
        RunIsolatedValidation(ValidateTokayIslandWorldObjects);
        RunIsolatedValidation(ValidateRoom2e6MaskSalesman);
        RunIsolatedValidation(ValidateRoom2f3DepressedBoy);
        RunIsolatedValidation(ValidateNayruIntroCutscene);
        RunIsolatedValidation(ValidateRalphPortalDepartureEvent);
        RunIsolatedValidation(ValidateAnimations);
        RunIsolatedValidation(ValidateLinkItemGeneratedData);
        RunIsolatedValidation(ValidateSwordBush);
        RunIsolatedValidation(ValidateAirborneSwordRendering);
        RunIsolatedValidation(ValidateShield);
        RunIsolatedValidation(ValidateShovel);
        RunIsolatedValidation(ValidateBombs);
        RunIsolatedValidation(ValidateSeedSatchel);
        RunIsolatedValidation(ValidateHarp);
        RunIsolatedValidation(ValidateSeedTrees);
        RunIsolatedValidation(ValidateRoom180OwlStatue);
        RunIsolatedValidation(ValidateGashaSpots);
        RunIsolatedValidation(ValidateMapleEvents);
        RunIsolatedValidation(ValidateObjectSpeedTable);
        RunIsolatedValidation(ValidateEnemyBehaviorTables);
        RunIsolatedValidation(ValidateEnemyPlacementRules);
        RunIsolatedValidation(ValidateEnemyObjectPlacementOrder);
        RunIsolatedValidation(ValidateHardhatAndSpinyBeetles);
        RunIsolatedValidation(ValidateSpikedBeetles);
        RunIsolatedValidation(ValidateKeese);
        RunIsolatedValidation(ValidatePeahat);
        RunIsolatedValidation(ValidateGraveyardCrowsAndDropProducers);
        RunIsolatedValidation(ValidateOctoroks);
        RunIsolatedValidation(ValidateArrowMoblins);
        RunIsolatedValidation(ValidateHostileProjectileLifecycle);
        RunIsolatedValidation(ValidateEnemySwordKnockback);
        RunIsolatedValidation(ValidateEnemyDamageBlink);
        RunIsolatedValidation(ValidateEnemyHazards);
        RunIsolatedValidation(ValidateStalfos);
        RunIsolatedValidation(ValidateZolsAndGels);
        RunIsolatedValidation(ValidateItemDrops);
        RunIsolatedValidation(ValidateTimePortals);
        RunIsolatedValidation(ValidateEnterPastEvent);
        RunIsolatedValidation(ValidateCrescentIslandPastStairs);
        RunIsolatedValidation(ValidateHouseWarp);
        RunIsolatedValidation(ValidateCaveWarps);
        RunIsolatedValidation(ValidateMakuTreeSouthExitReveal);
        RunIsolatedValidation(ValidateTerrain);
        RunIsolatedValidation(ValidateLinkTopDownMovement);
        RunIsolatedValidation(ValidateLinkTopDownSwimming);
        RunIsolatedValidation(ValidateLinkSideScrollSwimming);
        RunIsolatedValidation(ValidateLinkTerrainEffects);
        RunIsolatedValidation(ValidateHealth);
        RunIsolatedValidation(ValidatePlayerDamageAndDeath);
        RunIsolatedValidation(ValidateChests);
        RunIsolatedValidation(ValidateInventoryFoundation);
        RunIsolatedValidation(ValidateInventoryMenu);
        RunIsolatedValidation(ValidateRingFunctionality);
        RunIsolatedValidation(ValidateBraceletChestAndPushGate);
        RunIsolatedValidation(ValidatePushBlocks);
        RunIsolatedValidation(ValidateDungeonMechanics);
        RunIsolatedValidation(ValidateRoom2e3Interactions);
        RunIsolatedValidation(ValidateRoom5b6Interactions);
        RunIsolatedValidation(ValidateRoom5bfInteractions);
        RunIsolatedValidation(ValidateSpiritsGraveEntranceInteractions);
        RunIsolatedValidation(ValidateOverworldKeyholeAndGraveyardGate);
        RunIsolatedValidation(ValidateDarkRoomInteractions);
        RunIsolatedValidation(ValidateDungeonKeyDoors);
        RunIsolatedValidation(ValidateSpiritsGrave);
        RunIsolatedValidation(ValidateMapScreen);
        RunIsolatedValidation(ValidateLynnaShopInteractions);
        RunIsolatedValidation(ValidateVasuShopInteractions);
        RunIsolatedValidation(ValidateRemoteMakuFirstEssenceCutscene);
        RunIsolatedValidation(ValidateRemoteMakuSecondEssenceCutscene);
        RunIsolatedValidation(ValidateRemoteMakuHarpCutscene);
        RunIsolatedValidation(ValidateFairiesWoodsSequence);
        RunIsolatedValidation(ValidateGameOverRestart);
        RunIsolatedValidation(ValidateSaveAndQuitToTitle);
        RunIsolatedValidation(ValidateRoom083Interactions);
        RunIsolatedValidation(ValidateDebugSavestates);
        RunIsolatedValidation(ValidateWingDungeon);
        RunIsolatedValidation(ValidateHeadThwompFidelity);

        if (_validationFilter is not null && _executedValidationCount == 0)
        {
            throw new InvalidOperationException(
                $"No validation method named '{_validationFilter}' was registered.");
        }
        GD.Print(_validationFilter is null
            ? "Validated all gameplay and world-data scenarios."
            : $"Validated isolated scenario {_validationFilter}.");
    }
}
