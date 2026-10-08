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
    private int _skippedValidationCount;
    private int _failedValidationCount;
    private bool _continueOnFailure;
    private bool _skipRomValidation;
    private string? _validationFilter;
    private int _validationOrdinal;
    private int _shardIndex;
    private int _shardCount = 1;
    private readonly List<(Action Run, bool RequiresRom)> _registeredValidations = [];
    private ValidationCutsceneTrace? _enterPastCommandTrace;
    private ValidationCombatEffectAudit _combatEffectAudit = null!;

    public override void _Ready()
    {
        if (OS.GetCmdlineUserArgs().Contains("--profile-startup"))
        {
            BeginStartupProfile();
            return;
        }
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
        if (_startupProfile is not null)
        {
            AdvanceStartupProfile(delta);
            return;
        }
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

    private async void RunValidation()
    {
        try
        {
            _skipRomValidation = OS.GetCmdlineUserArgs().Contains("--skip-rom-validation");
            _continueOnFailure = OS.GetCmdlineUserArgs().Contains("--validate-continue-on-failure");
            foreach (string argument in OS.GetCmdlineUserArgs())
            {
                const string prefix = "--validate-shard=";
                if (!argument.StartsWith(prefix, StringComparison.Ordinal))
                    continue;
                string[] parts = argument[prefix.Length..].Split('/');
                if (parts.Length != 2 ||
                    !int.TryParse(parts[0], out int index) ||
                    !int.TryParse(parts[1], out int count) ||
                    count < 1 || index < 1 || index > count)
                {
                    throw new InvalidOperationException(
                        "Expected --validate-shard=INDEX/COUNT with 1 <= INDEX <= COUNT.");
                }
                _shardIndex = index - 1;
                _shardCount = count;
                FailIf(_validationFilter is not null,
                    "--validate-shard cannot be combined with --validate-only.");
            }
            ValidateAll();
            // AudioStreamPlayer.Stop queues its native playback for the
            // AudioServer mixer/update handoff. Let those engine phases run
            // before quitting a suite that creates and tears down output.
            _scene.ProcessMode = ProcessModeEnum.Disabled;
            if (_failedValidationCount == 0)
            {
                await CaptureSaveOptionsScreens();
                await CaptureBootLoadingScreen();
            }
            // Finalize unused managed image/texture readers while Godot is
            // alive, then let it drain their deferred native references. Do
            // this once per worker, outside scenario execution and timing.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_failedValidationCount != 0)
                throw new InvalidOperationException($"{_failedValidationCount} validation scenarios failed; all assigned scenarios were attempted.");
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

    private void RunIsolatedValidation(Action validation, bool requiresRom = false)
        => _registeredValidations.Add((validation, requiresRom));

    private void RunRegisteredValidations()
    {
        Dictionary<string, double>? weights = null;
        const string prefix = "--validate-timing-profile=";
        foreach (string argument in OS.GetCmdlineUserArgs())
            if (argument.StartsWith(prefix, StringComparison.Ordinal))
                weights = ValidationShardPlanner.ParseWeights(File.ReadLines(argument[prefix.Length..]), argument[prefix.Length..]);
        int[] assignments = ValidationShardPlanner.Assign(
            _registeredValidations.Select(scenario => scenario.Run.Method.Name).ToArray(), _shardCount, weights);
        GD.Print($"VALIDATION_SCHEDULING mode={(weights is null ? "round-robin" : "timings")} registered={assignments.Length}");
        foreach (var scenario in _registeredValidations)
        {
            int ordinal = _validationOrdinal++;
            if (assignments[ordinal] == _shardIndex) ExecuteIsolatedValidation(scenario.Run, scenario.RequiresRom);
        }
    }

    private void ExecuteIsolatedValidation(Action validation, bool requiresRom)
    {
        if (_validationFilter is not null &&
            !string.Equals(
                validation.Method.Name,
                _validationFilter,
                StringComparison.Ordinal))
        {
            return;
        }

        if (requiresRom && _skipRomValidation)
        {
            _skippedValidationCount++;
            GD.Print($"VALIDATION_SKIPPED name={validation.Method.Name} reason=rom-required");
            return;
        }

        _executedValidationCount++;
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
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

        double setupMs = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        try
        {
            validation();
            GD.Print(FormattableString.Invariant(
                $"VALIDATION_TIMING name={validation.Method.Name} setup_ms={setupMs:F3} total_ms={System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds:F3}"));
        }
        catch (Exception exception)
        {
            if (_continueOnFailure)
            {
                _executedValidationCount--;
                _failedValidationCount++;
                GD.Print(FormattableString.Invariant(
                    $"VALIDATION_TIMING name={validation.Method.Name} setup_ms={setupMs:F3} total_ms={System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds:F3}"));
                GD.PushError($"Isolated validation {validation.Method.Name} failed.\n{exception}");
                return;
            }
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
        RunIsolatedValidation(ValidateApplicationValidationFixture);
        RunIsolatedValidation(ValidateHotPaths);
        RunIsolatedValidation(ValidateControllerMovement);
        RunIsolatedValidation(ValidateGeneratedTableReader);
        RunIsolatedValidation(ValidateMenuLifecycleFoundation);
        RunIsolatedValidation(ValidateRepresentativeRooms);
        RunIsolatedValidation(ValidateNuunCompanionLayouts);
        RunIsolatedValidation(ValidateNuunHighlands);
        RunIsolatedValidation(ValidateNuunEnemyStates);
        RunIsolatedValidation(ValidateNuunWaterfall);
        RunIsolatedValidation(ValidateOracleObjectMath);
        RunIsolatedValidation(ValidateOracleRandom);
        RunIsolatedValidation(ValidateOracleRandomRom, requiresRom: true);
        RunIsolatedValidation(ValidateOracleMovementRom, requiresRom: true);
        RunIsolatedValidation(ValidateOracleAnglesRom, requiresRom: true);
        RunIsolatedValidation(ValidateOracleVerticalMotionRom, requiresRom: true);
        RunIsolatedValidation(ValidatePlacementBufferRom, requiresRom: true);
        RunIsolatedValidation(ValidateRoomPlacementRom, requiresRom: true);
        RunIsolatedValidation(ValidateEnemyAiRom, requiresRom: true);
        RunIsolatedValidation(ValidateEnemyAiGameplayRom, requiresRom: true);
        RunIsolatedValidation(ValidateWaterTektiteRom, requiresRom: true);
        RunIsolatedValidation(ValidateGiantBladeTrapRom, requiresRom: true);
        RunIsolatedValidation(ValidateBubbleRom, requiresRom: true);
        RunIsolatedValidation(ValidateBariRom, requiresRom: true);
        RunIsolatedValidation(ValidateFloormasterRom, requiresRom: true);
        RunIsolatedValidation(ValidateWizzrobeRom, requiresRom: true);
        RunIsolatedValidation(ValidateCandleRom, requiresRom: true);
        RunIsolatedValidation(ValidateVireProjectileRom, requiresRom: true);
        RunIsolatedValidation(ValidateWallArrowShooterRom, requiresRom: true);
        RunIsolatedValidation(ValidateVireRom, requiresRom: true);
        RunIsolatedValidation(ValidateOctogonEncounterRom, requiresRom: true);
        RunIsolatedValidation(ValidateOctogonEntryRom, requiresRom: true);
        RunIsolatedValidation(ValidateOctogonCombatRom, requiresRom: true);
        RunIsolatedValidation(ValidateOctogonBubbleRom, requiresRom: true);
        RunIsolatedValidation(ValidateOctogonDeathRom, requiresRom: true);
        RunIsolatedValidation(ValidateOctogonDiveRom, requiresRom: true);
        RunIsolatedValidation(ValidateMermaidPatternsRom, requiresRom: true);
        RunIsolatedValidation(ValidateMermaidCubeRom, requiresRom: true);
        RunIsolatedValidation(ValidateMermaidChangingFloorRom, requiresRom: true);
        RunIsolatedValidation(ValidateMermaidBossKeyRom, requiresRom: true);
        RunIsolatedValidation(ValidateMermaidEraWallsRom, requiresRom: true);
        RunIsolatedValidation(ValidateMermaidTorchOrderRom, requiresRom: true);
        RunIsolatedValidation(ValidateMermaidSpinnerRom, requiresRom: true);
        RunIsolatedValidation(ValidateMermaidChestRom, requiresRom: true);
        RunIsolatedValidation(ValidateMermaidFloorsRom, requiresRom: true);
        RunIsolatedValidation(ValidateMermaidDungeonAdmission);
        RunIsolatedValidation(ValidateMermaidBridgeRom, requiresRom: true);
        RunIsolatedValidation(ValidateMermaidConjunctionRom, requiresRom: true);
        RunIsolatedValidation(ValidateMermaidEssenceRom, requiresRom: true);
        RunIsolatedValidation(ValidateEnemyHitRecoveryRom, requiresRom: true);
        RunIsolatedValidation(ValidateEnemyKnockbackRom, requiresRom: true);
        RunIsolatedValidation(ValidateEnemyDeathHandoffRom, requiresRom: true);
        RunIsolatedValidation(ValidateLinkCollisionRom, requiresRom: true);
        RunIsolatedValidation(ValidateLinkCollisionGameplayRom, requiresRom: true);
        RunIsolatedValidation(ValidateLinkTerrainBoundaryRom, requiresRom: true);
        RunIsolatedValidation(ValidateLinkScreenBoundaryRom, requiresRom: true);
        RunIsolatedValidation(ValidateLinkSwimmingRom, requiresRom: true);
        RunIsolatedValidation(ValidateSideViewLaddersRom, requiresRom: true);
        RunIsolatedValidation(ValidateSideViewWaterRom, requiresRom: true);
        RunIsolatedValidation(ValidateSideViewPitsRom, requiresRom: true);
        RunIsolatedValidation(ValidateSideViewPlatformsRom, requiresRom: true);
        RunIsolatedValidation(ValidateSideViewPlatformScriptsRom, requiresRom: true);
        RunIsolatedValidation(ValidateLinkHazardRecoveryRom, requiresRom: true);
        RunIsolatedValidation(ValidateLinkDamageRom, requiresRom: true);
        RunIsolatedValidation(ValidateLinkInvincibilityRom, requiresRom: true);
        RunIsolatedValidation(ValidateLinkDamageGameplayRom, requiresRom: true);
        RunIsolatedValidation(ValidateObjectCollisionGeometryRom, requiresRom: true);
        RunIsolatedValidation(ValidateCollisionMasksRom, requiresRom: true);
        RunIsolatedValidation(ValidateLinkContactGeometryRom, requiresRom: true);
        RunIsolatedValidation(ValidateCollisionOrderRom, requiresRom: true);
        RunIsolatedValidation(ValidateObjectAnimationRom, requiresRom: true);
        RunIsolatedValidation(ValidateAnimationGameplayRom, requiresRom: true);
        RunIsolatedValidation(ValidateTreasureArithmeticRom, requiresRom: true);
        RunIsolatedValidation(ValidateTreasureGrantsRom, requiresRom: true);
        RunIsolatedValidation(ValidateRingGrantCapacityRom, requiresRom: true);
        RunIsolatedValidation(ValidateTreasureGameplayRom, requiresRom: true);
        RunIsolatedValidation(ValidateItemSlotAllocationRom, requiresRom: true);
        RunIsolatedValidation(ValidateItemButtonDispatchRom, requiresRom: true);
        RunIsolatedValidation(ValidateItemUseGameplayRom, requiresRom: true);
        RunIsolatedValidation(ValidateSwordTimingRom, requiresRom: true);
        RunIsolatedValidation(ValidateSwordPokeRom, requiresRom: true);
        RunIsolatedValidation(ValidateSwordCollisionRngRom, requiresRom: true);
        RunIsolatedValidation(ValidateSwordGameplayRom, requiresRom: true);
        RunIsolatedValidation(ValidateSwordAirRom, requiresRom: true);
        RunIsolatedValidation(ValidateSwordContactHandoffRom, requiresRom: true);
        RunIsolatedValidation(ValidateBombFuseExplosionRom, requiresRom: true);
        RunIsolatedValidation(ValidateBombPickupThrowRom, requiresRom: true);
        RunIsolatedValidation(ValidateBombGameplayRom, requiresRom: true);
        RunIsolatedValidation(ValidateBraceletGrabLiftRom, requiresRom: true);
        RunIsolatedValidation(ValidateBraceletCarryThrowRom, requiresRom: true);
        RunIsolatedValidation(ValidateBraceletGameplayRom, requiresRom: true);
        RunIsolatedValidation(ValidateFeatherJumpPhysicsRom, requiresRom: true);
        RunIsolatedValidation(ValidateFeatherAirMovementRom, requiresRom: true);
        RunIsolatedValidation(ValidateFeatherGameplayRom, requiresRom: true);
        RunIsolatedValidation(ValidateFeatherSideViewRom, requiresRom: true);
        RunIsolatedValidation(ValidateFeatherIceMomentumRom, requiresRom: true);
        RunIsolatedValidation(ValidateScrollTimingRom, requiresRom: true);
        RunIsolatedValidation(ValidateScrollHandoffRom, requiresRom: true);
        RunIsolatedValidation(ValidateBreakableSourceMasksRom, requiresRom: true);
        RunIsolatedValidation(ValidateDropSelectionRom, requiresRom: true);
        RunIsolatedValidation(ValidateTileMutationRom, requiresRom: true);
        RunIsolatedValidation(ValidateTileBreakGameplayRom, requiresRom: true);
        RunIsolatedValidation(ValidateChangedTileQueueRom, requiresRom: true);
        RunIsolatedValidation(ValidateChangedTileGraphicsRom, requiresRom: true);
        RunIsolatedValidation(ValidateChangedTileScrollRom, requiresRom: true);
        RunIsolatedValidation(ValidateCompanionDirectionsRom, requiresRom: true);
        RunIsolatedValidation(ValidateCompanionTerrainRom, requiresRom: true);
        RunIsolatedValidation(ValidateCompanionMovementGameplayRom, requiresRom: true);
        RunIsolatedValidation(ValidateCompanionMountRom, requiresRom: true);
        RunIsolatedValidation(ValidateCompanionEquippedItemsRom, requiresRom: true);
        RunIsolatedValidation(ValidateRickyAbilitiesRom, requiresRom: true);
        RunIsolatedValidation(ValidateMooshAbilitiesRom, requiresRom: true);
        RunIsolatedValidation(ValidateDimitriAbilitiesRom, requiresRom: true);
        RunIsolatedValidation(ValidateCompanionTraversalRom, requiresRom: true);
        RunIsolatedValidation(ValidateCompanionHazardsRom, requiresRom: true);
        RunIsolatedValidation(ValidateRickyLongJumpsRom, requiresRom: true);
        RunIsolatedValidation(ValidateMooshFlutterAndWaterRom, requiresRom: true);
        RunIsolatedValidation(ValidateCompanionFluteArrivalRom, requiresRom: true);
        RunIsolatedValidation(ValidateCompanionAnimationRom, requiresRom: true);
        RunIsolatedValidation(ValidateDimitriCarryGatesRom, requiresRom: true);
        RunIsolatedValidation(ValidateDimitriCarryOffsetsRom, requiresRom: true);
        RunIsolatedValidation(ValidateDimitriThrowRom, requiresRom: true);
        RunIsolatedValidation(ValidateDimitriMouthMasksRom, requiresRom: true);
        RunIsolatedValidation(ValidateCompanionAttackTilesRom, requiresRom: true);
        RunIsolatedValidation(ValidateDimitriSwallowRom, requiresRom: true);
        RunIsolatedValidation(ValidateCompanionScrollRom, requiresRom: true);
        RunIsolatedValidation(ValidateRickyTornadoAllocationRom, requiresRom: true);
        RunIsolatedValidation(ValidateCompanionUpdateGatesRom, requiresRom: true);
        RunIsolatedValidation(ValidateCompanionDeparturesRom, requiresRom: true);
        RunIsolatedValidation(ValidateCompanionFluteSpawnRom, requiresRom: true);
        RunIsolatedValidation(ValidateCompanionFluteGatesRom, requiresRom: true);
        RunIsolatedValidation(ValidateCompanionRememberedSpawnRom, requiresRom: true);
        RunIsolatedValidation(ValidateCompanionPresetsRom, requiresRom: true);
        RunIsolatedValidation(ValidateCompanionPresetPrecedenceRom, requiresRom: true);
        RunIsolatedValidation(ValidateCompanionPersistenceRom, requiresRom: true);
        RunIsolatedValidation(ValidateCompanionRememberedEligibilityRom, requiresRom: true);
        RunIsolatedValidation(ValidateCompanionSpawnInitializationRom, requiresRom: true);
        RunIsolatedValidation(ValidateCompanionSpawnScrollRom, requiresRom: true);
        RunIsolatedValidation(ValidateDimitriWaterDismountRom, requiresRom: true);
        RunIsolatedValidation(ValidateTreasureRupeeValues);
        RunIsolatedValidation(ValidateCpuDecimalArithmetic);
        RunIsolatedValidation(ValidateAnimationCounterBoundaries);
        RunIsolatedValidation(ValidateRoomEventTimeline);
        RunIsolatedValidation(ValidateRoomEventScheduling);
        RunIsolatedValidation(ValidateSharedRoomEventHosts);
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
        RunIsolatedValidation(ValidateFrontendIntroRom, requiresRom: true);
        RunIsolatedValidation(ValidateFrontendFixedUpdates);
        RunIsolatedValidation(ValidateMainMenu);
        RunIsolatedValidation(ValidateMainMenuRom, requiresRom: true);
        RunIsolatedValidation(ValidateInventoryFramePixelsRom, requiresRom: true);
        RunIsolatedValidation(ValidateInventoryMissingEssenceTextRom, requiresRom: true);
        RunIsolatedValidation(ValidateInventoryEssenceReplayRom, requiresRom: true);
        RunIsolatedValidation(ValidateInventoryItemFramesRom, requiresRom: true);
        RunIsolatedValidation(ValidateInventoryPassiveFramesRom, requiresRom: true);
        RunIsolatedValidation(ValidateInventoryQuestFramesRom, requiresRom: true);
        RunIsolatedValidation(ValidateInventoryRingFramesRom, requiresRom: true);
        RunIsolatedValidation(ValidateInventorySubmenuFramesRom, requiresRom: true);
        RunIsolatedValidation(ValidateInventorySharedSlotFramesRom, requiresRom: true);
        RunIsolatedValidation(ValidateInventoryRetainedPaletteRom, requiresRom: true);
        RunIsolatedValidation(ValidateInventorySubmenuPositionsRom, requiresRom: true);
        RunIsolatedValidation(ValidateInventorySubmenuAvailabilityRom, requiresRom: true);
        RunIsolatedValidation(ValidateInventorySubmenuInitialSelectionRom, requiresRom: true);
        RunIsolatedValidation(ValidateInventorySubmenuHudPixelsRom, requiresRom: true);
        RunIsolatedValidation(ValidateMapFramePixelsRom, requiresRom: true);
        RunIsolatedValidation(ValidateInteriorMapFramePixelsRom, requiresRom: true);
        RunIsolatedValidation(ValidateLargeInteriorMapFramePixelsRom, requiresRom: true);
        RunIsolatedValidation(ValidateRetainedDungeonMapFramePixelsRom, requiresRom: true);
        RunIsolatedValidation(ValidateGaleMapFramePixelsRom, requiresRom: true);
        RunIsolatedValidation(ValidateDungeonMapFramePixelsRom, requiresRom: true);
        RunIsolatedValidation(ValidateAlternateDungeonMapFramePixelsRom, requiresRom: true);
        RunIsolatedValidation(ValidateMapBackgroundSubstitutionsRom, requiresRom: true);
        RunIsolatedValidation(ValidateMapCellsRom, requiresRom: true);
        RunIsolatedValidation(ValidateDungeonMapRom, requiresRom: true);
        RunIsolatedValidation(ValidateRingListMenuRom, requiresRom: true);
        RunIsolatedValidation(ValidateRingListTextSpeedsRom, requiresRom: true);
        RunIsolatedValidation(ValidateRingScrollPixelsRom, requiresRom: true);
        RunIsolatedValidation(ValidateRingAllIconsPixelsRom, requiresRom: true);
        RunIsolatedValidation(ValidateHudHeartBeepRingRom, requiresRom: true);
        RunIsolatedValidation(ValidateSaveQuitMenuRom, requiresRom: true);
        RunIsolatedValidation(ValidateSaveIntegrityRom, requiresRom: true);
        RunIsolatedValidation(ValidateSavedHealthInitializationRom, requiresRom: true);
        RunIsolatedValidation(ValidateContinueInitializationRom, requiresRom: true);
        RunIsolatedValidation(ValidateShopCheckpointInitializationRom, requiresRom: true);
        RunIsolatedValidation(ValidateShieldLifecycleRom, requiresRom: true);
        RunIsolatedValidation(ValidateShieldInterruptionsRom, requiresRom: true);
        RunIsolatedValidation(ValidateBraceletLeverRom, requiresRom: true);
        RunIsolatedValidation(ValidateShieldAirborneRom, requiresRom: true);
        RunIsolatedValidation(ValidateShieldWaterRom, requiresRom: true);
        RunIsolatedValidation(ValidateShieldUnderwaterRom, requiresRom: true);
        RunIsolatedValidation(ValidateBoomerangLifecycleRom, requiresRom: true);
        RunIsolatedValidation(ValidateBoomerangTerrainRom, requiresRom: true);
        RunIsolatedValidation(ValidateBoomerangAllocationRom, requiresRom: true);
        RunIsolatedValidation(ValidateBoomerangDropsRom, requiresRom: true);
        RunIsolatedValidation(ValidateBoomerangAirborneRom, requiresRom: true);
        RunIsolatedValidation(ValidateRaftBoomerangRom, requiresRom: true);
        RunIsolatedValidation(ValidateMinecartBoomerangRom, requiresRom: true);
        RunIsolatedValidation(ValidateSwitchHookLifecycleRom, requiresRom: true);
        RunIsolatedValidation(ValidateSwitchHookTileExchangeRom, requiresRom: true);
        RunIsolatedValidation(ValidateSwitchHookWaterExchangeRom, requiresRom: true);
        RunIsolatedValidation(ValidateSwitchHookUnderwaterExchangeRom, requiresRom: true);
        RunIsolatedValidation(ValidateSwitchHookAllocationRom, requiresRom: true);
        RunIsolatedValidation(ValidateSwitchHookAirborneRom, requiresRom: true);
        RunIsolatedValidation(ValidateShovelLifecycleRom, requiresRom: true);
        RunIsolatedValidation(ValidateShovelTilesRom, requiresRom: true);
        RunIsolatedValidation(ValidateShovelDropsRom, requiresRom: true);
        RunIsolatedValidation(ValidateShovelClearingRom, requiresRom: true);
        RunIsolatedValidation(ValidateShovelWaterRom, requiresRom: true);
        RunIsolatedValidation(ValidateShovelSwimEntryRom, requiresRom: true);
        RunIsolatedValidation(ValidateShovelAirborneRom, requiresRom: true);
        RunIsolatedValidation(ValidateMermaidSwimmingRom, requiresRom: true);
        RunIsolatedValidation(ValidateMermaidDivingRom, requiresRom: true);
        RunIsolatedValidation(ValidateMermaidSeaSwimmingRom, requiresRom: true);
        RunIsolatedValidation(ValidateMermaidSeaDivingGatesRom, requiresRom: true);
        RunIsolatedValidation(ValidateDeepWaterDiveRom, requiresRom: true);
        RunIsolatedValidation(ValidateDeepWaterDungeonDiveRom, requiresRom: true);
        RunIsolatedValidation(ValidateMermaidUnderwaterRom, requiresRom: true);
        RunIsolatedValidation(ValidateUnderwaterSurfacingMasksRom, requiresRom: true);
        RunIsolatedValidation(ValidateUnderwaterSurfaceRom, requiresRom: true);
        RunIsolatedValidation(ValidateUnderwaterDungeonSurfaceRom, requiresRom: true);
        RunIsolatedValidation(ValidateJabuFloodedTilesetsRom, requiresRom: true);
        RunIsolatedValidation(ValidateJabuWaterTilesRom, requiresRom: true);
        RunIsolatedValidation(ValidateJabuWaterTilePairsRom, requiresRom: true);
        RunIsolatedValidation(ValidateUnderwaterHoleRom, requiresRom: true);
        RunIsolatedValidation(ValidateUnderwaterWarpHoleRom, requiresRom: true);
        RunIsolatedValidation(ValidateUnderwaterWaterRom, requiresRom: true);
        RunIsolatedValidation(ValidateUnderwaterCliffCoastRom, requiresRom: true);
        RunIsolatedValidation(ValidateUnderwaterCurrentRom, requiresRom: true);
        RunIsolatedValidation(ValidateUnderwaterFloorRom, requiresRom: true);
        RunIsolatedValidation(ValidateUnderwaterConveyorRom, requiresRom: true);
        RunIsolatedValidation(ValidateUnderwaterCurrentEdgesRom, requiresRom: true);
        RunIsolatedValidation(ValidateConveyorMovementRom, requiresRom: true);
        RunIsolatedValidation(ValidateLavaRecoveryRom, requiresRom: true);
        RunIsolatedValidation(ValidateLedgeJumpRom, requiresRom: true);
        RunIsolatedValidation(ValidateOutdoorLedgeTilesRom, requiresRom: true);
        RunIsolatedValidation(ValidateIndoorLedgeTilesRom, requiresRom: true);
        RunIsolatedValidation(ValidateUnderwaterLedgeTilesRom, requiresRom: true);
        RunIsolatedValidation(ValidateUnderwaterIndoorLedgeTilesRom, requiresRom: true);
        RunIsolatedValidation(ValidateWallSquishRom, requiresRom: true);
        RunIsolatedValidation(ValidateRaftMountRom, requiresRom: true);
        RunIsolatedValidation(ValidateRaftWaterTilesRom, requiresRom: true);
        RunIsolatedValidation(ValidateRaftDismountRom, requiresRom: true);
        RunIsolatedValidation(ValidateRaftSwordARom, requiresRom: true);
        RunIsolatedValidation(ValidateRaftSwordBRom, requiresRom: true);
        RunIsolatedValidation(ValidateRaftShieldARom, requiresRom: true);
        RunIsolatedValidation(ValidateRaftShieldBRom, requiresRom: true);
        RunIsolatedValidation(ValidateRaftSatchelARom, requiresRom: true);
        RunIsolatedValidation(ValidateRaftSatchelBRom, requiresRom: true);
        RunIsolatedValidation(ValidateRaftPegasusARom, requiresRom: true);
        RunIsolatedValidation(ValidateRaftPegasusBRom, requiresRom: true);
        RunIsolatedValidation(ValidateRaftFeatherRom, requiresRom: true);
        RunIsolatedValidation(ValidateRaftSeedShooterRom, requiresRom: true);
        RunIsolatedValidation(ValidateMinecartMountRom, requiresRom: true);
        RunIsolatedValidation(ValidateMinecartTracksRom, requiresRom: true);
        RunIsolatedValidation(ValidateMinecartApproachRom, requiresRom: true);
        RunIsolatedValidation(ValidateMinecartSwordMountRom, requiresRom: true);
        RunIsolatedValidation(ValidateMinecartShieldRom, requiresRom: true);
        RunIsolatedValidation(ValidateMinecartPegasusRom, requiresRom: true);
        RunIsolatedValidation(ValidateMinecartSeedShooterRom, requiresRom: true);
        RunIsolatedValidation(ValidateMinecartDoorsRom, requiresRom: true);
        RunIsolatedValidation(ValidateMinecartDoorControllersRom, requiresRom: true);
        RunIsolatedValidation(ValidateDungeonDoorScriptsRom, requiresRom: true);
        RunIsolatedValidation(ValidatePushBlockTriggerRom, requiresRom: true);
        RunIsolatedValidation(ValidateMinecartShutterRespawnRom, requiresRom: true);
        RunIsolatedValidation(ValidateIndoorLavaRecoveryRom, requiresRom: true);
        RunIsolatedValidation(ValidateUnderwaterLavaRecoveryRom, requiresRom: true);
        RunIsolatedValidation(ValidateUnderwaterIndoorLavaRecoveryRom, requiresRom: true);
        RunIsolatedValidation(ValidateUnderwaterWhirlpoolRom, requiresRom: true);
        RunIsolatedValidation(ValidatePegasusLifecycleRom, requiresRom: true);
        RunIsolatedValidation(ValidatePegasusAirborneRom, requiresRom: true);
        RunIsolatedValidation(ValidatePegasusItemInterruptionsRom, requiresRom: true);
        RunIsolatedValidation(ValidatePegasusGatesRom, requiresRom: true);
        RunIsolatedValidation(ValidatePegasusShieldScrollRom, requiresRom: true);
        RunIsolatedValidation(ValidateHeartRingLifecycleRom, requiresRom: true);
        RunIsolatedValidation(ValidateHeartRingWallSlideRom, requiresRom: true);
        RunIsolatedValidation(ValidateHeartRingSideViewRom, requiresRom: true);
        RunIsolatedValidation(ValidateLinkAirRecoilRom, requiresRom: true);
        RunIsolatedValidation(ValidateLinkAirProjectileContactRom, requiresRom: true);
        RunIsolatedValidation(ValidateLinkContactDamageHandoffRom, requiresRom: true);
        RunIsolatedValidation(ValidateCarriedContactDamageRom, requiresRom: true);
        RunIsolatedValidation(ValidateSeedSatchelLifecycleRom, requiresRom: true);
        RunIsolatedValidation(ValidateSeedSatchelAirborneRom, requiresRom: true);
        RunIsolatedValidation(ValidateSeedSatchelCapacityRom, requiresRom: true);
        RunIsolatedValidation(ValidateSeedSatchelClearingRom, requiresRom: true);
        RunIsolatedValidation(ValidateSeedSatchelReflectorRom, requiresRom: true);
        RunIsolatedValidation(ValidateGaleSatchelIndoorRom, requiresRom: true);
        RunIsolatedValidation(ValidateGaleSatchelAirborneRom, requiresRom: true);
        RunIsolatedValidation(ValidateGaleCaptureRom, requiresRom: true);
        RunIsolatedValidation(ValidateGaleMenuAcceptRom, requiresRom: true);
        RunIsolatedValidation(ValidateGaleAcceptedArrivalRom, requiresRom: true);
        RunIsolatedValidation(ValidateGaleAllDestinationsRom, requiresRom: true);
        RunIsolatedValidation(ValidateGaleArrivalMenuGatesRom, requiresRom: true);
        RunIsolatedValidation(ValidateGaleArrivalMenuResumeRom, requiresRom: true);
        RunIsolatedValidation(ValidateGalePromptFramesRom, requiresRom: true);
        RunIsolatedValidation(ValidateMapAreaTextFramesRom, requiresRom: true);
        RunIsolatedValidation(ValidateMapFadeFramesRom, requiresRom: true);
        RunIsolatedValidation(ValidateMapConditionalTextFramesRom, requiresRom: true);
        RunIsolatedValidation(ValidateRingMenuLifecycleRom, requiresRom: true);
        RunIsolatedValidation(ValidateGaleCaptureGatesRom, requiresRom: true);
        RunIsolatedValidation(ValidateGaleMenuNavigationRom, requiresRom: true);
        RunIsolatedValidation(ValidateSeedSatchelWaterRom, requiresRom: true);
        RunIsolatedValidation(ValidateSeedShooterLifecycleRom, requiresRom: true);
        RunIsolatedValidation(ValidateSeedShooterAirborneRom, requiresRom: true);
        RunIsolatedValidation(ValidateSeedShooterCapacityRom, requiresRom: true);
        RunIsolatedValidation(ValidateSeedShooterEyeRom, requiresRom: true);
        RunIsolatedValidation(ValidateSeedShooterAmmoRom, requiresRom: true);
        RunIsolatedValidation(ValidateSeedShooterGalePegasusRom, requiresRom: true);
        RunIsolatedValidation(ValidateSeedShooterExclusiveAndClearRom, requiresRom: true);
        RunIsolatedValidation(ValidateSeedShooterReflectorRom, requiresRom: true);
        RunIsolatedValidation(ValidateShieldProjectileRom, requiresRom: true);
        RunIsolatedValidation(ValidateHudRom, requiresRom: true);
        RunIsolatedValidation(ValidateHudItemPixelsRom, requiresRom: true);
        RunIsolatedValidation(ValidateHudVisibilityRom, requiresRom: true);
        RunIsolatedValidation(ValidateHudHeartBeepRom, requiresRom: true);
        RunIsolatedValidation(ValidateHudHeartBeepRecoveryRom, requiresRom: true);
        RunIsolatedValidation(ValidateHudHeartBeepMountedRom, requiresRom: true);
        RunIsolatedValidation(ValidateHudHeartBeepInstrumentRom, requiresRom: true);
        RunIsolatedValidation(ValidatePauseMenuFluteTextCompletionRom, requiresRom: true);
        RunIsolatedValidation(ValidateHudHeartBeepShockRom, requiresRom: true);
        RunIsolatedValidation(ValidatePauseMenuRom, requiresRom: true);
        RunIsolatedValidation(ValidatePauseMenuFadeColorsRom, requiresRom: true);
        RunIsolatedValidation(ValidatePauseMenuChordPromotionRom, requiresRom: true);
        RunIsolatedValidation(ValidatePauseMenuGatesRom, requiresRom: true);
        RunIsolatedValidation(ValidatePauseMenuHarpGatesRom, requiresRom: true);
        RunIsolatedValidation(ValidatePauseMenuFluteGatesRom, requiresRom: true);
        RunIsolatedValidation(ValidatePauseMenuHarpCompletionRom, requiresRom: true);
        RunIsolatedValidation(ValidatePauseMenuDeathGatesRom, requiresRom: true);
        RunIsolatedValidation(ValidatePauseMenuScrollGatesRom, requiresRom: true);
        RunIsolatedValidation(ValidateHudHeartBeepScrollRom, requiresRom: true);
        RunIsolatedValidation(ValidatePauseMenuShockGatesRom, requiresRom: true);
        RunIsolatedValidation(ValidatePauseMenuGaleGatesRom, requiresRom: true);
        RunIsolatedValidation(ValidatePauseMenuToggleGatesRom, requiresRom: true);
        RunIsolatedValidation(ValidateHudHeartBeepToggleRom, requiresRom: true);
        RunIsolatedValidation(ValidatePauseMenuLockMasksRom, requiresRom: true);
        RunIsolatedValidation(ValidateRingAppraisalTextSpeedsRom, requiresRom: true);
        RunIsolatedValidation(ValidateRingAppraisalRetentionRom, requiresRom: true);
        RunIsolatedValidation(ValidateRingAppraisalRepeatedRemovalRom, requiresRom: true);
        RunIsolatedValidation(ValidateNameEntryKeyboardRom, requiresRom: true);
        RunIsolatedValidation(ValidateNameEntryEditingRom, requiresRom: true);
        RunIsolatedValidation(ValidateNameEntryAutofireRom, requiresRom: true);
        RunIsolatedValidation(ValidateDialoguePagingRom, requiresRom: true);
        RunIsolatedValidation(ValidateDialogueFormattingRom, requiresRom: true);
        RunIsolatedValidation(ValidateDialogueChoicesRom, requiresRom: true);
        RunIsolatedValidation(ValidateNpcConversationRom, requiresRom: true);
        RunIsolatedValidation(ValidateNewGameIntro);
        RunIsolatedValidation(ValidateNewGameIntroRom, requiresRom: true);
        RunIsolatedValidation(ValidateNewGameIntroInputRom, requiresRom: true);
        RunIsolatedValidation(ValidateGameplayScenePreload);
        RunIsolatedValidation(ValidateDeferredGameplayAssets);
        RunIsolatedValidation(ValidatePreparedIntroHandoff);
        RunIsolatedValidation(ValidatePirateShipCourse);
        RunIsolatedValidation(ValidateImpaHelpFreeze);
        RunIsolatedValidation(ValidateImpaGraphicsContinuation);
        RunIsolatedValidation(ValidateBootLoading);
        RunIsolatedValidation(ValidateSoundEngine);
        RunIsolatedValidation(ValidateSoundDriverControls);
        RunIsolatedValidation(ValidateSoundDriverHandoffs);
        RunIsolatedValidation(ValidateSoundApuTiming);
        RunIsolatedValidation(ValidateSoundMixerBoundaries);
        RunIsolatedValidation(ValidateSoundDriverCatalog00To1f);
        RunIsolatedValidation(ValidateSoundDriverCatalog20To3f);
        RunIsolatedValidation(ValidateSoundDriverCatalog40To5f);
        RunIsolatedValidation(ValidateSoundDriverCatalog60To7f);
        RunIsolatedValidation(ValidateSoundDriverCatalog80To9f);
        RunIsolatedValidation(ValidateSoundDriverCataloga0Tobf);
        RunIsolatedValidation(ValidateSoundDriverCatalogc0Tode);
        RunIsolatedValidation(ValidateSoundApplicationBatching);
        RunIsolatedValidation(ValidateSoundOutputTiming);
        RunIsolatedValidation(ValidateSoundShortEffects);
        RunIsolatedValidation(ValidateGraphicsCache);
        RunIsolatedValidation(ValidateSpritePaletteReaders);
        RunIsolatedValidation(ValidateMonochromeFonts);
        RunIsolatedValidation(ValidateBufferedTilemaps);
        RunIsolatedValidation(ValidateNpcPaletteRebuildOffsets);
        RunIsolatedValidation(ValidateCompanionWallMasks);
        RunIsolatedValidation(ValidateBackgroundPaletteState);
        RunIsolatedValidation(ValidateVanillaTilesets);
        RunIsolatedValidation(ValidateDebugFlagMenu);
        RunIsolatedValidation(ValidateDebugObjectSpawner);
        RunIsolatedValidation(ValidateDebugObjectPreviews);
        RunIsolatedValidation(ValidateDebugCollision);
        RunIsolatedValidation(ValidateDebugRoomWarp);
        RunIsolatedValidation(ValidateDebugMapleShortcut);
        RunIsolatedValidation(ValidateDeathRespawnCheckpoints);
        RunIsolatedValidation(ValidateStartupTransitionFromRoom011);
        RunIsolatedValidation(ValidateScreenTransitionSourceBoundaries);
        RunIsolatedValidation(ValidateScreenTransitionSourceTiming);
        RunIsolatedValidation(ValidateScreenTransitionRendering);
        RunIsolatedValidation(ValidateRoomRasterization);
        RunIsolatedValidation(ValidateRoomPackTransitions);
        RunIsolatedValidation(ValidateRoomTransitionSounds);
        RunIsolatedValidation(ValidateScreenTransitionGraphicsPayloads);
        RunIsolatedValidation(ValidateSymmetryTransitionFromRoom022);
        RunIsolatedValidation(ValidateSigns);
        RunIsolatedValidation(ValidateDialogueClockInputIsolation);
        RunIsolatedValidation(ValidateNpcImplementationManifest);
        RunIsolatedValidation(ValidateNpcs);
        RunIsolatedValidation(ValidateDialogueScreenContext);
        RunIsolatedValidation(ValidateDialoguePaging);
        RunIsolatedValidation(ValidateRooms171And181);
        RunIsolatedValidation(ValidateDekuForestSoldierCutscene);
        RunIsolatedValidation(ValidateDekuForestPalaceCutscene);
        RunIsolatedValidation(ValidatePalaceEntranceGuardCollision);
        RunIsolatedValidation(ValidateRoom173SoldierPair);
        RunIsolatedValidation(ValidateRoom174PastOldLady);
        RunIsolatedValidation(ValidateRooms182And192NpcInteractions);
        RunIsolatedValidation(ValidateRoom183MiscManAndDrops);
        RunIsolatedValidation(ValidateRoom184StoneRabbitsAndSoldier);
        RunIsolatedValidation(ValidateRooms193And194NpcInteractions);
        RunIsolatedValidation(ValidateRoom22fPostman);
        RunIsolatedValidation(ValidateRoom3f7KnowItAllBirds);
        RunIsolatedValidation(ValidateRoom24eOldMan);
        RunIsolatedValidation(ValidateRoom23eToiletHand);
        RunIsolatedValidation(ValidateRoom2e9ShootingGallery);
        RunIsolatedValidation(ValidateRoom39eInteractions);
        RunIsolatedValidation(ValidateRoom3aeInteractions);
        RunIsolatedValidation(ValidateRoom20eNpcInteractions);
        RunIsolatedValidation(ValidateHouse20eEntry);
        RunIsolatedValidation(ValidateRoom10eBottomEntry);
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
        RunIsolatedValidation(ValidateInitialNayruBearReplay);
        RunIsolatedValidation(ValidateInitialNayruObjectArithmetic);
        RunIsolatedValidation(ValidateInitialNayruLinkedGift);
        RunIsolatedValidation(ValidateInitialNayruStoneChild);
        RunIsolatedValidation(ValidateInitialNayruSingingBoundary);
        RunIsolatedValidation(ValidateInitialNayruRecovery);
        RunIsolatedValidation(ValidateInitialNayruPossessionCounters);
        RunIsolatedValidation(ValidateMakuTreeDisappearanceCutscene);
        RunIsolatedValidation(ValidateMakuSproutRescueCutscene);
        RunIsolatedValidation(ValidateRoom05bCompanionTutorial);
        RunIsolatedValidation(ValidateRooms079And089Interactions);
        RunIsolatedValidation(ValidateRoom025Carpenters);
        RunIsolatedValidation(ValidateSymmetryNpcs);
        RunIsolatedValidation(ValidatePatchRestoration);
        RunIsolatedValidation(ValidateSymmetryNutHandoff);
        RunIsolatedValidation(ValidateSymmetrySecrets);
        RunIsolatedValidation(ValidateTuniNutPlacement);
        RunIsolatedValidation(ValidateSymmetryHouseExitPalette);
        RunIsolatedValidation(ValidateSymmetryFidelity);
        RunIsolatedValidation(ValidateSymmetryDungeonEntrance);
        RunIsolatedValidation(ValidateVolcanoEruption);
        RunIsolatedValidation(ValidateVolcanoRockLifecycle);
        RunIsolatedValidation(ValidateVolcanoShakeRng);
        RunIsolatedValidation(ValidateVolcanoRumblePauses);
        RunIsolatedValidation(ValidateFallingBoulderMotion);
        RunIsolatedValidation(ValidateFallingBoulderRooms);
        RunIsolatedValidation(ValidateFallingBoulderContact);
        RunIsolatedValidation(ValidateRoom06aRickyGloves);
        RunIsolatedValidation(ValidateRickyRiding);
        RunIsolatedValidation(ValidateCompanionWaitingFidelity);
        RunIsolatedValidation(ValidateMountedCompanionHurtbox);
        RunIsolatedValidation(ValidateMooshCliffFidelity);
        RunIsolatedValidation(ValidateCompanionAttackFidelity);
        RunIsolatedValidation(ValidateRoom098RickyGlovesPickup);
        RunIsolatedValidation(ValidateRoom06bMooshGoodbye);
        RunIsolatedValidation(ValidateRoom06cMooshRescue);
        RunIsolatedValidation(ValidateMakuTreeSavedCutscene);
        RunIsolatedValidation(ValidateMakuTreeAdviceAndLayout);
        RunIsolatedValidation(ValidateRoom056Comedian);
        RunIsolatedValidation(ValidateRoom07cPoe);
        RunIsolatedValidation(ValidateRoom22ePoe);
        RunIsolatedValidation(ValidateRoom20fCheval);
        RunIsolatedValidation(ValidateRoom179RalphAfterCheval);
        RunIsolatedValidation(ValidateRoom197RalphAfterRafton);
        RunIsolatedValidation(ValidateRooms21eAnd21fRafton);
        RunIsolatedValidation(ValidateRaft);
        RunIsolatedValidation(ValidateRaftFidelity);
        RunIsolatedValidation(ValidateRaftwreckCutscene);
        RunIsolatedValidation(ValidateTokayTheftCutscene);
        RunIsolatedValidation(ValidateTokayIslandInteractions);
        RunIsolatedValidation(ValidateTokayNativeFidelity);
        RunIsolatedValidation(ValidateTokayBusinessScrubs);
        RunIsolatedValidation(ValidateTokayPresentationAndSocket);
        RunIsolatedValidation(ValidateTokaySecret);
        RunIsolatedValidation(ValidateRoom35eSubrosian);
        RunIsolatedValidation(ValidateRoom3f8Npcs);
        RunIsolatedValidation(ValidatePlenSecret);
        RunIsolatedValidation(ValidateTokayDimitriScrollEntry);
        RunIsolatedValidation(ValidateTokayDimitriDeparture);
        RunIsolatedValidation(ValidateTokayRescueEmberEffects);
        RunIsolatedValidation(ValidateDimitriCompanion);
        RunIsolatedValidation(ValidateDimitriCarrying);
        RunIsolatedValidation(ValidateDimitriFlute);
        RunIsolatedValidation(ValidateFlutePresentation);
        RunIsolatedValidation(ValidateDebugRickyFlute);
        RunIsolatedValidation(ValidateDebugDimitriFlute);
        RunIsolatedValidation(ValidateDebugMooshFlute);
        RunIsolatedValidation(ValidateDimitriWaterReturn);
        RunIsolatedValidation(ValidateDimitriCliff);
        RunIsolatedValidation(ValidateDimitriUnmountedHole);
        RunIsolatedValidation(ValidateDimitriForestRescue);
        RunIsolatedValidation(ValidateDimitriForestRescueLinked);
        RunIsolatedValidation(ValidateDimitriForestEntry);
        RunIsolatedValidation(ValidateRoom034Interactions);
        RunIsolatedValidation(ValidateRickyForestQuest);
        RunIsolatedValidation(ValidateRickyForestQuestLinked);
        RunIsolatedValidation(ValidateMooshForestQuest);
        RunIsolatedValidation(ValidateMooshForestQuestLinked);
        RunIsolatedValidation(ValidateDimitriForestQuest);
        RunIsolatedValidation(ValidateDimitriForestQuestLinked);
        RunIsolatedValidation(ValidateForestHintFairies);
        RunIsolatedValidation(ValidateTokayIslandWorldObjects);
        RunIsolatedValidation(ValidateTalusPeaksVines);
        RunIsolatedValidation(ValidateRoom2e6MaskSalesman);
        RunIsolatedValidation(ValidateRoom2f5OldZora);
        RunIsolatedValidation(ValidateRoom2e7Mamamu);
        RunIsolatedValidation(ValidateRoom2e7MamamuDog);
        RunIsolatedValidation(ValidateRoom5c3Gorons);
        RunIsolatedValidation(ValidateRoom5c3GoronBoundaries);
        RunIsolatedValidation(ValidateRoom5c3GoronEntry);
        RunIsolatedValidation(ValidateGoronDanceScrollEntry);
        RunIsolatedValidation(ValidateGoronFileLoad);
        RunIsolatedValidation(ValidateNpcInitializationVisibility);
        RunIsolatedValidation(ValidateGoronVillagers);
        RunIsolatedValidation(ValidateGoronTrades);
        RunIsolatedValidation(ValidateGoronBombStatues);
        RunIsolatedValidation(ValidateGoronDance);
        RunIsolatedValidation(ValidateGoronGallery);
        RunIsolatedValidation(ValidateGoronBigBang);
        RunIsolatedValidation(ValidateGoronBigBangPartLifetime);
        RunIsolatedValidation(ValidateGoronBigBangExplosion);
        RunIsolatedValidation(ValidateGoronTargetCarts);
        RunIsolatedValidation(ValidateGoronTunnel);
        RunIsolatedValidation(ValidateGoronHints);
        RunIsolatedValidation(ValidateRoom2e8DumbbellMan);
        RunIsolatedValidation(ValidateRoom38fTokkey);
        RunIsolatedValidation(ValidateRoom050BombUpgradeFairy);
        RunIsolatedValidation(ValidateRoom2f3DepressedBoy);
        RunIsolatedValidation(ValidateNayruIntroCutscene);
        RunIsolatedValidation(ValidateRalphPortalDepartureEvent);
        RunIsolatedValidation(ValidateAnimations);
        RunIsolatedValidation(ValidateLinkItemGeneratedData);
        RunIsolatedValidation(ValidateSwordBush);
        RunIsolatedValidation(ValidateAirborneSwordRendering);
        RunIsolatedValidation(ValidateShield);
        RunIsolatedValidation(ValidateBombs);
        RunIsolatedValidation(ValidateSeedSatchel);
        RunIsolatedValidation(ValidateSeedShooter);
        RunIsolatedValidation(ValidateScentSeed);
        RunIsolatedValidation(ValidateGaleSeeds);
        RunIsolatedValidation(ValidateGaleSeedTutorial);
        RunIsolatedValidation(ValidateHarp);
        RunIsolatedValidation(ValidateHarpPlaybackRom, requiresRom: true);
        RunIsolatedValidation(ValidateHarpEffectsRom, requiresRom: true);
        RunIsolatedValidation(ValidateSeedTrees);
        RunIsolatedValidation(ValidateRoom180OwlStatue);
        RunIsolatedValidation(ValidateGashaSpots);
        RunIsolatedValidation(ValidateMapleEvents);
        RunIsolatedValidation(ValidateObjectSpeedTable);
        RunIsolatedValidation(ValidateEnemyBehaviorTables);
        RunIsolatedValidation(ValidateEnemyMovementReturnFlags);
        RunIsolatedValidation(ValidateEnemyCornerCharges);
        RunIsolatedValidation(ValidateEnemyPlacementRules);
        RunIsolatedValidation(ValidateEnemyObjectPlacementOrder);
        RunIsolatedValidation(ValidatePlacementScratch);
        RunIsolatedValidation(ValidateRoom465PolsVoices);
        RunIsolatedValidation(ValidateRoom462Moldorms);
        RunIsolatedValidation(ValidateHardhatAndSpinyBeetles);
        RunIsolatedValidation(ValidateSpikedBeetles);
        RunIsolatedValidation(ValidateKeese);
        RunIsolatedValidation(ValidatePeahat);
        RunIsolatedValidation(ValidateTektiteSourceBehavior);
        RunIsolatedValidation(ValidateSwordMaskedMoblinSourceBehavior);
        RunIsolatedValidation(ValidateRoom060Enemies);
        RunIsolatedValidation(ValidateRoom060EnemyCombat);
        RunIsolatedValidation(ValidateGraveyardCrowsAndDropProducers);
        RunIsolatedValidation(ValidateOctoroks);
        RunIsolatedValidation(ValidateRoom043Enemies);
        RunIsolatedValidation(ValidateScentSeedAttraction);
        RunIsolatedValidation(ValidateTokayIslandEnemies);
        RunIsolatedValidation(ValidateArrowMoblins);
        RunIsolatedValidation(ValidateCrownDungeonArrowMoblins);
        RunIsolatedValidation(ValidateCrownDungeonBeamosSourceData);
        RunIsolatedValidation(ValidateCrownDungeonBeamosTiming);
        RunIsolatedValidation(ValidateCrownDungeonBeamosBeam);
        RunIsolatedValidation(ValidateCrownDungeonFireballShooterSourceData);
        RunIsolatedValidation(ValidateCrownDungeonLikeLikeSourceData);
        RunIsolatedValidation(ValidateCrownDungeonLikeLikeStateMachine);
        RunIsolatedValidation(ValidateCrownDungeonLikeLikeContact);
        RunIsolatedValidation(ValidateCrownDungeonLikeLikeSeeds);
        RunIsolatedValidation(ValidateCrownDungeonBallChainSourceData);
        RunIsolatedValidation(ValidateCrownDungeonBallChainMotion);
        RunIsolatedValidation(ValidateCrownDungeonSpikedBallCollisions);
        RunIsolatedValidation(ValidateCrownDungeonBallChainRoom);
        RunIsolatedValidation(ValidateCrownDungeonBallChainSeeds);
        RunIsolatedValidation(ValidateBallChainStatus);
        RunIsolatedValidation(ValidateEnemyStatusCounter);
        RunIsolatedValidation(ValidateEnemyHighKnockback);
        RunIsolatedValidation(ValidateCrownDungeonSmasherSourceData);
        RunIsolatedValidation(ValidateCrownDungeonSmasherMotion);
        RunIsolatedValidation(ValidateCrownDungeonSmasherGrabProtocol);
        RunIsolatedValidation(ValidateCrownDungeonSmasherThrowMotion);
        RunIsolatedValidation(ValidateCrownDungeonSmasherDeath);
        RunIsolatedValidation(ValidateCrownDungeonSmasherInitialization);
        RunIsolatedValidation(ValidateCrownDungeonSmasherLiveAllocation);
        RunIsolatedValidation(ValidateCrownDungeonSmasherRoomLifecycle);
        RunIsolatedValidation(ValidateCrownDungeonSmasherWeaponResponses);
        RunIsolatedValidation(ValidateCrownDungeonSmasherGroundPush);
        RunIsolatedValidation(ValidateCrownDungeonSmasherBraceletLoop);
        RunIsolatedValidation(ValidateCrownDungeonSmasherReservedCollision);
        RunIsolatedValidation(ValidateCrownDungeonSmasherSwitchHook);
        RunIsolatedValidation(ValidateCrownDungeonSmasherRoomEntry);
        RunIsolatedValidation(ValidateCrownDungeonSmasherHeldExpiration);
        RunIsolatedValidation(ValidateCrownDungeonSmasherRewardHandoff);
        RunIsolatedValidation(ValidateCrownDungeonSomariaPlacement);
        RunIsolatedValidation(ValidateCrownDungeonSomariaGraphics);
        RunIsolatedValidation(ValidateCrownDungeonSomariaGroundStates);
        RunIsolatedValidation(ValidateCrownDungeonSomariaCarryThrow);
        RunIsolatedValidation(ValidateCrownDungeonSomariaSwing);
        RunIsolatedValidation(ValidateSomariaLiveBlock);
        RunIsolatedValidation(ValidateSomariaReplacementRom, requiresRom: true);
        RunIsolatedValidation(ValidateSomariaSwingRom, requiresRom: true);
        RunIsolatedValidation(ValidateBiggoronSwordDataRom, requiresRom: true);
        RunIsolatedValidation(ValidateBombchuDataRom, requiresRom: true);
        RunIsolatedValidation(ValidateBombchuGameplayRom, requiresRom: true);
        RunIsolatedValidation(ValidateBiggoronSwordGameplayRom, requiresRom: true);
        RunIsolatedValidation(ValidateSomariaSideViewRom, requiresRom: true);
        RunIsolatedValidation(ValidateSomariaHazardsRom, requiresRom: true);
        RunIsolatedValidation(ValidateSomariaBlockRom, requiresRom: true);
        RunIsolatedValidation(ValidateSomariaPushRom, requiresRom: true);
        RunIsolatedValidation(ValidateSomariaCarryThrowRom, requiresRom: true);
        RunIsolatedValidation(ValidateSomariaCarryCancellationRom, requiresRom: true);
        RunIsolatedValidation(ValidateSomariaCollisionData);
        RunIsolatedValidation(ValidateSomariaSpikedBall);
        RunIsolatedValidation(ValidateSomariaEnemyDamage);
        RunIsolatedValidation(ValidateSomariaZol);
        RunIsolatedValidation(ValidateSomariaGel);
        RunIsolatedValidation(ValidateSomariaEnemyContactRom, requiresRom: true);
        RunIsolatedValidation(ValidateCrownSomariaHeldDamage);
        RunIsolatedValidation(ValidateCrownGelItemTiming);
        RunIsolatedValidation(ValidateSomariaSmasherRom, requiresRom: true);
        RunIsolatedValidation(ValidateSmogProjectile);
        RunIsolatedValidation(ValidateSmogProjectileLive);
        RunIsolatedValidation(ValidateSmogWallMovement);
        RunIsolatedValidation(ValidateSmogFireTimer);
        RunIsolatedValidation(ValidateSmogIntro);
        RunIsolatedValidation(ValidateSmogFullEnemySplit);
        RunIsolatedValidation(ValidateSmogFullControllerAllocation);
        RunIsolatedValidation(ValidateSmogSmallCloud);
        RunIsolatedValidation(ValidateSmogMergedCloud);
        RunIsolatedValidation(ValidateSmogLargeCloud);
        RunIsolatedValidation(ValidateSmogCollisions);
        RunIsolatedValidation(ValidateSmogNativeDeath);
        RunIsolatedValidation(ValidateSmogLive);
        RunIsolatedValidation(ValidateSmogControllerData);
        RunIsolatedValidation(ValidateSmogTiles);
        RunIsolatedValidation(ValidateSmogMerge);
        RunIsolatedValidation(ValidateClinkNativeTiming);
        RunIsolatedValidation(ValidateKillPuffNativeTiming);
        RunIsolatedValidation(ValidateSmogDoorAlias);
        RunIsolatedValidation(ValidateCrownEnemyCornerKnockback);
        RunIsolatedValidation(ValidateSmogCommonStates);
        RunIsolatedValidation(ValidateSmogLastProjectileSlot);
        RunIsolatedValidation(ValidateSmogController);
        RunIsolatedValidation(ValidateSmogSentinel);
        RunIsolatedValidation(ValidateSmogPlayerCoordinates);
        RunIsolatedValidation(ValidateSmogLinkLock);
        RunIsolatedValidation(ValidateSmogPlacement);
        RunIsolatedValidation(ValidateSmogEntry);
        RunIsolatedValidation(ValidateSmogOwnerEffects);
        RunIsolatedValidation(ValidateSmogControllerAdapter);
        RunIsolatedValidation(ValidateSmogControllerLive);
        RunIsolatedValidation(ValidateSmogReward);
        RunIsolatedValidation(ValidateSmogSetupReset);
        RunIsolatedValidation(ValidateCrownPlatforms);
        RunIsolatedValidation(ValidateCrownPlatformMovementScratch);
        RunIsolatedValidation(ValidateSeedShooterEyeStatueData);
        RunIsolatedValidation(ValidateSeedShooterEyeStatueState);
        RunIsolatedValidation(ValidateSeedShooterEyeStatueLive);
        RunIsolatedValidation(ValidateCrownEyeChest);
        RunIsolatedValidation(ValidateLinkOnSpawnedChest);
        RunIsolatedValidation(ValidateCrownEyeChestBoundaries, requiresRom: true);
        RunIsolatedValidation(ValidateCrownPatternChests, requiresRom: true);
        RunIsolatedValidation(ValidateCrownPatternHint);
        RunIsolatedValidation(ValidatePushBlockSynchronizerData);
        RunIsolatedValidation(ValidateInteractionSlotOrder);
        RunIsolatedValidation(ValidateSynchronizedPushBlocks, requiresRom: true);
        RunIsolatedValidation(ValidateSynchronizedBlockButton, requiresRom: true);
        RunIsolatedValidation(ValidateSynchronizedBlockScroll, requiresRom: true);
        RunIsolatedValidation(ValidateSynchronizedBlockPending, requiresRom: true);
        RunIsolatedValidation(ValidateCrownPushInitialization, requiresRom: true);
        RunIsolatedValidation(ValidateSynchronizedBlockAllocation, requiresRom: true);
        RunIsolatedValidation(ValidateSynchronizedBlockQueue, requiresRom: true);
        RunIsolatedValidation(ValidateCrownPushContact, requiresRom: true);
        RunIsolatedValidation(ValidateCrownPushDestination, requiresRom: true);
        RunIsolatedValidation(ValidatePuzzleTrapReset, requiresRom: true);
        RunIsolatedValidation(ValidateCrownTrapJump);
        RunIsolatedValidation(ValidateCrownTrapFall);
        RunIsolatedValidation(ValidateCrownWallFollowers);
        RunIsolatedValidation(ValidateLinkSquish);
        RunIsolatedValidation(ValidateWallSquish);
        RunIsolatedValidation(ValidateButtonBridge, requiresRom: true);
        RunIsolatedValidation(ValidateTimedSeedReflectors);
        RunIsolatedValidation(ValidateCrownTorches);
        RunIsolatedValidation(ValidateCrownTorchTileQueue);
        RunIsolatedValidation(ValidateCrownRetractableChest);
        RunIsolatedValidation(ValidateCrownPlacedAllocation);
        RunIsolatedValidation(ValidateCrownButtons, requiresRom: true);
        RunIsolatedValidation(ValidateCrownButtonHeight, requiresRom: true);
        RunIsolatedValidation(ValidateCrownButtonChestItem);
        RunIsolatedValidation(ValidateChangedTileQueue);
        RunIsolatedValidation(ValidateParentItemUsage);
        RunIsolatedValidation(ValidateDynamicItemSlots);
        RunIsolatedValidation(ValidateCrownDungeonSmasherLinkResponses);
        RunIsolatedValidation(ValidateCrownDungeonBraceletLiftCancellation);
        RunIsolatedValidation(ValidateLikeLikePlayerGrab);
        RunIsolatedValidation(ValidateCrownDungeonFireballShooter);
        RunIsolatedValidation(ValidateFireballCounterRoomEntry);
        RunIsolatedValidation(ValidateNativeFireballCollisions);
        RunIsolatedValidation(ValidateCrownDungeonSwordEnemies);
        RunIsolatedValidation(ValidateCrownDungeonSwordSeedCollisions);
        RunIsolatedValidation(ValidateSymmetryEnemies);
        RunIsolatedValidation(ValidateCheepCheeps);
        RunIsolatedValidation(ValidateArrowDarknuts);
        RunIsolatedValidation(ValidatePodobooTowers);
        RunIsolatedValidation(ValidateHostileProjectileLifecycle);
        RunIsolatedValidation(ValidateEnemyShieldBumps);
        RunIsolatedValidation(ValidateEnemySwordKnockback);
        RunIsolatedValidation(ValidateEnemyDamageBlink);
        RunIsolatedValidation(ValidateEnemyHazards);
        RunIsolatedValidation(ValidateStalfos);
        RunIsolatedValidation(ValidateZolsAndGels);
        RunIsolatedValidation(ValidateItemDrops);
        RunIsolatedValidation(ValidateTerrainShadows);
        RunIsolatedValidation(ValidateMapleDropShadows);
        RunIsolatedValidation(ValidateMapleTransitionVisibility);
        RunIsolatedValidation(ValidateFountainDecorations);
        RunIsolatedValidation(ValidateDiggingEnemies);
        RunIsolatedValidation(ValidateTimePortals);
        RunIsolatedValidation(ValidateTimeWarpLandingFidelity);
        RunIsolatedValidation(ValidateTimePortalContactFidelity);
        RunIsolatedValidation(ValidateHiddenPortalSpots);
        RunIsolatedValidation(ValidateRoom141WaterPushblocks);
        RunIsolatedValidation(ValidateEnterPastEvent);
        RunIsolatedValidation(ValidateCrescentIslandPastStairs);
        RunIsolatedValidation(ValidateRoom5ccDiveWarp);
        RunIsolatedValidation(ValidateHouseWarp);
        RunIsolatedValidation(ValidateCaveWarps);
        RunIsolatedValidation(ValidateMakuTreeSouthExitReveal);
        RunIsolatedValidation(ValidateTerrain);
        RunIsolatedValidation(ValidateLinkMovementScratch);
        RunIsolatedValidation(ValidateSwordBeamScratch);
        RunIsolatedValidation(ValidateBombMovementScratch);
        RunIsolatedValidation(ValidateBombScrolling);
        RunIsolatedValidation(ValidateSeedMovementScratch);
        RunIsolatedValidation(ValidateDropMovementScratch);
        RunIsolatedValidation(ValidateDropConveyors);
        RunIsolatedValidation(ValidateGaleMovementScratch);
        RunIsolatedValidation(ValidateBoomerangMovementScratch);
        RunIsolatedValidation(ValidateSwitchHookMovementScratch);
        RunIsolatedValidation(ValidateBraceletMovementScratch);
        RunIsolatedValidation(ValidatePushBlockMovementScratch);
        RunIsolatedValidation(ValidateFallingHoleMovementScratch, requiresRom: true);
        RunIsolatedValidation(ValidateLinkTopDownSwimming);
        RunIsolatedValidation(ValidateLinkSideScrollSwimming);
        RunIsolatedValidation(ValidateSideScrollPitRespawn);
        RunIsolatedValidation(ValidateSideScrollSwimmingGameplay);
        RunIsolatedValidation(ValidateLedgeInteractionState);
        RunIsolatedValidation(ValidateFloorDoorRespawnState);
        RunIsolatedValidation(ValidateGetItemState);
        RunIsolatedValidation(ValidateSideScrollSwimmingExits);
        RunIsolatedValidation(ValidateSideScrollSwimmingBubbles);
        RunIsolatedValidation(ValidateSideScrollSwimmingKinematics);
        RunIsolatedValidation(ValidateLinkTerrainEffects);
        RunIsolatedValidation(ValidateHealth);
        RunIsolatedValidation(ValidateLinkDamagePaletteAssets);
        RunIsolatedValidation(ValidateChests);
        RunIsolatedValidation(ValidateInventoryFoundation);
        RunIsolatedValidation(ValidateSaveOptions);
        RunIsolatedValidation(ValidateSaveMenuBackgroundIsolation);
        RunIsolatedValidation(ValidateInventoryIconFidelity);
        RunIsolatedValidation(ValidateRingFunctionality);
        RunIsolatedValidation(ValidateBraceletChestAndPushGate);
        RunIsolatedValidation(ValidatePushBlocks, requiresRom: true);
        RunIsolatedValidation(ValidateDungeonMechanics);
        RunIsolatedValidation(ValidateRoom29eOrbBridge,requiresRom:true);
        RunIsolatedValidation(ValidateRollingRidgeButtonBridges);
        RunIsolatedValidation(ValidateMoblinKeepCollapsingFloor);
        RunIsolatedValidation(ValidateRoom054SeedCliffsAndBridge);
        RunIsolatedValidation(ValidateRoom449EchoingHowl);
        RunIsolatedValidation(ValidateRoom44aShadowHagBoss);
        RunIsolatedValidation(ValidateRoom44bMoonlitGrottoInteractions);
        RunIsolatedValidation(ValidateRoom44eMoonlitGrotto);
        RunIsolatedValidation(ValidateRoom44dSubterrorMiniboss);
        RunIsolatedValidation(ValidateRoom456MoonlitGrotto);
        RunIsolatedValidation(ValidateRoom458MoonlitGrotto);
        RunIsolatedValidation(ValidateRoom45eMoonlitGrotto);
        RunIsolatedValidation(ValidateRoom45bMoonlitGrotto);
        RunIsolatedValidation(ValidateRoom461MoonlitGrotto);
        RunIsolatedValidation(ValidateMoonlitGrottoCrystalCutsceneFreeze);
        RunIsolatedValidation(ValidateRoom464MoonlitGrotto, requiresRom:true);
        RunIsolatedValidation(ValidateDungeonSpinner, requiresRom: true);
        RunIsolatedValidation(ValidateSkullDungeonShroudedStalfos);
        RunIsolatedValidation(ValidateSkullDungeonFallingRopes);
        RunIsolatedValidation(ValidateSkullDungeonBladeTraps);
        RunIsolatedValidation(ValidateSkullDungeonStalfos);
        RunIsolatedValidation(ValidateEnemyLavaAvoidance);
        RunIsolatedValidation(ValidateSkullDungeonGibdos);
        RunIsolatedValidation(ValidateSkullDungeonFireKeese);
        RunIsolatedValidation(ValidateFireKeeseScreenTransition);
        RunIsolatedValidation(ValidateSwitchHookSourceData);
        RunIsolatedValidation(ValidateSwitchHookFlight);
        RunIsolatedValidation(ValidateSwitchHookTileExchange);
        RunIsolatedValidation(ValidateSwitchHookGibdoExchange);
        RunIsolatedValidation(ValidateSwitchHookStalfosAndRope);
        RunIsolatedValidation(ValidateSwitchHookShroudedStalfos);
        RunIsolatedValidation(ValidateSwitchHookDeflection);
        RunIsolatedValidation(ValidateSkullDungeonColorGels);
        RunIsolatedValidation(ValidateSkullDungeonFloors, requiresRom: true);
        RunIsolatedValidation(ValidateSkullDungeonPatterns, requiresRom: true);
        RunIsolatedValidation(ValidateSkullDungeonCubes, requiresRom: true);
        RunIsolatedValidation(ValidateSkullDungeonFillers, requiresRom: true);
        RunIsolatedValidation(ValidateSkullDungeonLevers);
        RunIsolatedValidation(ValidateSkullDungeonPlatforms, requiresRom: true);
        RunIsolatedValidation(ValidateSkullDungeonMinecarts);
        RunIsolatedValidation(ValidateSkullDungeonRails, requiresRom: true);
        RunIsolatedValidation(ValidateSwitchHookDungeonSwitches, requiresRom: true);
        RunIsolatedValidation(ValidateSwitchHookKeese);
        RunIsolatedValidation(ValidateSkullHookDamage);
        RunIsolatedValidation(ValidateSkullPeahatCycle);
        RunIsolatedValidation(ValidateSkullSideViewTraversal);
        RunIsolatedValidation(ValidateSkullMinibossPortal);
        RunIsolatedValidation(ValidateSkullZolCycles);
        RunIsolatedValidation(ValidateSkullZolSword);
        RunIsolatedValidation(ValidateSkullMoldormObjects);
        RunIsolatedValidation(ValidateSkullMoldormReferences);
        RunIsolatedValidation(ValidateSkullMoldormPartWrites);
        RunIsolatedValidation(ValidateSkullMoldormSwordWrite);
        RunIsolatedValidation(ValidateSkullDeathPartLifecycle);
        RunIsolatedValidation(ValidateSkullMoldormItemSwitchWrites);
        RunIsolatedValidation(ValidateSkullMoldormHitTiming);
        RunIsolatedValidation(ValidateSkullMoldormHazards);
        RunIsolatedValidation(ValidateTopDownAirSteering);
        RunIsolatedValidation(ValidatePegasusProjectiles);
        RunIsolatedValidation(ValidateSkullBossPegasus);
        RunIsolatedValidation(ValidateSkullBossSeeds);
        RunIsolatedValidation(ValidateSkullSeedScan);
        RunIsolatedValidation(ValidateSkullNativeSeeds);
        RunIsolatedValidation(ValidateSkullStunMotion);
        RunIsolatedValidation(ValidateSkullOrbScripts, requiresRom: true);
        RunIsolatedValidation(ValidateSkullStationaryOrb, requiresRom: true);
        RunIsolatedValidation(ValidateSkullEnergyBeads);
        RunIsolatedValidation(ValidateSkullEssenceObjects);
        RunIsolatedValidation(ValidateArmosWarriorSourceData);
        RunIsolatedValidation(ValidateEyesoarSourceData);
        RunIsolatedValidation(ValidateEyesoarFight);
        RunIsolatedValidation(ValidateSkullEssenceSequence);
        RunIsolatedValidation(ValidateCrownEssence);
        RunIsolatedValidation(ValidateCrownEntranceGates);
        RunIsolatedValidation(ValidateCrownPortal);
        RunIsolatedValidation(ValidateCrownOwlAllocation);
        RunIsolatedValidation(ValidateTingleSparkleLifecycle);
        RunIsolatedValidation(ValidateCrownOwlDialogue);
        RunIsolatedValidation(ValidateCrownShutterContact, requiresRom: true);
        RunIsolatedValidation(ValidateCrownShutterSwitchHook, requiresRom: true);
        RunIsolatedValidation(ValidateCrownShutterTriggers, requiresRom: true);
        RunIsolatedValidation(ValidateCrownShutterSomaria, requiresRom: true);
        RunIsolatedValidation(ValidateCrownShutterTiming, requiresRom: true);
        RunIsolatedValidation(ValidateCrownShutterPause, requiresRom: true);
        RunIsolatedValidation(ValidateCrownShutterScroll, requiresRom: true);
        RunIsolatedValidation(ValidateCrownShutterPalette, requiresRom: true);
        RunIsolatedValidation(ValidateCrownStatueRoutes);
        RunIsolatedValidation(ValidateCrownSquishDeath);
        RunIsolatedValidation(ValidateSmasherCommonStates);
        RunIsolatedValidation(ValidateSmasherCornerKnockback);
        RunIsolatedValidation(ValidateSmasherNativeBytes);
        RunIsolatedValidation(ValidateSmasherScrollAllocation);
        RunIsolatedValidation(ValidateSmasherUnlinkedExpiry);
        RunIsolatedValidation(ValidateCrownSparkSeeds);
        RunIsolatedValidation(ValidateCrownBoomerangTransformation);
        RunIsolatedValidation(ValidateBoomerangFlight);
        RunIsolatedValidation(ValidateBoomerangInput);
        RunIsolatedValidation(ValidateCrownBoomerangCollisions);
        RunIsolatedValidation(ValidateBoomerangDrops);
        RunIsolatedValidation(ValidateBallChainAllocation);
        RunIsolatedValidation(ValidateDrowningStateBoundary);
        RunIsolatedValidation(ValidateSomariaSatchelInput, requiresRom: true);
        RunIsolatedValidation(ValidateSomariaFeatherInput, requiresRom: true);
        RunIsolatedValidation(ValidateSomariaParentPriority, requiresRom: true);
        RunIsolatedValidation(ValidateSomariaInstrumentInput);
        RunIsolatedValidation(ValidateSomariaOtherInput, requiresRom: true);
        RunIsolatedValidation(ValidateCrownModalStair);
        RunIsolatedValidation(ValidateCrownWarpMarker);
        RunIsolatedValidation(ValidateCrownRecoilStair);
        RunIsolatedValidation(ValidateCrownWallFollowerSomaria);
        RunIsolatedValidation(ValidateCrownWhispSeeds);
        RunIsolatedValidation(ValidateCrownWallFollowerMelee);
        RunIsolatedValidation(ValidateCrownWallFollowerBeam);
        RunIsolatedValidation(ValidateSmasherWarpFade);
        RunIsolatedValidation(ValidateCrownShutterCoverage, requiresRom: true);
        RunIsolatedValidation(ValidateCrownChestRewards);
        RunIsolatedValidation(ValidateCrownKeyLocks, requiresRom: true);
        RunIsolatedValidation(ValidateCrownKeyAllocation, requiresRom: true);
        RunIsolatedValidation(ValidateCrownKeyTiming);
        RunIsolatedValidation(ValidateCrownKeyDoorTiming, requiresRom: true);
        RunIsolatedValidation(ValidateCrownKeyDoorEdges, requiresRom: true);
        RunIsolatedValidation(ValidateCrownKeyDoorDeath, requiresRom: true);
        RunIsolatedValidation(ValidateCrownKeyDoorContention, requiresRom: true);
        RunIsolatedValidation(ValidateCrownKeyDoorSwitchHook, requiresRom: true);
        RunIsolatedValidation(ValidateCrownKeyDoorScroll, requiresRom: true);
        RunIsolatedValidation(ValidateCrownStairs);
        RunIsolatedValidation(ValidateCrownStairEnemyFadeInitialization);
        RunIsolatedValidation(ValidateCrownSwordEnemyFadeInitialization);
        RunIsolatedValidation(ValidateCrownPassageReturns);
        RunIsolatedValidation(ValidateCrownEnemyStairs);
        RunIsolatedValidation(ValidateCrownExteriorWarp);
        RunIsolatedValidation(ValidateCrownMinorChestRewards);
        RunIsolatedValidation(ValidateCrownRaisedFloor);
        RunIsolatedValidation(ValidateCrownToggleTiles, requiresRom: true);
        RunIsolatedValidation(ValidateCrownToggleCutscene, requiresRom: true);
        RunIsolatedValidation(ValidateCrownToggleExit, requiresRom: true);
        RunIsolatedValidation(ValidateCrownToggleStair, requiresRom: true);
        RunIsolatedValidation(ValidateCrownStairGates);
        RunIsolatedValidation(ValidateCrownCarriedStair);
        RunIsolatedValidation(ValidateCrownItemStair);
        RunIsolatedValidation(ValidateCrownJumpStair);
        RunIsolatedValidation(ValidateCrownExchangeStair);
        RunIsolatedValidation(ValidateCrownToggleQueue, requiresRom: true);
        RunIsolatedValidation(ValidateCrownToggleScan, requiresRom: true);
        RunIsolatedValidation(ValidateCrownOrbPlacements);
        RunIsolatedValidation(ValidatePuzzlePuffTiming);
        RunIsolatedValidation(ValidateArmosWarriorFight);
        RunIsolatedValidation(ValidateKingMoblinFight);
        RunIsolatedValidation(ValidateKingMoblinDefeatTiming);
        RunIsolatedValidation(ValidateKingMoblinBombsAndRecentering);
        RunIsolatedValidation(ValidateDefeatedMoblinSequence);
        RunIsolatedValidation(ValidateKingMoblinCancellation);
        RunIsolatedValidation(ValidateRoom2cfCucco);
        RunIsolatedValidation(ValidateRoom2e3Interactions);
        RunIsolatedValidation(ValidateRoom5b6Interactions);
        RunIsolatedValidation(ValidateRoom5bfInteractions);
        RunIsolatedValidation(ValidateSpiritsGraveEntranceInteractions);
        RunIsolatedValidation(ValidateOverworldKeyholeAndGraveyardGate,requiresRom:true);
        RunIsolatedValidation(ValidateCrownDungeonEntrance,requiresRom:true);
        RunIsolatedValidation(ValidateMermaidsCaveEntrances,requiresRom:true);
        RunIsolatedValidation(ValidateLibraryKeyholeRom,requiresRom:true);
        RunIsolatedValidation(ValidateDarkRoomInteractions);
        RunIsolatedValidation(ValidateDungeonKeyDoors, requiresRom: true);
        RunIsolatedValidation(ValidateSpiritsGrave);
        RunIsolatedValidation(ValidateMapScreen);
        RunIsolatedValidation(ValidateMapPresentationContract);
        RunIsolatedValidation(ValidateLynnaShopInteractions);
        RunIsolatedValidation(ValidateSyrupShopInteractions);
        RunIsolatedValidation(ValidateSyrupShopGraphics);
        RunIsolatedValidation(ValidateObjectDrawOrder);
        RunIsolatedValidation(ValidateItemDropDrawPriority);
        RunIsolatedValidation(ValidateHiddenShopInteractions);
        RunIsolatedValidation(ValidateVasuShopInteractions);
        RunIsolatedValidation(ValidateRemoteMakuFirstEssenceCutscene);
        RunIsolatedValidation(ValidateRemoteMakuSecondEssenceCutscene);
        RunIsolatedValidation(ValidateRemoteMakuConfettiDrawOrder);
        RunIsolatedValidation(ValidateRemoteMakuConfettiCoordinates);
        RunIsolatedValidation(ValidateRemoteMakuHudPlacement);
        RunIsolatedValidation(ValidateRemoteMakuHarpCutscene);
        RunIsolatedValidation(ValidatePostD3RemoteMakuCutscene);
        RunIsolatedValidation(ValidateFairiesWoodsSequence);
        RunIsolatedValidation(ValidateGameOverRestart);
        RunIsolatedValidation(ValidateSaveAndQuitToTitle);
        RunIsolatedValidation(ValidateRoom083Interactions);
        RunIsolatedValidation(ValidateFountainFairies);
        RunIsolatedValidation(ValidateDebugSavestates);
        RunIsolatedValidation(ValidateDebugSavestateMinecarts);
        RunIsolatedValidation(ValidateInventoryFlagIsolation);
        RunIsolatedValidation(ValidateMovingSideScrollPlatforms);
        RunIsolatedValidation(ValidateWingDungeon, requiresRom:true);
        RunIsolatedValidation(ValidateHeadThwompFidelity);

        RunRegisteredValidations();
        if (_validationFilter is not null &&
            _executedValidationCount + _skippedValidationCount + _failedValidationCount == 0)
        {
            throw new InvalidOperationException(
                $"No validation method named '{_validationFilter}' was registered.");
        }
        GD.Print($"Validation finished: {_executedValidationCount} passed, " +
            $"{_skippedValidationCount} skipped, {_failedValidationCount} failed.");
        GD.Print($"VALIDATION_COMPLETE shard={_shardIndex + 1}/{_shardCount} " +
            $"executed={_executedValidationCount} skipped={_skippedValidationCount} " +
            $"registered={_validationOrdinal} failed={_failedValidationCount}");
    }
}
