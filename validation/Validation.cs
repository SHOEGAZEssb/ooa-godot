using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot : GameRoot
{
    private int _neutralInputFrames;
    private ValidationCutsceneTrace? _enterPastCommandTrace;

    private sealed class ValidationTimelineStep(int durationFrames) : IRoomEventTimelineStep
    {
        public int DurationFrames { get; } = durationFrames;
        public int Counter { get; set; }
    }

    private sealed class ValidationMenuClient(string name) : IOracleMenuLifecycleClient
    {
        public string MenuName { get; } = name;
        public int OpenAtWhiteCalls { get; private set; }
        public int CloseAtWhiteCalls { get; private set; }
        public int ClosedCalls { get; private set; }

        public void OpenAtWhite() => OpenAtWhiteCalls++;
        public void CloseAtWhite() => CloseAtWhiteCalls++;
        public void LifecycleClosed() => ClosedCalls++;
    }

    private sealed class ValidationRingPlayerWorld : IPlayerWorld
    {
        public bool IsTransitioning => false;
        public bool DialogueOpen => false;
        public bool SwordDisabled => false;
        public bool ItemUsageDisabled => false;
        public bool MovementDisabled => false;
        public bool RingTransformationsAllowed { get; set; } = true;
        public int SwordHitCalls { get; private set; }
        public int LastSwordDamage { get; private set; }
        public int ExpertTileHitCalls { get; private set; }
        public int SwordBeamCalls { get; private set; }
        public int LastSwordBeamDirection { get; private set; } = -1;
        public List<int> Sounds { get; } = new();

        public bool ApplySwordHit(Player player, Rect2 hitbox)
        {
            SwordHitCalls++;
            LastSwordDamage = player.SwordDamage;
            return false;
        }
        public bool ApplySwordTileHit(Player player, int direction, bool swordPoke) => false;
        public bool ApplyExpertsRingTileHit(Player player, int direction)
        {
            ExpertTileHitCalls++;
            return true;
        }
        public bool TryCreateSwordBeam(Player player, int direction)
        {
            SwordBeamCalls++;
            LastSwordBeamDirection = direction;
            return true;
        }
        public void PlaySound(int soundId) => Sounds.Add(soundId);
        public bool TryInteract(Player player) => false;
        public bool TrySecondaryInteract(Player player) => false;
        public bool TryUseBracelet(Player player) => false;
        public int TryUseSeedSatchel(Player player) => 0;
        public bool DigWithShovel(Vector2 point, Vector2I direction) => false;
        public bool Collides(Vector2 playerPosition) => false;
        public Vector2 ResolveMovement(
            Vector2 playerPosition, Vector2 movement, bool allowWallSlide) => movement;
        public bool IsPushingAgainstWall(
            Vector2 playerPosition, Vector2I facing, Vector2 movementInput) => false;
        public void UpdatePushableBlocks(
            Vector2 playerPosition, Vector2I facing, Vector2 movementInput) { }
        public ActiveTerrainInfo GetActiveTerrain(Vector2 playerPosition) => default;
        public Vector2 GetTerrainPush(Vector2 playerPosition) => Vector2.Zero;
        public bool TryStartLedgeHop(
            Player player, Vector2 from, Vector2 attemptedMovement) => false;
        public void SpawnDrowningSplash(
            Vector2 position, OracleRoomData.HazardType hazard) { }
        public bool CheckTileWarp(Player player) => false;
        public void CheckRoomExit(Player player) { }
    }

    private sealed class ValidationCutsceneTrace : ICutsceneCommandTraceSink
    {
        public List<CutsceneCommandTraceEntry> Entries { get; } = new();
        public List<CutsceneObservationTraceEntry> Observations { get; } = new();
        public void Record(CutsceneCommandTraceEntry entry) => Entries.Add(entry);
        public void RecordObservation(CutsceneObservationTraceEntry entry) =>
            Observations.Add(entry);

        public bool Saw(string observation, string? actor = null, int? value = null) =>
            Observations.Any(entry =>
                entry.Observation == observation &&
                (actor is null || entry.Actor?.Value == actor) &&
                (!value.HasValue || entry.Value == value.Value));

        public int LastValue(string observation) =>
            Observations.Last(entry => entry.Observation == observation).Value;

        public int OrValues(string observation) =>
            Observations.Where(entry => entry.Observation == observation)
                .Aggregate(0, (mask, entry) => mask | entry.Value);

        public int Count(string observation) =>
            Observations.Count(entry => entry.Observation == observation);

        public bool SawPosition(string observation, string actor, Vector2 position) =>
            Observations.Any(entry => entry.Observation == observation &&
                entry.Actor?.Value == actor && entry.Position.IsEqualApprox(position));
    }

    private sealed class ValidationImpaPostPushHost(int linkAngle) : ICutsceneCommandHost
    {
        public ValidationCutsceneTrace Trace { get; } = new();
        public bool DialogueOpen { get; private set; }
        public bool IsLinkedGame => false;
        public int FrameCounter { get; private set; }
        public ICutsceneCommandTraceSink TraceSink => Trace;
        public Vector2 Position { get; private set; }
        public Vector2I Facing { get; private set; } = Vector2I.Right;
        public List<int> TextIds { get; } = new();
        public int Signal { get; private set; } = 0x06;
        public bool Ended { get; private set; }

        public bool HasActorBinding(CutsceneActorId actor) => actor.Value == "Impa";
        public void AdvanceValidationFrame() => FrameCounter++;
        public void CloseDialogue() => DialogueOpen = false;
        public void SetInputEnabled(bool enabled) => throw Unsupported(nameof(SetInputEnabled));
        public void SetMenuEnabled(bool enabled) => throw Unsupported(nameof(SetMenuEnabled));
        public void SetDisabledObjects(int value) =>
            throw Unsupported(nameof(SetDisabledObjects));
        public bool GateOpen(string gate) => throw Unsupported(nameof(GateOpen));
        public bool MemoryEquals(string binding, int value) =>
            binding == "w1Link.angle" && linkAngle == value;
        public void ShowText(int textId, string message)
        {
            TextIds.Add(textId);
            DialogueOpen = true;
        }
        public void SetActorAnimation(
            string actor,
            int animation,
            string encodedAnimation)
        {
        }
        public void SetActorMovementAnimation(
            string actor,
            int angle,
            string encodedAnimation)
        {
            Vector2 direction = OracleObjectMath.StrictCardinalVector(angle);
            Facing = new Vector2I(
                Mathf.RoundToInt(direction.X), Mathf.RoundToInt(direction.Y));
        }
        public void SetActorCollisionRadii(string actor, int radiusY, int radiusX) =>
            throw Unsupported(nameof(SetActorCollisionRadii));
        public void SetActorButtonSensitive(string actor) =>
            throw Unsupported(nameof(SetActorButtonSensitive));
        public void MoveActorAtSpeed(string actor, int speed, int angle) =>
            Position += OracleObjectMath.StrictCardinalVector(angle) * (speed / 40.0f);
        public void SetActorZ(string actor, int zFixed) =>
            throw Unsupported(nameof(SetActorZ));
        public void SetActorVisible(string actor, bool visible) =>
            throw Unsupported(nameof(SetActorVisible));
        public void WriteMemory(string binding, int value)
        {
            if (binding != "wTmpcfc0.genericCutscene.cfd0")
                throw Unsupported(nameof(WriteMemory));
            Signal = value;
        }
        public void PlaySound(int sound) => throw Unsupported(nameof(PlaySound));
        public void SetGlobalFlag(int flag) => throw Unsupported(nameof(SetGlobalFlag));
        public void OrRoomFlag(int flag) => throw Unsupported(nameof(OrRoomFlag));
        public void RunNativeHandler(string handler) =>
            throw Unsupported(nameof(RunNativeHandler));
        public void ScriptEnded() => Ended = true;

        private static InvalidOperationException Unsupported(string operation) =>
            new($"Validation Impa post-push host does not support {operation}.");
    }

    public override void _Ready()
    {
        base._Ready();
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
        ValidateMenuLifecycleFoundation();
        _world.ValidateRepresentativeRooms();
        ValidateOracleObjectMath();
        ValidateOracleRandom();
        ValidateRoomEventTimeline();
        ValidateSaveDataFoundation();
        ValidateSaveStore();
        ValidateTreasureInterpreter();
        ValidateDungeonCollectibles();
        ValidateRoomTileChanges();
        ValidateExplicitSavePersistence();
        ValidateMainMenu();
        ValidateNewGameIntro();
        ValidateSoundEngine();
        ValidateGraphicsCache();
        ValidateDebugFlagMenu();
        ValidateDebugCollision();
        ValidateDeathRespawnCheckpoints();

        LoadValidationRoom(0, 0x11);
        ValidateStartupTransition();
        LoadValidationRoom(0, 0x22);
        ValidateSymmetryTransition();

        ValidateSigns();
        ValidateNpcs();
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
        ValidateNayruIntroCutscene();
        ValidateRalphPortalDepartureEvent();
        ValidateAnimations();
        ValidateSwordBush();
        ValidateShield();
        ValidateShovel();
        ValidateSeedSatchel();
        ValidateEnemyPlacementRules();
        ValidateEnemyObjectPlacementOrder();
        ValidateKeese();
        ValidateOctoroks();
        ValidateStalfos();
        ValidateZolsAndGels();
        ValidateItemDrops();
        ValidateTimePortals();
        ValidateEnterPastEvent();
        ValidateHouseWarp();
        ValidateCaveWarps();
        ValidateTerrain();
        ValidateHealth();
        ValidateChests();
        ValidateInventoryFoundation();
        ValidateInventoryMenu();
        ValidateRingFunctionality();
        ValidateBraceletChestAndPushGate();
        ValidatePushBlocks();
        ValidateDungeonMechanics();
        ValidateDungeonKeyDoors();
        ValidateMapScreen();
        ValidateLynnaShopInteractions();
        ValidateVasuShopInteractions();
        ValidateSaveAndQuitToTitle();

        GD.Print("Validated all gameplay and world-data scenarios.");
    }
}
