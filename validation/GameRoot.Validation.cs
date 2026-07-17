using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace oracleofages;

public sealed class ValidationGameRoot : GameRoot
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
        ValidateDeathRespawnCheckpoints();

        LoadValidationRoom(0, 0x11);
        ValidateStartupTransition();
        LoadValidationRoom(0, 0x22);
        ValidateSymmetryTransition();

        ValidateSigns();
        ValidateNpcs();
        ValidateRoom149FamilyInteractions();
        ValidateNpcFlagVisibility();
        ValidateBipinBlossomNaming();
        ValidateImpaIntroEncounter();
        ValidateMakuTreeDisappearanceCutscene();
        ValidateNayruIntroCutscene();
        ValidateRalphPortalDepartureEvent();
        ValidateAnimations();
        ValidateSwordBush();
        ValidateEnemyPlacementRules();
        ValidateEnemyObjectPlacementOrder();
        ValidateKeese();
        ValidateOctoroks();
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
        ValidateBraceletChestAndPushGate();
        ValidatePushBlocks();
        ValidateMapScreen();
        ValidateSaveAndQuitToTitle();

        GD.Print("Validated all gameplay and world-data scenarios.");
    }

    private void ValidateGameplaySceneGraph()
    {
        Vector2 roomViewportSize = new(
            OracleRoomData.ViewportWidth, OracleRoomData.ViewportHeight);
        Vector2 screenSize = new(
            OracleRoomData.ViewportWidth, OracleRoomData.ScreenHeight);
        if (_sound.GetParent() != this || _sound.Owner != this ||
            _scene.GetParent() != this ||
            _scene.SceneFilePath != GameSceneGraph.ScenePath ||
            GetChildCount() != 2 ||
            _scene.WorldRoot.GetParent() != _scene ||
            _scene.WorldRoot.Owner != _scene ||
            _scene.InterfaceLayer.GetParent() != _scene ||
            _scene.InterfaceLayer.Owner != _scene ||
            _roomView.GetParent() != _scene.WorldRoot ||
            _player.GetParent() != _scene.WorldRoot ||
            _roomCamera.GetParent() != _scene.WorldRoot ||
            _pushBlocks.GetParent() != _scene.WorldRoot)
        {
            throw new InvalidOperationException(
                "The main/gameplay PackedScene ownership boundary or world-node parentage regressed.");
        }

        if (_scene.Position != Vector2.Zero || _scene.WorldRoot.Position != Vector2.Zero ||
            _scene.InterfaceLayer.Layer != 10 ||
            _roomView.ZIndex != 0 || _player.ZIndex != 10 ||
            !_roomCamera.Enabled || _roomCamera.PositionSmoothingEnabled ||
            _hud.Position != new Vector2(0, 128) || _hud.ZIndex != 20 ||
            _warpFade.Size != roomViewportSize || _warpFade.ZIndex != 15 ||
            _warpFade.MouseFilter != Control.MouseFilterEnum.Ignore ||
            _warpFade.Color != new Color(1, 1, 1, 0) ||
            _scene.MenuFade.Size != screenSize || _scene.MenuFade.ZIndex != 50 ||
            _scene.MenuFade.MouseFilter != Control.MouseFilterEnum.Ignore ||
            _scene.MenuFade.Color != new Color(1, 1, 1, 0) ||
            _dialogue.Visible || _dialogue.ZIndex != 49 ||
            _mapScreen.Visible || _mapScreen.ZIndex != 40 ||
            _inventoryScreen.Visible || _inventoryScreen.ZIndex != 45 ||
            _saveQuitScreen.Visible || _saveQuitScreen.ZIndex != 46 ||
            _debugFlagScreen.Visible || _debugFlagScreen.ZIndex != 110 ||
            _roomDebug.Position != new Vector2(2, 0) || _roomDebug.ZIndex != 100 ||
            _roomDebug.MouseFilter != Control.MouseFilterEnum.Ignore ||
            _roomDebug.GetThemeFontSize("font_size") != 8 ||
            _roomDebug.GetThemeConstant("outline_size") != 1)
        {
            throw new InvalidOperationException(
                $"{GameSceneGraph.ScenePath} no longer preserves the fixed camera, UI, or draw-order values.");
        }

        GD.Print("Validated main/gameplay PackedScene ownership, unique typed bindings, " +
            "world-node containment, and fixed camera/UI presentation values.");
    }

    private void ValidateMenuLifecycleFoundation()
    {
        bool processEnabled = _player.IsProcessing();
        bool physicsEnabled = _player.IsPhysicsProcessing();
        bool debugVisible = _roomDebug.Visible;
        var fade = new ColorRect { Color = new Color(1, 1, 1, 0) };
        var pause = new GameplayPauseController(_player, _roomDebug);
        var lifecycle = new OracleMenuLifecycle(fade, pause);
        var inventory = new ValidationMenuClient("MENU_INVENTORY_VALIDATION");
        var map = new ValidationMenuClient("MENU_MAP_VALIDATION");

        if (!lifecycle.TryBeginOpening(inventory) ||
            lifecycle.TryBeginOpening(map) ||
            !pause.IsLeased || !pause.IsOwnedBy(inventory) ||
            _player.IsProcessing() || _player.IsPhysicsProcessing() || _roomDebug.Visible)
        {
            throw new InvalidOperationException(
                "The shared menu lifecycle did not acquire one exclusive gameplay pause lease.");
        }

        lifecycle.Update(inventory, 0.5 / 60.0);
        if (lifecycle.FadeUpdate != 0 || !Mathf.IsZeroApprox(fade.Color.A))
            throw new InvalidOperationException(
                "The fixed-update menu fade advanced on a fractional update.");
        lifecycle.Update(inventory, 0.5 / 60.0);
        if (lifecycle.FadeUpdate != 1 ||
            !Mathf.IsEqualApprox(fade.Color.A, 1.0f / OracleMenuLifecycle.FastFadeUpdates))
        {
            throw new InvalidOperationException(
                "The fixed-update menu fade did not consume two half-updates as one update.");
        }

        for (int update = 1; update < OracleMenuLifecycle.FastFadeUpdates - 1; update++)
            lifecycle.Update(inventory, 1.0 / 60.0);
        if (inventory.OpenAtWhiteCalls != 0 || lifecycle.FadeUpdate != 10 ||
            lifecycle.CurrentPhase != OracleMenuLifecycle.Phase.OpeningFadeOut)
        {
            throw new InvalidOperationException(
                "The common menu lifecycle swapped screens before fast fade update 11.");
        }
        lifecycle.Update(inventory, 1.0 / 60.0);
        if (inventory.OpenAtWhiteCalls != 1 || !Mathf.IsEqualApprox(fade.Color.A, 1.0f) ||
            lifecycle.CurrentPhase != OracleMenuLifecycle.Phase.OpeningFadeIn)
        {
            throw new InvalidOperationException(
                "The common menu lifecycle did not swap screens at full white on update 11.");
        }
        for (int update = 0; update < OracleMenuLifecycle.FastFadeUpdates; update++)
            lifecycle.Update(inventory, 1.0 / 60.0);
        if (!lifecycle.IsOpenFor(inventory) || inventory.OpenAtWhiteCalls != 1 ||
            !Mathf.IsZeroApprox(fade.Color.A) || !pause.IsLeased)
        {
            throw new InvalidOperationException(
                "The common menu lifecycle did not finish its 11-update fade-in while retaining ownership.");
        }

        lifecycle.BeginClosing(inventory);
        for (int update = 0; update < OracleMenuLifecycle.FastFadeUpdates; update++)
            lifecycle.Update(inventory, 1.0 / 60.0);
        if (inventory.CloseAtWhiteCalls != 1 ||
            lifecycle.CurrentPhase != OracleMenuLifecycle.Phase.ClosingFadeIn ||
            !pause.IsLeased)
        {
            throw new InvalidOperationException(
                "The common menu lifecycle did not remove its screen at closing full white.");
        }
        for (int update = 0; update < OracleMenuLifecycle.FastFadeUpdates; update++)
            lifecycle.Update(inventory, 1.0 / 60.0);
        if (lifecycle.IsActive || pause.IsLeased || inventory.ClosedCalls != 1 ||
            _player.IsProcessing() != processEnabled ||
            _player.IsPhysicsProcessing() != physicsEnabled ||
            _roomDebug.Visible != debugVisible)
        {
            throw new InvalidOperationException(
                "The shared menu lifecycle did not release ownership and restore its captured gameplay state.");
        }

        if (!lifecycle.TryBeginOpening(map))
            throw new InvalidOperationException("The shared menu lifecycle could not be reopened.");
        lifecycle.Update(map, (OracleMenuLifecycle.FastFadeUpdates * 2.0 + 1.0) / 60.0);
        if (!lifecycle.IsOpenFor(map) || map.OpenAtWhiteCalls != 1 ||
            !Mathf.IsZeroApprox(fade.Color.A))
        {
            throw new InvalidOperationException(
                "A long rendered frame did not execute both fixed 11-update menu fades exactly once.");
        }
        lifecycle.CloseImmediately(map);

        _player.SetProcess(false);
        _player.SetPhysicsProcess(true);
        _roomDebug.Visible = false;
        GameplayPauseController.PauseLease preservedLease = pause.TryAcquire(map) ??
            throw new InvalidOperationException("A released gameplay pause lease could not be reacquired.");
        preservedLease.Dispose();
        if (_player.IsProcessing() || !_player.IsPhysicsProcessing() || _roomDebug.Visible)
        {
            throw new InvalidOperationException(
                "A gameplay pause lease blindly enabled state instead of restoring its captured values.");
        }
        _player.SetProcess(processEnabled);
        _player.SetPhysicsProcess(physicsEnabled);
        _roomDebug.Visible = debugVisible;
        fade.Free();

        GD.Print("Validated one shared Oracle menu load state, exclusive pause ownership, " +
            "fractional fixed-update accumulation, exact 11-update white swap boundaries, " +
            "and captured-state restoration.");
    }

    private static void ValidateOracleObjectMath()
    {
        bool rejectedNonCardinal = false;
        try
        {
            OracleObjectMath.StrictCardinalVector(0x04);
        }
        catch (InvalidOperationException)
        {
            rejectedNonCardinal = true;
        }

        int airborneZ = 0;
        int airborneSpeedZ = -0x100;
        bool airborneLanded = OracleObjectMath.UpdateSpeedZ(
            ref airborneZ, ref airborneSpeedZ, 0x20);
        int landingZ = -0x10;
        int landingSpeedZ = 0x20;
        bool landed = OracleObjectMath.UpdateSpeedZ(
            ref landingZ, ref landingSpeedZ, 0x20);

        if (OracleObjectMath.ToPixelPosition(new Vector2(1.75f, -0.25f)) !=
                new Vector2(1, -1) ||
            !OracleObjectMath.VectorFromAngle32(0x00).IsEqualApprox(Vector2.Up) ||
            !OracleObjectMath.VectorFromAngle32(0x08).IsEqualApprox(Vector2.Right) ||
            OracleObjectMath.AngleToward(Vector2.Zero, Vector2.Up) != 0x00 ||
            OracleObjectMath.AngleToward(Vector2.Zero, Vector2.Right) != 0x08 ||
            OracleObjectMath.AngleToward(Vector2.Zero, Vector2.Down) != 0x10 ||
            OracleObjectMath.AngleToward(Vector2.Zero, Vector2.Left) != 0x18 ||
            OracleObjectMath.CardinalVector(0x0f) != Vector2.Right ||
            OracleObjectMath.StrictCardinalVector(0x18) != Vector2.Left ||
            airborneLanded || airborneZ != -0x100 || airborneSpeedZ != -0xe0 ||
            !landed || landingZ != 0 || landingSpeedZ != 0x20 ||
            !rejectedNonCardinal ||
            !OracleObjectMath.IsInsideOriginalScreenBoundary(new Vector2(-7, -7)) ||
            OracleObjectMath.IsInsideOriginalScreenBoundary(new Vector2(168, 0)) ||
            OracleObjectMath.IsInsideOriginalScreenBoundary(new Vector2(0, 136)))
        {
            throw new InvalidOperationException(
                "Shared original-object coordinate, angle, Z integration, or " +
                "screen-boundary math regressed.");
        }
        GD.Print("Validated shared 8.8 object-pixel flooring and Z integration, 32-step " +
            "angles, strict/masked cardinal decoding, and original screen boundaries.");
    }

    private void ValidateOracleRandom()
    {
        var firstParse = new OracleRandom();
        bool rejectedUnparsedPlacement = false;
        try
        {
            firstParse.NextPlacementValue();
        }
        catch (InvalidOperationException)
        {
            rejectedUnparsedPlacement = true;
        }
        firstParse.BeginRoomParse();
        byte[] firstPlacements = new byte[8];
        for (int index = 0; index < firstPlacements.Length; index++)
            firstPlacements[index] = firstParse.NextPlacementValue();
        OracleRandom.Result firstNext = firstParse.Next();

        var secondParse = new OracleRandom();
        secondParse.BeginRoomParse();
        for (int index = 0; index < 17; index++)
            secondParse.NextPlacementValue();
        secondParse.BeginRoomParse();
        byte[] secondPlacements = new byte[8];
        for (int index = 0; index < secondPlacements.Length; index++)
            secondPlacements[index] = secondParse.NextPlacementValue();
        OracleRandom.Result secondNext = secondParse.Next();

        if (!rejectedUnparsedPlacement ||
            !firstPlacements.SequenceEqual(new byte[]
                { 0x7d, 0xe4, 0xf0, 0x49, 0x98, 0xd7, 0x5c, 0xfe }) ||
            firstNext != new OracleRandom.Result(0xc6, 0x1a, 0x04) ||
            !secondPlacements.SequenceEqual(new byte[]
                { 0x07, 0x5d, 0x70, 0xde, 0xa8, 0x08, 0x6f, 0xb3 }) ||
            secondNext != new OracleRandom.Result(0x59, 0xd0, 0x9b))
        {
            throw new InvalidOperationException(
                "Room parsing did not rebuild the enemy-placement permutation, reset its " +
                "index, or consume the original 256 shared RNG values.");
        }

        OracleRoomData emptyRoom = _world.LoadRoom(0, 0x00);
        var validationRoot = new Node { Name = "OracleRandomValidation" };
        AddChild(validationRoot);

        var directRandom = new OracleRandom();
        var directManager = new RoomEntityManager(
            validationRoot, new NpcDatabase(), new EnemyDatabase(),
            new ItemDropDatabase(), new TimePortalDatabase(), directRandom);
        directManager.LoadRoom(0, emptyRoom);
        if (directRandom.Next() != new OracleRandom.Result(0xc6, 0x1a, 0x04))
        {
            throw new InvalidOperationException(
                "A direct room load did not generate exactly one enemy-placement buffer.");
        }
        directManager.Clear();

        var preloadRandom = new OracleRandom();
        var preloadManager = new RoomEntityManager(
            validationRoot, new NpcDatabase(), new EnemyDatabase(),
            new ItemDropDatabase(), new TimePortalDatabase(), preloadRandom);
        preloadManager.LoadRoom(0, emptyRoom);
        preloadManager.BeginScreenTransition(0, emptyRoom, Vector2.Zero);
        if (preloadRandom.Next() != new OracleRandom.Result(0x59, 0xd0, 0x9b))
        {
            throw new InvalidOperationException(
                "A scrolling destination preload did not generate the next placement buffer.");
        }
        preloadManager.Clear();

        var cutsceneRandom = new OracleRandom();
        var cutsceneManager = new RoomEntityManager(
            validationRoot, new NpcDatabase(), new EnemyDatabase(),
            new ItemDropDatabase(), new TimePortalDatabase(), cutsceneRandom);
        cutsceneManager.LoadCutsceneRoom(0, emptyRoom, includeTimePortals: false);
        if (cutsceneRandom.Next() != new OracleRandom.Result(0x5e, 0x27, 0xa5))
        {
            throw new InvalidOperationException(
                "A cutscene-only room load unexpectedly parsed ordinary room objects.");
        }
        cutsceneManager.Clear();
        RemoveChild(validationRoot);
        validationRoot.Free();

        GD.Print("Validated shared getRandomNumber state, per-parse 256-call placement-buffer " +
            "generation, placement-index reset, direct loads, destination preloads, and " +
            "cutscene-only load exclusion.");
    }

    private static void ValidateRoomTileChanges()
    {
        OracleSaveData save = OracleSaveData.CreateStandardGame();
        long animationTick = 0;
        var rooms = new RoomSession(
            0, 0x3a,
            () => animationTick,
            () => animationTick = 0,
            save);
        var changes = new RoomTileChangeDatabase();
        var warps = new WarpDatabase();
        static Vector2 Point(int position) => new(
            (position & 0x0f) * OracleRoomData.MetatileSize + 8,
            (position >> 4) * OracleRoomData.MetatileSize + 8);

        Vector2 doorPoint = Point(0x23);
        OracleRoomData room = rooms.CurrentRoom;

        if (changes.RuleCount != 44 || changes.RoomCount != 35 ||
            room.GetPackedPosition(doorPoint) != 0x23 ||
            room.GetOriginalMetatile(doorPoint) != 0xa7 ||
            room.GetMetatile(doorPoint) != 0xa7 || !room.IsSolid(doorPoint) ||
            warps.TryGetTileWarp(0, 0x3a, 0x23, room.GetMetatile(doorPoint), out _))
        {
            throw new InvalidOperationException(
                "Room 0:3a did not begin with Nayru's house door closed at $23/$a7.");
        }

        save.SetGlobalFlag(OracleSaveData.GlobalFlagIntroDone);
        room = rooms.Load(0, 0x3a);
        if (room.GetMetatile(doorPoint) != 0xee || room.IsSolid(doorPoint) ||
            !warps.TryGetTileWarp(0, 0x3a, 0x23, 0xee, out WarpDatabase.Warp warp) ||
            warp.DestinationGroup != 3 || warp.DestinationRoom != 0x9e)
        {
            throw new InvalidOperationException(
                "GLOBALFLAG_INTRO_DONE $0a did not open room 0:3a's $23/$ee door to 3:9e.");
        }

        save.SetGlobalFlag(OracleSaveData.GlobalFlagIntroDone, value: false);
        room = rooms.Load(0, 0x3a);
        if (room.GetMetatile(doorPoint) != 0xa7 || !room.IsSolid(doorPoint))
        {
            throw new InvalidOperationException(
                "Reloading room 0:3a without GLOBALFLAG_INTRO_DONE did not restore door $23/$a7.");
        }

        // Current-room set/clear conditions and arbitrary direct writes.
        room = rooms.Load(0, 0x73);
        byte forestOriginal = room.GetOriginalMetatile(Point(0x73));
        save.SetRoomFlag(0, 0x73, OracleSaveData.RoomFlag80);
        room = rooms.Load(0, 0x73);
        if (room.GetMetatile(Point(0x73)) != 0x3a ||
            room.GetMetatile(Point(0x74)) != 0x10 ||
            room.GetMetatile(Point(0x77)) != 0x3a)
        {
            throw new InvalidOperationException(
                "Room 0:73 flag $80 did not apply its five imported rubble writes.");
        }
        save.SetRoomFlag(0, 0x73, OracleSaveData.RoomFlag80, value: false);
        room = rooms.Load(0, 0x73);
        if (room.GetMetatile(Point(0x73)) != forestOriginal)
            throw new InvalidOperationException("Room 0:73 did not restore its original rubble layout.");

        room = rooms.Load(0, 0xac);
        byte treeOriginal = room.GetOriginalMetatile(Point(0x33));
        if (room.GetMetatile(Point(0x33)) != 0xaf || room.GetMetatile(Point(0x44)) != 0xaf)
            throw new InvalidOperationException(
                "Room 0:ac's clear flag-$80 branch did not remove the scent tree.");
        save.SetRoomFlag(0, 0xac, OracleSaveData.RoomFlag80);
        room = rooms.Load(0, 0xac);
        if (room.GetMetatile(Point(0x33)) != treeOriginal)
            throw new InvalidOperationException(
                "Room 0:ac flag $80 did not retain the original planted-tree layout.");

        // Explicit other-room flags.
        room = rooms.Load(0, 0x0b);
        byte caveOriginal = room.GetOriginalMetatile(Point(0x43));
        if (room.GetMetatile(Point(0x43)) != caveOriginal)
            throw new InvalidOperationException("Room 0:0b opened before room 0:0a flag $40 was set.");
        save.SetRoomFlag(0, 0x0a, OracleSaveData.RoomFlag40);
        room = rooms.Load(0, 0x0b);
        if (room.GetMetatile(Point(0x43)) != 0xdd)
            throw new InvalidOperationException(
                "Room 0:0a flag $40 did not open room 0:0b's imported cave entrance.");

        // Essence conditions.
        room = rooms.Load(5, 0xb9);
        byte elderOriginal = room.GetOriginalMetatile(Point(0x41));
        if (room.GetMetatile(Point(0x41)) != elderOriginal)
            throw new InvalidOperationException("Room 5:b9 changed before essence bit 3 was set.");
        save.WriteWramByte(0xc6bf, (byte)(1 << 3));
        room = rooms.Load(5, 0xb9);
        if (room.GetMetatile(Point(0x41)) != 0xa1 ||
            room.GetMetatile(Point(0x44)) != 0xef ||
            room.GetMetatile(Point(0x55)) != 0xa2)
        {
            throw new InvalidOperationException(
                "Essence bit 3 did not apply room 5:b9's two imported boulder rows.");
        }

        // Draw rectangles preserve the original height/width ordering.
        room = rooms.Load(2, 0x90);
        save.SetRoomFlag(2, 0x90, 0x02);
        room = rooms.Load(2, 0x90);
        if (room.GetMetatile(Point(0x42)) != 0xdd ||
            room.GetMetatile(Point(0x47)) != 0xef ||
            room.GetMetatile(Point(0x52)) != 0xb9 ||
            room.GetMetatile(Point(0x57)) != 0xbe)
        {
            throw new InvalidOperationException(
                "Room 2:90 flag $02 did not draw its imported 2x6 Jabu entrance rectangle.");
        }

        // Full-layout copies and ANDed global/current-room conditions.
        save.SetGlobalFlag(0x0f); // GLOBALFLAG_D3_CRYSTALS
        OracleRoomData sourceRoom = rooms.World.LoadRoom(4, 0x60);
        room = rooms.Load(4, 0x52);
        for (int y = 0; y < room.HeightInTiles; y++)
        for (int x = 0; x < room.WidthInTiles; x++)
        {
            Vector2 point = new(
                x * OracleRoomData.MetatileSize + 8,
                y * OracleRoomData.MetatileSize + 8);
            if (room.GetMetatile(point) != sourceRoom.GetOriginalMetatile(point))
            {
                throw new InvalidOperationException(
                    "GLOBALFLAG_D3_CRYSTALS did not copy room 4:60's original layout into 4:52.");
            }
        }

        room = rooms.Load(4, 0x60);
        if (room.GetMetatile(Point(0x57)) != 0xf1)
            throw new InvalidOperationException(
                "Room 4:60 did not create its closed chest for clear room item flag $20.");
        save.SetRoomFlag(4, 0x60, OracleSaveData.RoomFlagItem);
        room = rooms.Load(4, 0x60);
        if (room.GetMetatile(Point(0x57)) != 0xf0)
            throw new InvalidOperationException(
                "Room 4:60 item flag $20 did not select the opened chest under the D3 global flag.");

        GD.Print("Validated 44 imported rules for 35 room-specific tile changers: " +
            "global/current/specific-room/essence/WRAM conditions, set/fill/draw/replace/copy " +
            "operations, and room 0:3a's closed-to-open Nayru-house warp.");
    }

    private static void ValidateRoomEventTimeline()
    {
        var timeline = new RoomEventTimeline<ValidationTimelineStep>();
        timeline.Enqueue(new ValidationTimelineStep(2));
        timeline.Enqueue(new ValidationTimelineStep(0));
        var observedCounters = new List<int>();
        bool Update(ValidationTimelineStep step)
        {
            observedCounters.Add(step.Counter);
            return --step.Counter == 0;
        }

        if (!timeline.AdvanceFrame(Update) ||
            !timeline.AdvanceFrame(Update) ||
            !timeline.AdvanceFrame(Update) ||
            timeline.AdvanceFrame(Update) ||
            !observedCounters.SequenceEqual(new[] { 2, 1, 1 }))
        {
            throw new InvalidOperationException(
                "Room-event timeline duration clamping or one-step update cadence regressed.");
        }

        timeline.Enqueue(new ValidationTimelineStep(3));
        timeline.AdvanceFrame(Update);
        timeline.Clear();
        if (timeline.AdvanceFrame(Update))
            throw new InvalidOperationException("Room-event timeline clear retained active work.");

        static CutsceneCommandSource CommandSource(
            string script,
            int commandIndex,
            string opcode) =>
            new(script, $"{script}Label", commandIndex, 100 + commandIndex, opcode);

        var callHost = new ValidationImpaPostPushHost(linkAngle: 0x18);
        var callRunner = new CutsceneCommandRunner(callHost);
        CutsceneCommand[] callCommands =
        [
            new CutsceneCallCommand(CommandSource("callValidation", 0, "callscript"), 3),
            new CutsceneWriteMemoryCommand(
                CommandSource("callValidation", 1, "writememory"),
                "wTmpcfc0.genericCutscene.cfd0",
                0x08),
            new CutsceneEndCommand(CommandSource("callValidation", 2, "scriptend")),
            new CutsceneWriteMemoryCommand(
                CommandSource("callValidation", 3, "writememory"),
                "wTmpcfc0.genericCutscene.cfd0",
                0x07),
            new CutsceneReturnCommand(CommandSource("callValidation", 4, "retscript"))
        ];
        callRunner.Start(callCommands);
        callHost.AdvanceValidationFrame();
        callRunner.AdvanceFrame();
        if (callHost.Signal != 0x06 || callRunner.Instruction != 3)
            throw new InvalidOperationException("Cutscene callscript did not yield at its target boundary.");
        callHost.AdvanceValidationFrame();
        callRunner.AdvanceFrame();
        if (callHost.Signal != 0x07 || callRunner.Instruction != 1)
            throw new InvalidOperationException("Cutscene retscript did not yield at its return boundary.");
        callHost.AdvanceValidationFrame();
        callRunner.AdvanceFrame();
        int[] callOrder = callHost.Trace.Entries
            .Where(entry => entry.Phase == CutsceneCommandTracePhase.Started)
            .Select(entry => entry.Source.CommandIndex)
            .ToArray();
        if (callRunner.Active || !callHost.Ended || callHost.Signal != 0x08 ||
            !callOrder.SequenceEqual(new[] { 0, 3, 4, 1, 2 }) ||
            !callHost.Trace.Entries.Any(entry =>
                entry.Source.CommandIndex == 0 &&
                entry.Phase == CutsceneCommandTracePhase.Completed &&
                entry.NextCommandIndex == 3) ||
            !callHost.Trace.Entries.Any(entry =>
                entry.Source.CommandIndex == 4 &&
                entry.Phase == CutsceneCommandTracePhase.Completed &&
                entry.NextCommandIndex == 1))
        {
            throw new InvalidOperationException(
                "Cutscene branch/call stack execution or trace targets regressed.");
        }

        var laneHost = new ValidationImpaPostPushHost(linkAngle: 0x18);
        var scheduler = new CutsceneCommandLaneScheduler(laneHost);
        scheduler.StartLane(
            "laneA",
            [
                new CutsceneSetAnimationCommand(
                    CommandSource("laneA", 0, "setanimation"), "Impa", 0, ""),
                new CutsceneWaitFramesCommand(
                    CommandSource("laneA", 1, "waitframes"), 3),
                new CutsceneEndCommand(CommandSource("laneA", 2, "scriptend"))
            ]);
        scheduler.StartLane(
            "laneB",
            [
                new CutsceneSetAnimationCommand(
                    CommandSource("laneB", 0, "setanimation"), "Impa", 1, ""),
                new CutsceneWaitFramesCommand(
                    CommandSource("laneB", 1, "waitframes"), 2),
                new CutsceneEndCommand(CommandSource("laneB", 2, "scriptend"))
            ]);
        for (int frame = 0; frame < 5; frame++)
        {
            laneHost.AdvanceValidationFrame();
            scheduler.AdvanceFrame();
        }
        string[] laneStartOrder = laneHost.Trace.Entries
            .Where(entry => entry.Phase == CutsceneCommandTracePhase.Started)
            .Select(entry => $"{entry.Source.Script}:{entry.Source.CommandIndex}")
            .ToArray();
        if (scheduler.Active || scheduler.Count != 2 ||
            !laneStartOrder.SequenceEqual(
                new[] { "laneA:0", "laneB:0", "laneA:1", "laneB:1", "laneB:2", "laneA:2" }))
        {
            throw new InvalidOperationException(
                "Parallel cutscene lanes lost independent counters or stable insertion order.");
        }
        scheduler.Clear();
        if (scheduler.Active || scheduler.Count != 0)
            throw new InvalidOperationException("Parallel cutscene lane clear retained active work.");

        try
        {
            callRunner.Start(
            [
                new CutsceneSetAnimationCommand(
                    new CutsceneCommandSource(
                        "bindingValidation", "missingActor", 0, 321, "setanimation"),
                    "Nayru",
                    0,
                    ""),
                new CutsceneEndCommand(
                    new CutsceneCommandSource(
                        "bindingValidation", "missingActor", 1, 322, "scriptend"))
            ]);
            throw new InvalidOperationException(
                "Cutscene runner accepted an unregistered typed actor binding.");
        }
        catch (InvalidOperationException exception) when (
            exception.Message.Contains("Nayru", StringComparison.Ordinal) &&
            exception.Message.Contains("missingActor", StringComparison.Ordinal) &&
            exception.Message.Contains("[0]", StringComparison.Ordinal) &&
            exception.Message.Contains("line 321", StringComparison.Ordinal))
        {
        }

        var sequence = new RoomEventTimeline();
        var observedSequence = new List<string>();
        bool gateOpen = false;
        sequence.Wait(
            2,
            counterChanged: remaining => observedSequence.Add($"wait:{remaining}"),
            elapsed: () => observedSequence.Add("elapsed"));
        sequence.WaitUntil(
            () => gateOpen,
            completed: () => observedSequence.Add("gate"));
        sequence.Yield();
        sequence.Do(() => observedSequence.Add("action"));

        if (!sequence.Active ||
            !sequence.AdvanceFrame() ||
            !sequence.AdvanceFrame() ||
            !sequence.AdvanceFrame())
        {
            throw new InvalidOperationException(
                "Finite room-event sequence did not retain queued or gated work.");
        }

        gateOpen = true;
        if (!sequence.AdvanceFrame() ||
            !sequence.AdvanceFrame() ||
            !sequence.Active ||
            !sequence.AdvanceFrame() ||
            sequence.Active ||
            sequence.AdvanceFrame() ||
            !observedSequence.SequenceEqual(
                new[] { "wait:1", "wait:0", "elapsed", "gate", "action" }))
        {
            throw new InvalidOperationException(
                "Finite room-event wait, gate, action, or command cadence regressed.");
        }

        GD.Print("Validated shared room-event timeline duration clamping, command boundaries, " +
            "finite wait/gate/action sequences, typed actor diagnostics, call stacks, " +
            "parallel lane ordering, independent counters, and lifecycle clearing.");
    }

    private void ValidateSoundEngine()
    {
        var data = new OracleSoundData();
        OracleSoundData.ChannelStart[] title = data.ChannelsFor(
            OracleSoundEngine.MusTitlescreen).ToArray();
        OracleSoundData.ChannelStart[] getItem = data.ChannelsFor(0x4c).ToArray();
        OracleSoundData.ChannelStart[] makuDisappear = data.ChannelsFor(
            OracleSoundEngine.SndMakuDisappear).ToArray();
        if (title.Length != 4 ||
            !title.Select(channel => channel.Channel).SequenceEqual(new[] { 0, 1, 4, 6 }) ||
            title.Any(channel => channel.Priority != 1 || channel.Bank != 0x3a) ||
            getItem.Length != 4 ||
            !getItem.Select(channel => channel.Channel).SequenceEqual(new[] { 2, 3, 5, 7 }) ||
            getItem.Any(channel => channel.Priority != 8 || channel.Bank != 0x3b) ||
            makuDisappear.Length != 2 ||
            !makuDisappear.Select(channel => channel.Channel).SequenceEqual(new[] { 2, 7 }) ||
            makuDisappear.Any(channel => channel.Priority != 1 || channel.Bank != 0x39) ||
            data.FrequencyRegister(0x0c) != 0x002d ||
            data.FrequencyRegister(0x26) != 0x0642 ||
            data.FrequencyRegisterByIndex(0x16) != 0x05ce ||
            data.EnvelopeAttackFrames(8, 1) != 8 ||
            data.EnvelopeAttackFrames(8, 2) != 17 ||
            !Enumerable.Range(0, 8).Select(data.VibratoOffset)
                .SequenceEqual(new[] { 0, 1, 2, 1, 0, -1, -2, -1 }) ||
            data.RoomMusic(0, 0x11) != 0x03 ||
            data.RoomMusic(0, 0x38) != 0x1e ||
            data.RoomMusic(0, 0x49) != OracleSoundEngine.MusOverworld ||
            data.RoomMusic(1, 0x11) != 0x04 ||
            !data.TryGetNoise(0x24, out OracleSoundData.NoiseRecord noise) ||
            noise.Envelope != 0x01 || noise.Frequency != 0x47 ||
            data.WaveSample(0x0e, 0) >= data.WaveSample(0x0e, 16))
        {
            throw new InvalidOperationException(
                "Imported sound pointers, frequencies, room assignments, waveforms, or noise table diverged.");
        }

        float pulseFrequency = OracleSoundEngine.ToneFrequencyForValidation(0, 0x05ce);
        float waveFrequency = OracleSoundEngine.ToneFrequencyForValidation(4, 0x05ce);
        if (!Mathf.IsEqualApprox(pulseFrequency, 233.2242f) ||
            !Mathf.IsEqualApprox(waveFrequency, 116.6121f) ||
            !Mathf.IsEqualApprox(pulseFrequency, waveFrequency * 2) ||
            !Mathf.IsEqualApprox(OracleSoundEngine.NoiseClockForValidation(0x14), 32768.0f) ||
            !Mathf.IsEqualApprox(OracleSoundEngine.NoiseClockForValidation(0x75), 409.6f) ||
            OracleSoundEngine.NoiseClockForValidation(0xe1) != 0 ||
            OracleSoundEngine.CgbHighPassFactorForValidation is < 0.904 or > 0.905)
        {
            throw new InvalidOperationException(
                "GBC pulse/wave/noise clocks or CGB high-pass coefficient diverged.");
        }

        var sound = new OracleSoundEngine(data, enableOutput: false);
        sound.PlaySound(OracleSoundEngine.MusTitlescreen);
        sound.Tick();
        OracleSoundEngine.ChannelState square1 = sound.Channel(0);
        OracleSoundEngine.ChannelState square2 = sound.Channel(1);
        OracleSoundEngine.ChannelState wave = sound.Channel(4);
        if (sound.ActiveMusic != OracleSoundEngine.MusTitlescreen ||
            !square1.Active || square1.DutyOrWaveform != 2 || square1.Volume != 8 ||
            square1.CurrentFrequencyRegister != 0x0642 || square1.WaitFrames != 0x17 ||
            !square2.Active || square2.CurrentFrequencyRegister != 0x06e7 ||
            square2.WaitFrames != 0x17 || !wave.Active || wave.Gate ||
            wave.WaitFrames != 0x23 || sound.Channel(6).Active)
        {
            throw new InvalidOperationException(
                "MUS_TITLESCREEN did not execute its original first square/wave/noise commands.");
        }


        // Channel 0's first $18 note, second $14 note, and following $10
        // rest consume 45 sound updates including their command updates.
        for (int update = 0; update < 44; update++)
            sound.Tick();
        if (!square1.Active || !square1.Gate || square1.WaitFrames != 0x0f ||
            square1.OutputVolume != 2 || square1.EnvelopePeriod != 1 ||
            square1.EnvelopeDirection != -1)
        {
            throw new InvalidOperationException(
                "MUS_TITLESCREEN channel 0 rest did not install its period-1 square release.");
        }

        sound.PlaySound(OracleSoundEngine.SndMenuMove);
        sound.Tick();
        OracleSoundEngine.ChannelState sfxSquare = sound.Channel(2);
        if (!sfxSquare.Active || sfxSquare.Priority != 1 ||
            sfxSquare.DutyOrWaveform != 3 || !sfxSquare.RawFrequencyMode ||
            sfxSquare.RawEnvelope != 0xd9 ||
            sfxSquare.CurrentFrequencyRegister != 0x07a0 ||
            sfxSquare.WaitFrames != 2)
        {
            throw new InvalidOperationException(
                "SND_MENU_MOVE did not execute its raw-frequency $07a0/$03 command.");
        }

        sound.PlaySound(OracleSoundEngine.SndSwordSlash);
        sound.Tick();
        OracleSoundEngine.ChannelState rawNoise = sound.Channel(7);
        if (!rawNoise.Active || !rawNoise.Gate || rawNoise.Priority != 1 ||
            rawNoise.RawEnvelope != 0x20 || rawNoise.OutputVolume != 2 ||
            rawNoise.EnvelopePeriod != 0 || rawNoise.NoiseRegister != 0x47 ||
            rawNoise.NoiseTriggerPending || rawNoise.WaitFrames != 0)
        {
            throw new InvalidOperationException(
                "SND_SWORDSLASH did not retrigger CH4 from its raw NR42/NR43 pair.");
        }

        sound.PlaySound(OracleSoundEngine.SndMakuDisappear);
        sound.Tick();
        OracleSoundEngine.ChannelState makuPulse = sound.Channel(2);
        OracleSoundEngine.ChannelState makuNoise = sound.Channel(7);
        if (!makuPulse.Active || makuPulse.Priority != 1 ||
            makuPulse.DutyOrWaveform != 2 || makuPulse.Volume != 3 ||
            makuPulse.OutputVolume != 3 ||
            makuPulse.CurrentFrequencyRegister != 0x002d ||
            makuPulse.WaitFrames != 0x1b ||
            !makuNoise.Active || !makuNoise.Gate || makuNoise.Priority != 1 ||
            makuNoise.RawEnvelope != 0xf0 || makuNoise.OutputVolume != 15 ||
            makuNoise.EnvelopePeriod != 0 || makuNoise.NoiseRegister != 0x75 ||
            makuNoise.NoiseTriggerPending || makuNoise.WaitFrames != 0x1b)
        {
            throw new InvalidOperationException(
                "SND_MAKUDISAPPEAR did not start its low C2 pulse and raw $f0/$75 CH4 block.");
        }

        sound.PlaySound(0x4c);
        sound.Tick();
        int protectedOffset = sound.Channel(2).Offset;
        if (sound.Channel(2).Priority != 8 || sound.Channel(3).Priority != 8 ||
            sound.Channel(5).Priority != 8 || sound.Channel(7).Priority != 8)
        {
            throw new InvalidOperationException(
                "SND_GETITEM did not claim all four SFX channels at priority 8.");
        }
        sound.PlaySound(OracleSoundEngine.SndMenuMove);
        if (sound.Channel(2).Priority != 8 || sound.Channel(2).Offset != protectedOffset)
            throw new InvalidOperationException(
                "Low-priority SND_MENU_MOVE replaced SND_GETITEM's square channel.");

        sound.PlaySound(OracleSoundEngine.SndCtrlStopSfx);
        if (new[] { 2, 3, 5, 7 }.Any(channel => sound.Channel(channel).Active))
            throw new InvalidOperationException("SNDCTRL_STOPSFX did not release all SFX channels.");
        sound.PlaySound(OracleSoundEngine.SndCtrlStopMusic);
        sound.Tick();
        if (sound.ActiveMusic != 0 || new[] { 0, 1, 4, 6 }.Any(channel => sound.Channel(channel).Active))
            throw new InvalidOperationException("SNDCTRL_STOPMUSIC did not run sound $de's stop channels.");

        var outputSound = new OracleSoundEngine(
            data, enableOutput: true, allowHeadlessOutput: true);
        AddChild(outputSound);
        try
        {
            if (!outputSound.OutputResourcesActiveForValidation)
                throw new InvalidOperationException("The output sound engine did not create its stream playback.");
            RemoveChild(outputSound);
            if (outputSound.OutputResourcesActiveForValidation)
            {
                throw new InvalidOperationException(
                    "The output sound engine retained its player or stream playback after _ExitTree.");
            }
        }
        finally
        {
            if (GodotObject.IsInstanceValid(outputSound))
            {
                if (outputSound.GetParent() == this)
                    RemoveChild(outputSound);
                outputSound.Free();
            }
        }

        sound.Free();
        GD.Print("Validated all 223 original sound pointers, room music assignments, " +
            "frequency/wave/noise clocks, envelope/vibrato tables, CGB filtering, " +
            "title square releases, raw square/noise SFX including SND_MAKUDISAPPEAR, " +
            "channel priority, stop controls, and output teardown.");
    }

    private static void ValidateGraphicsCache()
    {
        string[] pngPaths = EnumeratePngPaths("res://assets/oracle")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        if (pngPaths.Length < 200)
        {
            throw new InvalidOperationException(
                $"Expected the complete generated PNG set, found only {pngPaths.Length} files.");
        }

        foreach (string path in pngPaths)
        {
            Image resourceImage = OracleGraphicsCache.LoadImage(path);
            using Image rawImage = OracleGraphicsCache.LoadRawPngForValidation(path);
            if (resourceImage.GetWidth() != rawImage.GetWidth() ||
                resourceImage.GetHeight() != rawImage.GetHeight() ||
                resourceImage.GetFormat() != rawImage.GetFormat() ||
                !resourceImage.GetData().AsSpan().SequenceEqual(rawImage.GetData()))
            {
                throw new InvalidOperationException(
                    $"ResourceLoader changed imported PNG pixels for {path}.");
            }
        }

        const string sourcePath = "res://assets/oracle/gfx/spr_impa.png";
        int loadsBefore = OracleGraphicsCache.SourceLoadCount;
        Image source = OracleGraphicsCache.LoadImage(sourcePath);
        int loadsAfterFirst = OracleGraphicsCache.SourceLoadCount;
        Image sameSource = OracleGraphicsCache.LoadImage(sourcePath);
        if (!ReferenceEquals(source, sameSource) ||
            OracleGraphicsCache.SourceLoadCount != loadsAfterFirst ||
            loadsAfterFirst - loadsBefore is < 0 or > 1)
        {
            throw new InvalidOperationException(
                "Repeated graphics access did not return one cached CPU source image.");
        }

        const string extraPath = "res://assets/oracle/gfx/spr_common_sprites.png";
        Image composite = OracleGraphicsCache.AppendGraphics(source, extraPath);
        Image sameComposite = OracleGraphicsCache.AppendGraphics(source, extraPath);
        int expectedExtraX = Mathf.CeilToInt(source.GetWidth() / 128.0f) * 128;
        Image extra = OracleGraphicsCache.LoadImage(extraPath);
        if (!ReferenceEquals(composite, sameComposite) ||
            composite.GetWidth() != expectedExtraX + extra.GetWidth() ||
            composite.GetHeight() != Math.Max(source.GetHeight(), extra.GetHeight()))
        {
            throw new InvalidOperationException(
                "Chained graphics did not preserve `$20-tile slot alignment or cache identity.");
        }

        const string encodedOam = "8,0,0,0;8,8,2,32";
        string encodedAnimation = $"2@{encodedOam}|4@{encodedOam}~1";
        OracleGraphicsCache.AnimationDefinition animation =
            OracleGraphicsCache.GetAnimationDefinition(encodedAnimation);
        OracleGraphicsCache.AnimationDefinition sameAnimation =
            OracleGraphicsCache.GetAnimationDefinition(encodedAnimation);
        if (!ReferenceEquals(animation, sameAnimation) ||
            animation.LoopStart != 1 || animation.Frames.Length != 2 ||
            animation.Frames[0].Duration != 2 || animation.Frames[1].Duration != 4 ||
            animation.Frames.Any(frame => frame.EncodedOam != encodedOam))
        {
            throw new InvalidOperationException(
                "Encoded animation definitions were not parsed and cached immutably.");
        }

        int buildsBefore = OracleGraphicsCache.OamBuildCount;
        Texture2D cached = NpcCharacter.BuildOamTexture(source, encodedOam, 0, 1);
        int buildsAfterFirst = OracleGraphicsCache.OamBuildCount;
        int hitsAfterFirst = OracleGraphicsCache.OamCacheHitCount;
        Texture2D sameCached = NpcCharacter.BuildOamTexture(source, encodedOam, 0, 1);
        if (!ReferenceEquals(cached, sameCached) ||
            OracleGraphicsCache.OamBuildCount != buildsAfterFirst ||
            OracleGraphicsCache.OamCacheHitCount != hitsAfterFirst + 1 ||
            buildsAfterFirst - buildsBefore is < 0 or > 1)
        {
            throw new InvalidOperationException(
                "An identical OAM frame was rebuilt instead of reused.");
        }

        using Texture2D uncached = NpcCharacter.BuildOamTextureUncachedForValidation(
            source, encodedOam, 0, 1);
        using Image cachedImage = cached.GetImage();
        using Image uncachedImage = uncached.GetImage();
        if (cachedImage.GetWidth() != uncachedImage.GetWidth() ||
            cachedImage.GetHeight() != uncachedImage.GetHeight() ||
            !cachedImage.GetData().AsSpan().SequenceEqual(uncachedImage.GetData()))
        {
            throw new InvalidOperationException(
                "Cached fixed OAM composition differs from the original compositor.");
        }

        (Texture2D positioned, Vector2 positionedOffset) =
            NpcCharacter.BuildPositionedOamTexture(
                source, encodedOam, 0, 1, null, true);
        (Texture2D uncachedPositioned, Vector2 uncachedPositionedOffset) =
            NpcCharacter.BuildPositionedOamTextureUncachedForValidation(
                source, encodedOam, 0, 1, null, true);
        using (uncachedPositioned)
        using (Image positionedImage = positioned.GetImage())
        using (Image uncachedPositionedImage = uncachedPositioned.GetImage())
        {
            if (positionedOffset != uncachedPositionedOffset ||
                positionedImage.GetWidth() != uncachedPositionedImage.GetWidth() ||
                positionedImage.GetHeight() != uncachedPositionedImage.GetHeight() ||
                !positionedImage.GetData().AsSpan().SequenceEqual(
                    uncachedPositionedImage.GetData()))
            {
                throw new InvalidOperationException(
                    "Cached positioned OAM composition differs from the original compositor.");
            }
        }

        Color[] overridePalette =
        {
            Colors.Transparent,
            Color.Color8(0x11, 0x22, 0x33),
            Color.Color8(0x44, 0x55, 0x66),
            Color.Color8(0x77, 0x88, 0x99)
        };
        Texture2D paletteVariant = NpcCharacter.BuildOamTexture(
            source, encodedOam, 0, 1, overridePalette, true);
        Texture2D inversionVariant = NpcCharacter.BuildOamTexture(
            source, encodedOam, 0, 1, null, false);
        if (ReferenceEquals(cached, positioned) ||
            ReferenceEquals(cached, paletteVariant) ||
            ReferenceEquals(cached, inversionVariant))
        {
            throw new InvalidOperationException(
                "OAM cache keys collapsed composition, palette, or grayscale variants.");
        }

        NpcDatabase.NpcRecord npcRecord = new NpcDatabase().GetRoomNpcs(0, 0x66).First();
        var firstNpc = new NpcCharacter();
        var secondNpc = new NpcCharacter();
        try
        {
            firstNpc.Initialize(npcRecord);
            int buildsAfterFirstNpc = OracleGraphicsCache.OamBuildCount;
            secondNpc.Initialize(npcRecord);
            if (OracleGraphicsCache.OamBuildCount != buildsAfterFirstNpc)
            {
                throw new InvalidOperationException(
                    "A second NPC instance rebuilt shared facing OAM frames.");
            }

            firstNpc.SetScriptAnimation(npcRecord.DownAnimation);
            int buildsAfterFirstScriptSelection = OracleGraphicsCache.OamBuildCount;
            firstNpc.SetScriptAnimation(npcRecord.DownAnimation);
            if (OracleGraphicsCache.OamBuildCount != buildsAfterFirstScriptSelection)
            {
                throw new InvalidOperationException(
                    "Re-selecting a scripted NPC animation rebuilt its OAM textures.");
            }
        }
        finally
        {
            firstNpc.Free();
            secondNpc.Free();
        }

        GD.Print($"Validated ResourceLoader pixel parity for {pngPaths.Length} generated PNGs, " +
            "immutable source/composite reuse, `$20-tile chain alignment, complete OAM cache keys, " +
            "cross-instance/scripted-animation reuse, and byte-identical fixed/positioned composition.");
    }

    private static IEnumerable<string> EnumeratePngPaths(string directory)
    {
        foreach (string file in DirAccess.GetFilesAt(directory))
        {
            if (file.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                yield return $"{directory}/{file}";
        }
        foreach (string child in DirAccess.GetDirectoriesAt(directory))
        foreach (string path in EnumeratePngPaths($"{directory}/{child}"))
            yield return path;
    }

    private void ValidateSaveAndQuitToTitle()
    {
        GameSceneGraph gameplayScene = _scene;
        bool quitAfterFailure = false;
        var failedMenu = new InventoryMenuController(
            _inventoryScreen,
            _saveQuitScreen,
            _menuLifecycle,
            () => true,
            () => OracleSaveStore.SaveResult.Failed("validation failure"),
            () => quitAfterFailure = true);
        failedMenu.OpenSaveImmediatelyForValidation();
        _saveQuitScreen.Move(1);
        _saveQuitScreen.Move(1);
        failedMenu.SelectSaveOptionForValidation();
        for (int frame = 0; frame < InventoryMenuController.SaveSelectionDelayFrames; frame++)
            failedMenu.Update(1.0 / 60.0);
        if (!_saveQuitScreen.SaveErrorVisible ||
            failedMenu.LastSaveError != "validation failure" ||
            !failedMenu.SaveMenuOpen || quitAfterFailure)
        {
            throw new InvalidOperationException(
                "A failed Save and Quit did not remain open with a surfaced retryable error.");
        }
        failedMenu.SelectSaveOptionForValidation();
        if (_saveQuitScreen.SaveErrorVisible || !failedMenu.SaveMenuOpen)
            throw new InvalidOperationException("The Save and Quit failure could not be dismissed for retry.");
        failedMenu.CloseImmediatelyForValidation();

        _inventoryMenu.OpenSaveImmediatelyForValidation();
        _saveQuitScreen.Move(1);
        _saveQuitScreen.Move(1);
        int saveRequests = _inventoryMenu.SaveRequests;
        int saveWrites = _saveWriteRequests;
        _inventoryMenu.SelectSaveOptionForValidation();
        for (int frame = 0; frame < InventoryMenuController.SaveSelectionDelayFrames; frame++)
            _inventoryMenu.Update(1.0 / 60.0);
        if (_inventoryMenu.SaveRequests != saveRequests + 1 ||
            _saveWriteRequests != saveWrites + 1 ||
            _inventoryMenu.QuitRequests != 1 || _mainMenu is null || _mainMenuScreen is null ||
            _inventoryMenu.IsActive || !gameplayScene.IsQueuedForDeletion() ||
            _sound.IsQueuedForDeletion() || _mainMenuScreen.GetParent() != this)
        {
            throw new InvalidOperationException(
                "Save and Quit did not save, free the gameplay scene as one lifecycle unit, " +
                "preserve application audio, and return to title/file select after 30 updates.");
        }
        GD.Print("Validated retryable Save and Quit failure handling, successful persistence " +
            "request, one-root gameplay cleanup, persistent application audio, and return to " +
            "title after 30 updates.");
    }

    private void ValidateNewGameIntro()
    {
        var screen = new NewGameIntroScreen { Name = "NewGameIntroValidation" };
        AddChild(screen);
        int completionRequests = 0;
        var intro = new NewGameIntroController(
            screen, () => completionRequests++, _sound);
        NewGameIntroDatabase.NewGameIntroRecord record = intro.Record;
        var introDatabase = new NewGameIntroDatabase();
        NewGameIntroDatabase.IntroSpriteFrame[] linkSpin =
            introDatabase.SpriteFrames("link-spin");
        NewGameIntroDatabase.IntroSpriteFrame[] linkVanish =
            introDatabase.SpriteFrames("link-vanish");
        NewGameIntroDatabase.IntroSpriteFrame[] linkArrival =
            introDatabase.SpriteFrames("link-arrival");
        NewGameIntroDatabase.IntroSpriteFrame[] orbDescend =
            introDatabase.SpriteFrames("orb-descend");
        NewGameIntroDatabase.IntroSpriteFrame[] orbVanish =
            introDatabase.SpriteFrames("orb-vanish");
        int descendFrames = record.InitialWaitFrames + record.VoiceWaitFrames;

        if (_sound.ActiveMusic != OracleSoundEngine.MusEssenceRoom)
            throw new InvalidOperationException(
                "Pregame state $0a did not start MUS_ESSENCE_ROOM for the blue-orb descent.");

        if (record.InitialWaitFrames != 300 || record.VoiceWaitFrames != 60 ||
            record.PostVanishWaitFrames != 60 || record.SummonFrames != 128 ||
            record.LinkX != 0x50 || record.LinkY != 0xd0 ||
            record.LinkSummonedFlag != OracleSaveData.GlobalFlagLinkSummoned ||
            record.PregameIntroDoneFlag != OracleSaveData.GlobalFlagPregameIntroDone ||
            record.TextId != 0x1213 || record.TextPosition != 2 ||
            record.SpinFrameDuration != 4 || record.SpinGraphics.Length != 8 ||
            record.VanishDurations.Length != 4 || record.VanishGraphics.Length != 4 ||
            record.DescendOscillation.Length != 8 ||
            record.DescendOscillation[6] != -1 ||
            record.HoverOscillation.Length != 8 ||
            !record.HoverOscillation.SequenceEqual(new[] { -1, -1, -1, 0, 1, 1, 1, 0 }) ||
            NewGameIntroScreen.FirstVisibleLinkFrameForValidation(
                record.LinkY,
                descendFrames,
                record.DescendOscillation,
                record.HoverOscillation) != 96 ||
            NewGameIntroScreen.LinkZForValidation(
                descendFrames,
                descendFrames,
                record.DescendOscillation,
                record.HoverOscillation) != 77 ||
            NewGameIntroScreen.LinkZForValidation(
                descendFrames + 64,
                descendFrames,
                record.DescendOscillation,
                record.HoverOscillation) != 77 ||
            CutsceneSpriteRenderer.SourcePixelForValidation(0x0900, 0x00, 0, 0) !=
                new Vector2I(64, 64) ||
            CutsceneSpriteRenderer.SourcePixelForValidation(0x1c00, 0x12, 7, 15) !=
                new Vector2I(79, 239) ||
            intro.TotalVanishFrames != 62 ||
            NewGameIntroController.ArrivalFadeWaitFrames != 65 ||
            Player.NewGameSlowFallInitialZ(0x48) != -0x50 ||
            Player.NewGameSlowFallInitialZ(0x90) != -0x80 ||
            Player.NewGameSlowFallZForValidation(0x48, 58) != -3 ||
            Player.NewGameSlowFallZForValidation(0x48, 59) != 0 ||
            RoomView.WaveOffsetForValidation(0xff, 0x1f) != 0xfe ||
            RoomView.WaveOffsetForValidation(0xff, 0x5f) != -0xfe)
        {
            throw new InvalidOperationException(
                "Imported CUTSCENE_PREGAME_INTRO data diverged from the disassembly.");
        }

        if (linkSpin.Length != 8 || linkVanish.Length != 4 || linkArrival.Length != 3 ||
            orbDescend.Length != 2 || orbVanish.Length != 4 ||
            linkSpin.Any(frame => frame.Duration != 4 || frame.BasePalette != 0) ||
            linkSpin[0].Parts.Length != 2 || linkVanish[1].Parts.Length != 1 ||
            !linkArrival.Select(frame => frame.Duration).SequenceEqual(new[] { 4, 4, 4 }) ||
            !linkArrival.Select(frame => frame.SourceOffset)
                .SequenceEqual(new[] { 0x0c40, 0x0c00, 0x0c20 }) ||
            !linkArrival.Select(frame => frame.Parts.Length)
                .SequenceEqual(new[] { 2, 2, 2 }) ||
            linkArrival.Any(frame => frame.BasePalette != 0) ||
            linkArrival
                .Any(frame => frame.Parts.Any(part => (part.Flags & 0x07) != 0)) ||
            orbDescend[0].SourceOffset != 0x1c00 ||
            orbDescend[0].Parts.Length != 18 || orbDescend[1].Parts.Length != 14 ||
            orbDescend.Any(frame => frame.BasePalette != 0 ||
                frame.Parts.Any(part => (part.Flags & 0x07) != 4)) ||
            !orbVanish.Select(frame => frame.Duration)
                .SequenceEqual(new[] { 30, 18, 1, 1 }) ||
            !orbVanish.Select(frame => frame.Parts.Length)
                .SequenceEqual(new[] { 6, 2, 2, 2 }) ||
            orbVanish.Any(frame => frame.SourceOffset != 0x1d40 ||
                frame.BasePalette != 4))
        {
            throw new InvalidOperationException(
                "Imported pregame Link/orb/slow-fall graphics, OAM, palettes, or animation frames diverged.");
        }

        for (int frame = 0; frame < intro.TotalVoiceWaitFrames - 1; frame++)
            intro.Update(1.0 / 60.0);
        if (intro.CurrentStage != NewGameIntroController.Stage.WaitingForVoice ||
            intro.StageFrame != intro.TotalVoiceWaitFrames - 1 ||
            screen.Dialogue.IsOpen)
        {
            throw new InvalidOperationException(
                "TX_1213 opened before the original 300+60 update wait.");
        }
        intro.Update(1.0 / 60.0);
        if (intro.CurrentStage != NewGameIntroController.Stage.Dialogue ||
            intro.StageFrame != 0 ||
            !screen.Dialogue.IsOpen ||
            screen.Dialogue.CurrentMessage != "Accept our\nquest, hero!" ||
            screen.Dialogue.Position.Y != 80)
        {
            throw new InvalidOperationException(
                "CUTSCENE_PREGAME_INTRO did not open TX_1213 at position 2.");
        }

        screen.Dialogue.Close();
        intro.Update(1.0 / 60.0);
        if (intro.CurrentStage != NewGameIntroController.Stage.Vanishing ||
            intro.StageFrame != 0)
        {
            throw new InvalidOperationException(
                "Closing TX_1213 did not enter the vanish timeline at frame zero.");
        }
        for (int frame = 0; frame < intro.TotalVanishFrames - 1; frame++)
            intro.Update(1.0 / 60.0);
        if (intro.CurrentStage != NewGameIntroController.Stage.Vanishing ||
            intro.StageFrame != intro.TotalVanishFrames - 1)
        {
            throw new InvalidOperationException("Link's original vanish animation ended early.");
        }
        intro.Update(1.0 / 60.0);
        if (intro.CurrentStage != NewGameIntroController.Stage.PostVanish ||
            intro.StageFrame != 0)
        {
            throw new InvalidOperationException(
                "Link's vanish did not enter the post-vanish timeline at frame zero.");
        }
        for (int frame = 0; frame < record.PostVanishWaitFrames; frame++)
            intro.Update(1.0 / 60.0);
        if (completionRequests != 1 ||
            intro.CurrentStage != NewGameIntroController.Stage.Complete ||
            intro.StageFrame != record.PostVanishWaitFrames ||
            _sound.ActiveMusic != 0)
        {
            throw new InvalidOperationException(
                "The new-game intro did not stop music and hand off to the summon transition exactly once.");
        }

        _player.WarpTo(new Vector2(0x50, 0x48));
        _newGameArrivalTicks = 0.0;
        _newGameArrivalFadeFrames = NewGameIntroController.ArrivalFadeWaitFrames;
        _newGameArrivalFrames = record.SummonFrames;
        _newGameArrivalPhase = 0;
        _newGameArrivalLastFrame = 0;
        _player.Visible = false;
        _player.SetPhysicsProcess(false);
        _player.SetProcess(false);
        for (int frame = 1; frame <= NewGameIntroController.ArrivalFadeWaitFrames; frame++)
        {
            if (!UpdateNewGameArrival(1.0 / 60.0) || _player.Visible ||
                _player.IsNewGameSlowFalling)
            {
                throw new InvalidOperationException(
                    "The new-game room fade exposed Link before transition $0b.");
            }
        }
        for (int frame = 1; frame < record.SummonFrames / 2; frame++)
        {
            if (!UpdateNewGameArrival(1.0 / 60.0) || _player.Visible)
                throw new InvalidOperationException("Link appeared before summon-wave frame 64.");
        }
        if (!UpdateNewGameArrival(1.0 / 60.0) || !_player.Visible ||
            !_player.IsNewGameSlowFalling || _player.NewGameSlowFallFrame != 0 ||
            _player.NewGameSlowFallZ != -0x50)
        {
            throw new InvalidOperationException(
                "Transition $0b did not begin at wave frame 64 and Z -$50.");
        }
        for (int frame = 65; frame <= 67; frame++)
            UpdateNewGameArrival(1.0 / 60.0);
        if (_player.NewGameSlowFallFrame != 0)
            throw new InvalidOperationException("LINK_ANIM_MODE_FALL frame $e6 ended early.");
        UpdateNewGameArrival(1.0 / 60.0);
        if (_player.NewGameSlowFallFrame != 1)
            throw new InvalidOperationException("LINK_ANIM_MODE_FALL did not advance after four updates.");
        for (int frame = 69; frame <= 122; frame++)
            UpdateNewGameArrival(1.0 / 60.0);
        if (!_player.IsNewGameSlowFalling || _player.NewGameSlowFallFrame != 2 ||
            _player.NewGameSlowFallZ != -3)
        {
            throw new InvalidOperationException(
                "Transition $0b diverged before its 59th gravity update.");
        }
        if (!UpdateNewGameArrival(1.0 / 60.0) || _player.IsNewGameSlowFalling)
            throw new InvalidOperationException("Transition $0b did not land on wave frame 123.");
        for (int frame = 124; frame < record.SummonFrames; frame++)
            UpdateNewGameArrival(1.0 / 60.0);
        if (UpdateNewGameArrival(1.0 / 60.0) || !_player.Visible ||
            !_player.IsProcessing() || !_player.IsPhysicsProcessing())
        {
            throw new InvalidOperationException(
                "The summon wave did not restore Link control on frame 128.");
        }

        screen.QueueFree();
        GD.Print("Validated CUTSCENE_PREGAME_INTRO frame-96 top entrance, interleaved 8x16 OBJ cells, " +
            "hardware OAM priority, cumulative descend/hover Z tables, $0d/$06 blue-orb OAM and palette 4, " +
            "300/60 waits, TX_1213, 62-update vanish handoff, 60-update black hold, 65-update white-fade wait, " +
            "MUS_ESSENCE_ROOM/STOPMUSIC handoff, 128-update wave, and transition $0b's three-pose " +
            "4-update/59-gravity-update slow fall.");
    }

    private void ValidateExplicitSavePersistence()
    {
        int saveWrites = _saveWriteRequests;
        bool flagWasSet = _saveData.HasGlobalFlag(OracleSaveData.GlobalFlagSavedNayru);
        int rupees = _inventory.Rupees;
        int rupeeDelta = rupees == 999 ? -1 : 1;

        _saveData.SetGlobalFlag(OracleSaveData.GlobalFlagSavedNayru, !flagWasSet);
        _inventory.AddRupees(rupeeDelta);
        _ExitTree();
        _inventory.AddRupees(-rupeeDelta);
        _saveData.SetGlobalFlag(OracleSaveData.GlobalFlagSavedNayru, flagWasSet);

        _inventoryMenu.OpenSaveImmediatelyForValidation();
        int menuSaveRequests = _inventoryMenu.SaveRequests;
        _inventoryMenu.SelectSaveOptionForValidation();
        if (_saveWriteRequests != saveWrites ||
            _inventoryMenu.SaveRequests != menuSaveRequests ||
            _saveQuitScreen.DelayCounter != InventoryMenuController.SaveSelectionDelayFrames)
        {
            throw new InvalidOperationException(
                "Ordinary save-image changes, application exit, or Continue unexpectedly wrote the active file.");
        }
        _inventoryMenu.CloseImmediatelyForValidation();

        GD.Print("Validated explicit-only persistence: gameplay changes and Continue remain " +
            "in memory, and application exit does not save.");
    }

    private void ValidateSaveDataFoundation()
    {
        OracleSaveData save = OracleSaveData.CreateStandardGame();
        var standardInventory = new InventoryState(_treasures, save);
        if (save.HasGlobalFlag(OracleSaveData.GlobalFlagMakuTreeDisappeared) ||
            save.GetRoomFlags(0, 0x38) != 0 ||
            save.RespawnGroup != 0 || save.RespawnRoom != 0x8a ||
            save.RespawnStateModifier != 0 || save.RespawnFacing != 0 ||
            save.RespawnY != 0x38 || save.RespawnX != 0x48 ||
            standardInventory.HasTreasure(TreasureDatabase.TreasureSword) ||
            standardInventory.SwordLevel != 0 ||
            standardInventory.EquippedA == InventoryState.ItemSword ||
            standardInventory.EquippedB == InventoryState.ItemSword)
        {
            throw new InvalidOperationException(
                "A standard file did not begin swordless, with clear flags and the " +
                "original 0:8a/$38/$48 checkpoint.");
        }

        save.SetGlobalFlag(OracleSaveData.GlobalFlagMakuTreeDisappeared);
        save.SetRoomFlag(2, 0x38, OracleSaveData.RoomFlagLayoutSwap);
        save.SetRoomFlag(6, 0x87, OracleSaveData.RoomFlagItem);
        save.SetRoomFlag(7, 0xa6, OracleSaveData.RoomFlag80);
        save.SetRoomFlag(1, 0x41, OracleSaveData.RoomFlagPortalSpotDiscovered);
        save.SetMinimapLocation(1, 0x41);
        save.SetMakuTreeState(1);
        if (!save.HasGlobalFlag(0x0c) ||
            !save.HasRoomFlag(0, 0x38, OracleSaveData.RoomFlagLayoutSwap) ||
            !save.HasRoomFlag(4, 0x87, OracleSaveData.RoomFlagItem) ||
            !save.HasRoomFlag(5, 0xa6, OracleSaveData.RoomFlag80) ||
            !save.HasRoomFlag(3, 0x41, OracleSaveData.RoomFlagPortalSpotDiscovered) ||
            save.MinimapGroup != 1 || save.MinimapRoom != 0x41 || save.MakuTreeState != 1)
        {
            throw new InvalidOperationException(
                "Global flags, room flags, or the original 0/2, 1/3, 4/6 group aliases diverged.");
        }

        var inventory = new InventoryState(_treasures, save);
        inventory.GiveTreasure(_treasures.GetObject("TREASURE_OBJECT_SWITCH_HOOK_00"));
        inventory.AddRupees(987);
        save.SetDeathRespawnPoint(5, 0xa6, 2, 3, 0x78, 0x88);
        byte[] encoded = save.Serialize();
        if (encoded.Length != OracleSaveData.FileSize ||
            !encoded.AsSpan(2, 8).SequenceEqual("Z21216-0"u8) ||
            !OracleSaveData.TryDeserialize(encoded, out OracleSaveData? decoded))
        {
            throw new InvalidOperationException(
                "The $550-byte Ages save image did not pass its signature/checksum round trip.");
        }

        var restoredInventory = new InventoryState(_treasures, decoded!);
        if (!restoredInventory.HasTreasure(TreasureDatabase.TreasureSwitchHook) ||
            restoredInventory.SwitchHookLevel != 1 ||
            restoredInventory.EquippedB != TreasureDatabase.TreasureSwitchHook ||
            restoredInventory.Rupees != 987 || !decoded!.HasGlobalFlag(0x0c) ||
            !decoded.HasRoomFlag(0, 0x38, OracleSaveData.RoomFlagLayoutSwap) ||
            decoded.RespawnGroup != 5 || decoded.RespawnRoom != 0xa6 ||
            decoded.RespawnStateModifier != 2 || decoded.RespawnFacing != 3 ||
            decoded.RespawnY != 0x78 || decoded.RespawnX != 0x88)
        {
            throw new InvalidOperationException(
                "Inventory, BCD rupees, story flags, or room flags were lost across save reload.");
        }

        encoded[^1] ^= 0x80;
        if (OracleSaveData.TryDeserialize(encoded, out _))
            throw new InvalidOperationException("A corrupted Ages save checksum was accepted.");

        GD.Print("Validated original $550-byte save signature/checksum, 128 global flags, " +
            "four aliased room-flag tables, initial/current death checkpoint, inventory fields, " +
            "swordless standard-game state, and BCD rupee round trip.");
    }

    private static void ValidateSaveStore()
    {
        string directory = Path.Combine(
            Path.GetTempPath(), $"ooa-save-validation-{Guid.NewGuid():N}");
        string primary = Path.Combine(directory, "save.sav");
        string backup = primary + ".bak";
        try
        {
            var save = OracleSaveData.CreateStandardGame();
            save.SetLinkName("ONE");
            OracleSaveStore.SaveResult result = OracleSaveStore.Save(save, primary);
            if (!result.Success || !File.Exists(primary) || File.Exists(backup) ||
                OracleSaveStore.LoadOrCreate(primary).LinkName != "ONE")
            {
                throw new InvalidOperationException(
                    $"The first-save path failed or invented a backup generation: {result.ErrorMessage}");
            }

            save.SetLinkName("TWO");
            result = OracleSaveStore.Save(save, primary);
            if (!result.Success ||
                OracleSaveStore.LoadOrCreate(primary).LinkName != "TWO" ||
                !OracleSaveData.TryDeserialize(File.ReadAllBytes(backup), out OracleSaveData? first) ||
                first!.LinkName != "ONE")
            {
                throw new InvalidOperationException(
                    $"The second save did not rotate generation ONE into .bak: {result.ErrorMessage}");
            }

            save.SetLinkName("THREE");
            result = OracleSaveStore.Save(save, primary);
            if (!result.Success ||
                OracleSaveStore.LoadOrCreate(primary).LinkName != "THREE" ||
                !OracleSaveData.TryDeserialize(File.ReadAllBytes(backup), out OracleSaveData? second) ||
                second!.LinkName != "TWO")
            {
                throw new InvalidOperationException(
                    $"A later save did not preserve the immediately previous generation: {result.ErrorMessage}");
            }

            File.WriteAllBytes(primary, new byte[] { 0xde, 0xad, 0xbe, 0xef });
            OracleSaveData recovered = OracleSaveStore.LoadOrCreate(primary);
            if (recovered.LinkName != "TWO")
                throw new InvalidOperationException("A corrupt primary did not load generation TWO from .bak.");
            recovered.SetLinkName("FOUR");
            result = OracleSaveStore.Save(recovered, primary);
            if (!result.Success || OracleSaveStore.LoadOrCreate(primary).LinkName != "FOUR" ||
                !OracleSaveData.TryDeserialize(File.ReadAllBytes(backup), out OracleSaveData? preserved) ||
                preserved!.LinkName != "TWO")
            {
                throw new InvalidOperationException(
                    "Saving after backup recovery overwrote the good backup with the corrupt primary.");
            }

            string blockingFile = Path.Combine(directory, "not-a-directory");
            File.WriteAllText(blockingFile, "blocked");
            result = OracleSaveStore.Save(
                recovered, Path.Combine(blockingFile, "save.sav"));
            if (result.Success || string.IsNullOrWhiteSpace(result.ErrorMessage))
                throw new InvalidOperationException("A save I/O failure escaped or returned success.");
            if (File.Exists(primary + ".tmp") || File.Exists(primary + ".previous.tmp"))
                throw new InvalidOperationException("A completed save left temporary generation files behind.");
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }

        GD.Print("Validated durable temporary save serialization, first-save promotion, " +
            "previous-generation .bak rotation, corrupt-primary recovery, cleanup, and surfaced I/O errors.");
    }

    private void ValidateTreasureInterpreter()
    {
        if (_treasures.BehaviourCount != 0x68)
        {
            throw new InvalidOperationException(
                $"Expected `$68 typed treasure behaviours, found {_treasures.BehaviourCount}.");
        }
        for (int treasure = 0; treasure < 0x68; treasure++)
        {
            TreasureDatabase.BehaviourRecord behaviour = _treasures.GetBehaviour(treasure);
            if (behaviour.TreasureId != treasure)
                throw new InvalidOperationException($"Treasure ${treasure:x2} resolved to another behaviour.");
        }

        static TreasureDatabase.TreasureObjectRecord Reward(int treasure, int parameter) => new(
            $"VALIDATION_TREASURE_{treasure:x2}", treasure, 0, parameter, 0xff, 0, string.Empty);

        OracleSaveData save = OracleSaveData.CreateStandardGame();
        var inventory = new InventoryState(_treasures, save);
        int inventoryCommits = 0;
        int saveCommits = 0;
        int rupeeNotifications = 0;
        inventory.Changed += () => inventoryCommits++;
        inventory.RupeesChanged += () => rupeeNotifications++;
        save.Changed += () => saveCommits++;

        inventory.GiveTreasure(_treasures.GetObject("TREASURE_OBJECT_RUPEES_04"));
        if (inventory.Rupees != 30 || inventoryCommits != 1 || saveCommits != 1 ||
            rupeeNotifications != 1)
        {
            throw new InvalidOperationException(
                "A mode `$0e rupee grant did not commit exactly once.");
        }

        inventoryCommits = saveCommits = 0;
        inventory.GiveTreasure(Reward(TreasureDatabase.TreasureSeedSatchel, 1));
        if (inventory.SeedSatchelLevel != 1 || inventory.EmberSeeds != 0x20 ||
            save.ReadWramByte(0xc6b9) != 0x20 || inventoryCommits != 1 || saveCommits != 1)
        {
            throw new InvalidOperationException(
                "The Seed Satchel did not grant and persist its extra 20 Ember Seeds in one transaction.");
        }

        inventory.GiveTreasure(Reward(0x62, 0));
        inventory.GiveTreasure(Reward(0x20, 0x15));
        inventory.GiveTreasure(Reward(0x21, 0x09));
        inventory.GiveTreasure(Reward(0x22, 0x09));
        inventory.GiveTreasure(Reward(0x23, 0x09));
        inventory.GiveTreasure(Reward(0x24, 0x09));
        if (inventory.SeedSatchelLevel != 2 || inventory.EmberSeeds != 0x35 ||
            inventory.ScentSeeds != 0x09 || inventory.PegasusSeeds != 0x09 ||
            inventory.GaleSeeds != 0x09 || inventory.MysterySeeds != 0x09)
        {
            throw new InvalidOperationException(
                "Typed mode `$0f seed counters or the satchel capacity table diverged.");
        }

        inventory.GiveTreasure(Reward(0x0d, 0x10));
        inventory.GiveTreasure(Reward(0x0e, 0x0c));
        inventory.GiveTreasure(Reward(0x52, 0x07));
        inventory.GiveTreasure(Reward(0x07, 0x02));
        inventory.GiveTreasure(Reward(0x08, 0x01));
        inventory.GiveTreasure(Reward(0x51, 0x22));
        inventory.GiveTreasure(Reward(0x15, 0x00));
        if (inventory.Bombchus != 0x10 || inventory.AnimalCompanion != 0x0c ||
            inventory.RememberedCompanionId != 0x07 || inventory.ObtainedSeasons != 0x04 ||
            inventory.MagnetGlovePolarity != 0x01 ||
            save.ReadWramByte(0xc6fb) != 0x23)
        {
            throw new InvalidOperationException(
                "An imported typed WRAM binding did not execute or persist its collection mode.");
        }

        inventory.GiveTreasure(Reward(TreasureDatabase.TreasureBoomerang, 1));
        inventory.GiveTreasure(Reward(TreasureDatabase.TreasureFeather, 1));
        inventory.GiveTreasure(Reward(TreasureDatabase.TreasureSlingshot, 1));
        inventory.SelectSatchelSeeds(2);
        inventory.SelectShooterSeeds(3);
        inventory.SelectSlingshotSeeds(4);
        inventory.GiveTreasure(Reward(0x60, 0));
        if (save.ReadWramByte(0xc700) != 1 || save.ReadWramByte(0xc701) != 1 ||
            save.ReadWramByte(0xc6ff) != 1 || save.ReadWramByte(0xc703) != 2 ||
            save.ReadWramByte(0xc704) != 3 || save.ReadWramByte(0xc705) != 4 ||
            !inventory.HasUpgrade(0) || !inventory.HasTreasure(0x60))
        {
            throw new InvalidOperationException(
                "Item levels, selected seeds, or transient wUpgradesObtained were not updated.");
        }

        // These linked-item bytes alias the first room-flag bytes in the original layout.
        // An unrelated inventory update must consume the live byte instead of restoring a stale copy.
        save.SetRoomFlag(0, 0x00, 0x80);
        inventory.GiveTreasure(Reward(0x20, 0x01));
        if (save.ReadWramByte(0xc700) != 0x81)
            throw new InvalidOperationException("Inventory persistence clobbered aliased room flag 0:00.");

        if (!OracleSaveData.TryDeserialize(save.Serialize(), out OracleSaveData? restoredSave))
            throw new InvalidOperationException("Typed treasure WRAM state did not deserialize.");
        var restored = new InventoryState(_treasures, restoredSave);
        if (restored.SeedSatchelLevel != 2 || restored.EmberSeeds != 0x36 ||
            restored.ScentSeeds != 0x09 || restored.Bombchus != 0x10 ||
            restored.BoomerangLevel != 0x81 || restored.FeatherLevel != 1 ||
            restored.SlingshotLevel != 1 || restored.SatchelSelectedSeeds != 2 ||
            restored.ShooterSelectedSeeds != 3 || restored.SlingshotSelectedSeeds != 4)
        {
            throw new InvalidOperationException(
                "Typed treasure variables were lost across the `$550-byte save round trip.");
        }

        OracleSaveData ringSave = OracleSaveData.CreateStandardGame();
        var ringInventory = new InventoryState(_treasures, ringSave);
        ringInventory.GiveTreasure(Reward(TreasureDatabase.TreasureRing, 0x1e));
        if (ringInventory.UnappraisedRingCount != 1 ||
            ringInventory.UnappraisedRingAt(0) != 0x5e ||
            ringInventory.UnappraisedRingAt(1) != 0xff ||
            ringSave.ReadWramByte(0xc6cd) != 0x01)
        {
            throw new InvalidOperationException(
                "Treasure mode `$09 did not append an unappraised ring and update its BCD count.");
        }

        var fullRings = new byte[0x40];
        Array.Fill(fullRings, (byte)0x41);
        ringSave.WriteWramBytes(0xc5c0, fullRings);
        ringSave.WriteWramByte(0xc6cd, 0x64);
        ringInventory = new InventoryState(_treasures, ringSave);
        ringInventory.GiveTreasure(Reward(TreasureDatabase.TreasureRing, 0x02));
        if (ringInventory.UnappraisedRingCount != 0x40 ||
            ringInventory.UnappraisedRingAt(0x3e) != 0x41 ||
            ringInventory.UnappraisedRingAt(0x3f) != 0x42 ||
            ringSave.ReadWramByte(0xc6cd) != 0x64)
        {
            throw new InvalidOperationException(
                "A full unappraised-ring list did not replace a duplicate like mode `$09.");
        }

        GD.Print("Validated all `$68 typed treasure behaviours, seed counters/capacities, " +
            "mode `$09 ring storage, linked item/selection aliases, transient upgrade bits, " +
            "single-commit treasure transactions, and save persistence.");
    }

    private void ValidateDungeonCollectibles()
    {
        int currentDungeon = -1;
        OracleSaveData save = OracleSaveData.CreateStandardGame();
        var inventory = new InventoryState(
            _treasures, save, () => currentDungeon);
        TreasureDatabase.TreasureObjectRecord smallKey =
            _treasures.GetObject("TREASURE_OBJECT_SMALL_KEY_03");
        TreasureDatabase.TreasureObjectRecord bossKey =
            _treasures.GetObject("TREASURE_OBJECT_BOSS_KEY_03");
        TreasureDatabase.TreasureObjectRecord compass =
            _treasures.GetObject("TREASURE_OBJECT_COMPASS_02");
        TreasureDatabase.TreasureObjectRecord map =
            _treasures.GetObject("TREASURE_OBJECT_MAP_02");

        void GiveDungeonSet()
        {
            inventory.GiveTreasure(smallKey);
            inventory.GiveTreasure(bossKey);
            inventory.GiveTreasure(compass);
            inventory.GiveTreasure(map);
        }

        // A malformed/non-dungeon reward must not silently mutate dungeon 0.
        GiveDungeonSet();
        for (int dungeon = 0; dungeon < 16; dungeon++)
        {
            if (inventory.GetDungeonSmallKeys(dungeon) != 0 ||
                inventory.HasDungeonBossKey(dungeon) ||
                inventory.HasDungeonCompass(dungeon) ||
                inventory.HasDungeonMap(dungeon))
            {
                throw new InvalidOperationException(
                    "A dungeon collectible obtained with wDungeonIndex `$ff mutated dungeon state.");
            }
        }

        int[] boundaryDungeons = { 0, 7, 8, 15 };
        foreach (int dungeon in boundaryDungeons)
        {
            currentDungeon = dungeon;
            GiveDungeonSet();
        }
        currentDungeon = 8;
        inventory.GiveTreasure(smallKey);

        for (int dungeon = 0; dungeon < 16; dungeon++)
        {
            bool expected = boundaryDungeons.Contains(dungeon);
            int expectedKeys = dungeon == 8 ? 2 : expected ? 1 : 0;
            if (inventory.GetDungeonSmallKeys(dungeon) != expectedKeys ||
                inventory.HasDungeonBossKey(dungeon) != expected ||
                inventory.HasDungeonCompass(dungeon) != expected ||
                inventory.HasDungeonMap(dungeon) != expected)
            {
                throw new InvalidOperationException(
                    $"Dungeon collectible indexing failed for dungeon {dungeon}: " +
                    $"keys={inventory.GetDungeonSmallKeys(dungeon)}, " +
                    $"boss={inventory.HasDungeonBossKey(dungeon)}, " +
                    $"compass={inventory.HasDungeonCompass(dungeon)}, " +
                    $"map={inventory.HasDungeonMap(dungeon)}.");
            }
        }

        if (save.ReadWramByte(0xc672) != 1 ||
            save.ReadWramByte(0xc679) != 1 ||
            save.ReadWramByte(0xc67a) != 2 ||
            save.ReadWramByte(0xc681) != 1 ||
            save.ReadWramByte(0xc682) != 0x81 ||
            save.ReadWramByte(0xc683) != 0x81 ||
            save.ReadWramByte(0xc684) != 0x81 ||
            save.ReadWramByte(0xc685) != 0x81 ||
            save.ReadWramByte(0xc686) != 0x81 ||
            save.ReadWramByte(0xc687) != 0x81)
        {
            throw new InvalidOperationException(
                "Dungeon collectibles did not retain the original 16 key bytes and " +
                "two-byte boss-key/compass/map bitsets in WRAM.");
        }

        byte[] encoded = save.Serialize();
        if (!OracleSaveData.TryDeserialize(encoded, out OracleSaveData? decoded))
            throw new InvalidOperationException("Dungeon collectible save data did not deserialize.");
        var restored = new InventoryState(_treasures, decoded);
        for (int dungeon = 0; dungeon < 16; dungeon++)
        {
            bool expected = boundaryDungeons.Contains(dungeon);
            int expectedKeys = dungeon == 8 ? 2 : expected ? 1 : 0;
            if (restored.GetDungeonSmallKeys(dungeon) != expectedKeys ||
                restored.HasDungeonBossKey(dungeon) != expected ||
                restored.HasDungeonCompass(dungeon) != expected ||
                restored.HasDungeonMap(dungeon) != expected)
            {
                throw new InvalidOperationException(
                    $"Dungeon {dungeon} collectibles were lost across save serialization.");
            }
        }

        GD.Print("Validated treasure behaviour modes `$87/`$86 against live dungeon " +
            "indices 0, 7, 8, and 15, non-dungeon rejection, both bitset bytes, " +
            "and save round-trip persistence.");
    }

    private void ValidateDeathRespawnCheckpoints()
    {
        if (!_deathRespawnPoints.UpdatesContinuously(0, 0x38) ||
            !_deathRespawnPoints.UpdatesContinuously(1, 0x38) ||
            _deathRespawnPoints.UpdatesContinuously(0, 0x39))
        {
            throw new InvalidOperationException(
                "The imported roomSpecificCode $06 death-respawn room table regressed.");
        }

        int saveWrites = _saveWriteRequests;
        LoadValidationRoom(0, 0x38);
        _player.WarpTo(new Vector2(0x38, 0x58));
        _player.Face(Vector2I.Left);
        _deathRespawnPoints.Update();
        if (_saveData.RespawnGroup != 0 || _saveData.RespawnRoom != 0x38 ||
            _saveData.RespawnFacing != 3 || _saveData.RespawnY != 0x58 ||
            _saveData.RespawnX != 0x38 || _saveWriteRequests != saveWrites)
        {
            throw new InvalidOperationException(
                "Room 0:38 did not update its live death checkpoint without autosaving.");
        }

        LoadValidationRoom(0, 0x39);
        _player.WarpTo(new Vector2(0x78, 0x68));
        _player.Face(Vector2I.Down);
        _deathRespawnPoints.Update();
        if (!OracleSaveData.TryDeserialize(_saveData.Serialize(), out OracleSaveData? restored) ||
            restored!.RespawnGroup != 0 || restored.RespawnRoom != 0x38 ||
            restored.RespawnFacing != 3 || restored.RespawnY != 0x58 ||
            restored.RespawnX != 0x38)
        {
            throw new InvalidOperationException(
                "An ordinary room replaced the checkpoint, or saving captured Link's arbitrary position.");
        }

        GD.Print("Validated imported roomSpecificCode $06 checkpoint rooms and save-image " +
            "retention of the maintained checkpoint instead of Link's arbitrary position.");
    }

    private void ValidateMainMenu()
    {
        var stored = new OracleSaveData?[OracleSaveStore.SlotCount];
        var screen = new MainMenuScreen { Name = "MainMenuValidation" };
        AddChild(screen);
        int startedSlot = -1;
        OracleSaveData? startedSave = null;
        var menu = new MainMenuController(
            screen,
            (slot, save) => { startedSlot = slot; startedSave = save; },
            slot => stored[slot],
            (slot, save) =>
            {
                stored[slot] = save;
                return OracleSaveStore.SaveResult.Succeeded;
            },
            slot => stored[slot] = null);

        if (MainMenuScreen.InterleavedSourceTileForValidation(0, 16) != 0 ||
            MainMenuScreen.InterleavedSourceTileForValidation(1, 16) != 16 ||
            MainMenuScreen.InterleavedSourceTileForValidation(2, 16) != 1 ||
            !screen.FileNameStripColorForValidation.IsEqualApprox(Colors.Black) ||
            !screen.FilePanelColorForValidation.IsEqualApprox(
                screen.DeathTileBackgroundColorForValidation) ||
            !screen.NameEntryFieldColorForValidation.IsEqualApprox(
                screen.NameEntryPanelColorForValidation) ||
            !screen.NameEntryHighlightIsGlyphMaskForValidation ||
            MainMenuScreen.SpriteColorIndexForValidation(Colors.White, inverted: false) != 0 ||
            MainMenuScreen.SpriteColorIndexForValidation(Colors.Black, inverted: false) != 3 ||
            MainMenuScreen.SpriteColorIndexForValidation(Colors.Black, inverted: true) != 0 ||
            MainMenuScreen.SpriteColorIndexForValidation(Colors.White, inverted: true) != 3 ||
            MainMenuScreen.TextSpeedCursorPositionForValidation(0) != new Vector2(41, 128) ||
            MainMenuScreen.TextSpeedCursorPositionForValidation(4) != new Vector2(105, 128))
        {
            throw new InvalidOperationException(
                "File-select tile interleave, panel fill, filename/name-entry fields, " +
                "name cursor priority mask, sprite inversion, " +
                "or original message-speed cursor coordinates regressed.");
        }

        menu.BeginTitleStart();
        for (int frame = 0; frame < MainMenuController.WhiteFadeFrames - 1; frame++)
            menu.Update(1.0 / 60.0);
        if (menu.CurrentPage != MainMenuScreen.Page.Title)
            throw new InvalidOperationException("The title white fade ended before its original 32 updates.");
        menu.Update(1.0 / 60.0);
        if (menu.CurrentPage != MainMenuScreen.Page.FileSelect)
            throw new InvalidOperationException("The title did not enter file select after its 32-update white fade.");
        menu.Move(Vector2I.Up);
        if (menu.Cursor != 3)
            throw new InvalidOperationException("File-select Up did not wrap from file 1 to the bottom row.");
        menu.Move(Vector2I.Right);
        if (screen.Choice != 1)
            throw new InvalidOperationException("File-select Copy/Erase horizontal selection did not toggle.");

        screen.SetCursor(0);
        menu.Accept();
        if (menu.CurrentPage != MainMenuScreen.Page.NewFileOptions)
            throw new InvalidOperationException("A blank slot did not open New Game/Secret/Game Link.");
        menu.Accept();
        if (menu.CurrentPage != MainMenuScreen.Page.NameEntry)
            throw new InvalidOperationException("New Game did not open file name entry.");
        if (!screen.TryGetSelectedNameCharacter(out char initialCharacter) ||
            initialCharacter != 'A')
            throw new InvalidOperationException(
                "The original US name keyboard did not begin on uppercase A.");
        for (int column = 0; column < 6; column++)
            screen.MoveNameCursor(Vector2I.Right);
        if (!screen.TryGetSelectedNameCharacter(out char lowercaseCharacter) ||
            lowercaseCharacter != 'a')
            throw new InvalidOperationException(
                "Name entry did not preserve the original six-column uppercase/lowercase split.");
        for (int column = 0; column < 6; column++)
            screen.MoveNameCursor(Vector2I.Left);
        foreach (char character in "LINK")
            screen.AppendNameCharacter(character);
        for (int row = 0; row < 5; row++)
            screen.MoveNameCursor(Vector2I.Down);
        screen.MoveNameCursor(Vector2I.Right);
        screen.MoveNameCursor(Vector2I.Right);
        if (screen.NameCursor != 0x56 || screen.NameLowerChoice != 2)
            throw new InvalidOperationException(
                "Name entry did not map the five character rows onto its original OK option.");
        menu.Accept();
        if (stored[0]?.LinkName != "LINK" || menu.CurrentPage != MainMenuScreen.Page.FileSelect)
            throw new InvalidOperationException("Name entry did not initialize and save the selected standard file.");

        screen.SetCursor(0);
        menu.Accept();
        if (menu.CurrentPage != MainMenuScreen.Page.TextSpeed || screen.TextSpeed != 4 ||
            screen.Cursor != 0 || screen.SelectedSlot != 0)
            throw new InvalidOperationException("An existing file did not open its message-speed confirmation.");
        menu.Move(Vector2I.Left);
        menu.Accept();
        for (int frame = 0; frame < MainMenuController.WhiteFadeFrames - 1; frame++)
            menu.Update(1.0 / 60.0);
        if (startedSave is not null)
            throw new InvalidOperationException("File select started gameplay before its 32-update white fade.");
        menu.Update(1.0 / 60.0);
        if (startedSlot != 0 || startedSave != stored[0] || stored[0]!.TextSpeed != 3)
            throw new InvalidOperationException("Message-speed confirmation did not save and start the chosen file.");

        var copyScreen = new MainMenuScreen { Name = "MainMenuCopyValidation" };
        AddChild(copyScreen);
        var copyMenu = new MainMenuController(
            copyScreen, (_, _) => { }, slot => stored[slot],
            (slot, save) =>
            {
                stored[slot] = save;
                return OracleSaveStore.SaveResult.Succeeded;
            },
            slot => stored[slot] = null);
        copyMenu.OpenFileSelect();
        copyScreen.SetCursor(3);
        copyMenu.Accept();
        copyScreen.SetCursor(0);
        copyMenu.Accept();
        copyMenu.Accept();
        copyMenu.Move(Vector2I.Right);
        copyMenu.Accept();
        if (stored[1]?.LinkName != "LINK" || ReferenceEquals(stored[0], stored[1]))
            throw new InvalidOperationException("Copy did not clone the original $550-byte file into its destination.");

        copyScreen.SetCursor(3);
        copyScreen.SetChoice(1);
        copyMenu.Accept();
        copyScreen.SetCursor(1);
        copyMenu.Accept();
        copyMenu.Move(Vector2I.Right);
        copyMenu.Accept();
        if (stored[1] is not null)
            throw new InvalidOperationException("Erase confirmation did not clear the selected file slot.");

        bool startedAfterFailure = false;
        var failureScreen = new MainMenuScreen { Name = "MainMenuSaveFailureValidation" };
        AddChild(failureScreen);
        var failureMenu = new MainMenuController(
            failureScreen,
            (_, _) => startedAfterFailure = true,
            slot => stored[slot],
            (_, _) => OracleSaveStore.SaveResult.Failed("validation failure"));
        failureMenu.OpenFileSelect();
        failureScreen.SetCursor(0);
        failureMenu.Accept();
        failureMenu.Accept();
        if (failureMenu.CurrentPage != MainMenuScreen.Page.TextSpeed ||
            !failureScreen.SaveErrorVisible ||
            failureMenu.LastSaveError != "validation failure" || startedAfterFailure)
        {
            throw new InvalidOperationException(
                "A file-select save failure escaped, changed page, or began gameplay.");
        }
        failureMenu.Accept();
        if (failureScreen.SaveErrorVisible || failureMenu.CurrentPage != MainMenuScreen.Page.TextSpeed)
            throw new InvalidOperationException("The file-select save error was not dismissible and retryable.");

        screen.QueueFree();
        copyScreen.QueueFree();
        failureScreen.QueueFree();
        GD.Print("Validated title/file-select 32-update white fades, slot wrapping, " +
            "new-file naming, message speed, copy, erase, and retryable save failures.");
    }

    private void ValidateDebugFlagMenu()
    {
        if (!InputMap.HasAction("debug_flags"))
            throw new InvalidOperationException("The F1 debug_flags input action was not registered.");

        _debugFlagMenu.OpenImmediatelyForValidation();
        if (!_debugFlagMenu.IsActive || !_debugFlagScreen.Visible ||
            !_gameplayPause.IsOwnedBy(_debugFlagMenu) ||
            _player.IsPhysicsProcessing() || _player.IsProcessing())
        {
            throw new InvalidOperationException(
                "The debug flag menu did not open in screen space and freeze Link.");
        }

        _debugFlagScreen._Input(new InputEventKey
        {
            PhysicalKeycode = Key.Tab,
            Pressed = true
        });
        if (_debugFlagScreen.Page != DebugFlagScreen.FlagPage.Room)
            throw new InvalidOperationException(
                "A physical Tab key event did not switch the flag editor to room flags.");
        _debugFlagScreen._Input(new InputEventKey
        {
            Keycode = Key.Tab,
            Pressed = true
        });
        if (_debugFlagScreen.Page != DebugFlagScreen.FlagPage.Global)
            throw new InvalidOperationException(
                "A logical Tab key event did not switch the flag editor back to global flags.");

        _debugFlagScreen.SelectGlobalFlagForValidation(OracleSaveData.GlobalFlagIntroDone);
        if (!_debugFlagScreen.RenderedText.Contains("GLOBALFLAG_INTRO_DONE", StringComparison.Ordinal))
            throw new InvalidOperationException(
                "The imported name for GLOBALFLAG_INTRO_DONE was not displayed.");
        bool globalBefore = _saveData.HasGlobalFlag(OracleSaveData.GlobalFlagIntroDone);
        _debugFlagScreen.ToggleSelectedFlag();
        if (_saveData.HasGlobalFlag(OracleSaveData.GlobalFlagIntroDone) == globalBefore)
            throw new InvalidOperationException("The global flag editor did not toggle flag $0a.");
        _debugFlagScreen.ToggleSelectedFlag();

        const int room = 0xaa;
        const int bit = 6;
        byte mask = 1 << bit;
        bool roomBefore = _saveData.HasRoomFlag(5, room, mask);
        _debugFlagScreen.SelectRoomFlagForValidation(7, room, bit);
        if (_debugFlagScreen.SelectedRoomGroup != 5 ||
            !_debugFlagScreen.RenderedText.Contains("GENERIC_40", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The room editor did not expose group 7 through canonical table 5 and bit 6.");
        }
        _debugFlagScreen.ToggleSelectedFlag();
        if (_saveData.HasRoomFlag(5, room, mask) == roomBefore)
            throw new InvalidOperationException("The room flag editor did not toggle 5:aa bit 6.");
        _debugFlagScreen.MoveHorizontal(1);
        if (_debugFlagScreen.SelectedRoom != 0xab)
            throw new InvalidOperationException("Room flag browsing did not advance $aa -> $ab.");
        _debugFlagScreen.MoveHorizontal(-1);
        _debugFlagScreen.ToggleSelectedFlag();

        _debugFlagMenu.CloseImmediatelyForValidation();
        if (_debugFlagMenu.IsActive || _debugFlagScreen.Visible ||
            !_player.IsPhysicsProcessing() || !_player.IsProcessing())
        {
            throw new InvalidOperationException(
                "Closing the debug flag menu did not restore Link processing.");
        }

        GD.Print("Validated F1 global/room flag editor, all imported global labels, " +
            "aliased room tables, navigation, mutation, and gameplay freezing.");
    }

    private void ValidateTimePortals()
    {
        var database = new TimePortalDatabase();
        var effectDatabase = new TimeWarpEffectDatabase();
        _saveData.SetGlobalFlag(
            OracleSaveData.GlobalFlagEnterPastCutsceneDone,
            value: false);
        _enterPastCommandTrace = new ValidationCutsceneTrace();
        _roomEvents.CommandTraceSink = _enterPastCommandTrace;
        (int Even, int Odd)[] expectedMasks =
        {
            (0xdd, 0xff), (0xdd, 0xbb), (0x55, 0xbb), (0x55, 0xaa),
            (0x11, 0xaa), (0x11, 0x88), (0x00, 0x88), (0x00, 0x00)
        };
        if (effectDatabase.TimeWarpSprite != "spr_timeportal" ||
            effectDatabase.CommonSprite != "spr_common_sprites" ||
            effectDatabase.SparkleSprite !=
                "spr_triforce_sparkle_vineseed_bookofseals" ||
            effectDatabase.PrimaryTileBase != 0 || effectDatabase.PrimaryPalette != 0 ||
            effectDatabase.BeamPalette != 7 ||
            effectDatabase.TrailTileBase != 0x10 || effectDatabase.TrailPalette != 3 ||
            effectDatabase.ParticleTileBase != 0x1e || effectDatabase.ParticlePalette != 4 ||
            effectDatabase.SparkleTileBase != 0x0a || effectDatabase.SparklePalette != 2 ||
            effectDatabase.PrimaryPriority != 3 || effectDatabase.BeamPriority != 2 ||
            effectDatabase.TrailPriority != 1 || effectDatabase.ParticlePriority != 3 ||
            effectDatabase.SparklePriority != 1 ||
            effectDatabase.DissolveFrames != 48 ||
            effectDatabase.SourceEffectFrames != 120 ||
            effectDatabase.SourceTrailFrames != 60 ||
            effectDatabase.ArrivalWaitFrames != 30 ||
            effectDatabase.ArrivalEffectFrames != 16 ||
            effectDatabase.ArrivalFlickerFrames != 30 ||
            effectDatabase.Particles.Count != 8 ||
            effectDatabase.Particles[0] != new TimeWarpEffectDatabase.ParticleRecord(0x280, -4, 0) ||
            effectDatabase.Particles[7] != new TimeWarpEffectDatabase.ParticleRecord(0x240, 9, 3) ||
            effectDatabase.OutdoorBeamPalette.SequenceEqual(effectDatabase.IndoorBeamPalette) ||
            expectedMasks.Where((mask, index) =>
                RoomTransitionController.TimeWarpDissolveMaskForValidation(index) != mask).Any())
        {
            throw new InvalidOperationException(
                "Imported $dd/$2b/$84 time-warp graphics, priorities, timing, particles, palettes, or " +
                "$dd/$ff..$00/$00 dissolve masks changed.");
        }
        IReadOnlyList<TimePortalDatabase.PortalRecord> ordinaryRecords =
            database.GetRoomPortals(0, 0x3a);
        if (ordinaryRecords.Count != 1 || ordinaryRecords[0].SubId != 0x00 ||
            ordinaryRecords[0].X != 0x18 || ordinaryRecords[0].Y != 0x28)
        {
            throw new InvalidOperationException(
                "Room 0:3a did not preserve its ordinary `$e1:$00 portal at `$21.");
        }

        LoadValidationRoom(0, 0x3a);
        List<TimePortal> ordinaryPortals = _entities.Entities<TimePortal>();
        if (ordinaryPortals.Count != 1 || !ordinaryPortals[0].Active ||
            _currentRoom.GetMetatile(ordinaryPortals[0].Position) != 0xd7)
        {
            throw new InvalidOperationException(
                "The exposed `$d7 marker in room 0:3a did not create an active ordinary portal.");
        }

        IReadOnlyList<TimePortalDatabase.PortalRecord> records = database.GetRoomPortals(0, 0x39);
        if (records.Count != 1 || records[0].SubId != 0x01 ||
            records[0].X != 0x28 || records[0].Y != 0x28 || records[0].LoopStart != 3)
        {
            throw new InvalidOperationException(
                "Room 0:39 did not preserve its `$e1:$01 portal at `$22 with animation loop index 3.");
        }

        LoadValidationRoom(0, 0x39);
        bool sourceUsesIndoorBeamPalette =
            (_currentRoom.TilesetFlags & 0x80) != 0;
        bool destinationUsesIndoorBeamPalette =
            (_rooms.GetRoom(1, 0x39).TilesetFlags & 0x80) != 0;
        if (sourceUsesIndoorBeamPalette || !destinationUsesIndoorBeamPalette)
        {
            throw new InvalidOperationException(
                "Canonical room 0:39 -> 1:39 no longer crosses from the outdoor " +
                "PALH_c1 source classification to the indoor PALH_c2 destination classification.");
        }
        List<TimePortal> portals = _entities.Entities<TimePortal>();
        if (portals.Count != 1 || portals[0].Active ||
            _currentRoom.GetMetatile(portals[0].Position) != 0x3a)
        {
            throw new InvalidOperationException(
                $"Room 0:39 portal initial state was count={portals.Count}, " +
                $"active={(portals.Count == 1 && portals[0].Active)}, tile=" +
                $"`${(portals.Count == 1 ? _currentRoom.GetMetatile(portals[0].Position) : 0):x2}.");
        }

        TimePortal portal = portals[0];
        if (!_currentRoom.ReplaceMetatile(portal.Position, 0x3a, 0xd7, (long)_animationTicks))
            throw new InvalidOperationException("Could not reveal portal-spot metatile `$d7 for validation.");
        _roomView.QueueRedraw();
        _entities.Update(1.0 / 60.0, _player);
        if (!portal.Active)
            throw new InvalidOperationException("The `$e1 portal did not initialize after its `$d7 spot was revealed.");
        for (int frame = 0; frame < 6; frame++)
            _entities.Update(1.0 / 60.0, _player);
        if (portal.CurrentFrame != 3)
            throw new InvalidOperationException("The portal did not finish its three 2-update intro frames.");
        for (int frame = 0; frame < 6; frame++)
            _entities.Update(1.0 / 60.0, _player);
        if (portal.CurrentFrame != 3)
            throw new InvalidOperationException("The portal's three-frame animation loop restarted incorrectly.");

        _player.WarpTo(portal.Position, recordSafe: false);
        _player.Face(Vector2I.Left);
        // Enter from a composite non-neutral pose. The sword previously hid a
        // stale pushing flag which became visible as soon as portal entry
        // cancelled the sword.
        _player.SetCutscenePushing(true);
        _player.StartSwordAttackForValidation(Vector2.Left);
        _sound.ClearPlayRequestAudit();
        _entities.Update(1.0 / 60.0, _player);
        if (!IsTransitioning || portal.Visible || _player.Position != portal.Position ||
            _player.FacingVector != Vector2I.Down || _player.Walking || _player.IsPushing ||
            _player.IsAttacking || _player.IsHoldingItemOneHand || _sound.ActiveMusic != 0 ||
            _transitions.TimeWarpPhaseName != "TimeWarpInitialize")
        {
            throw new InvalidOperationException(
                "interactionBeginTimewarp did not delete the portal, center Link in a neutral " +
                "down-facing pose, restart sound, and trigger CUTSCENE_TIMEWARP.");
        }

        // A long rendered frame must still service only state 0. In
        // particular, first-use shader compilation cannot collapse the 48
        // graphics-buffer updates into one visible frame.
        _transitions.UpdateWarp(5.0 / 60.0);
        if (_transitions.TimeWarpPhaseName != "TimeWarpDissolve" ||
            _transitions.TimeWarpDissolveStep != 0 ||
            _transitions.TimeWarpDissolveBufferStep != -1 ||
            _transitions.TimeWarpAppliedDissolveStep != -1 || _hud.Visible ||
            !_player.Visible ||
            !Mathf.IsEqualApprox(
                _roomView.BackgroundFadeAlpha,
                1.0f / RoomTransitionController.FastPaletteFadeFrames))
        {
            throw new InvalidOperationException(
                "CUTSCENE_TIMEWARP state 0 did not hide the HUD, start the BG-only fast black " +
                "fade, preserve a long rendered frame, and prepare the still-unmasked " +
                "six-buffer dissolve pass.");
        }
        UpdateRoomWarpTransition(5.0 / 60.0);
        if (_transitions.TimeWarpDissolveStep != 0 ||
            _transitions.TimeWarpDissolveBufferStep != 4 ||
            _transitions.TimeWarpAppliedDissolveStep != -1 || !_player.Visible)
        {
            throw new InvalidOperationException(
                "Link was hidden by the first five non-Link object/common graphics buffer updates.");
        }
        UpdateRoomWarpTransition(1.0 / 60.0);
        if (_transitions.TimeWarpDissolveBufferStep != 5 ||
            _transitions.TimeWarpAppliedDissolveStep != 0 || !_player.Visible)
        {
            throw new InvalidOperationException(
                "The final bank-6 object/companion pass did not commit mask $dd/$ff " +
                "without masking Link.");
        }
        UpdateRoomWarpTransition(1.0 / 60.0);
        if (_transitions.TimeWarpDissolveStep != 1 ||
            _transitions.TimeWarpDissolveBufferStep != 0 ||
            _transitions.TimeWarpAppliedDissolveStep != 0 || !_player.Visible)
        {
            throw new InvalidOperationException(
                "The second $dd/$bb non-Link buffer cycle did not begin on update 7.");
        }
        UpdateRoomWarpTransition(41.0 / 60.0);
        if (_transitions.TimeWarpPhaseName != "TimeWarpSetup" ||
            _transitions.TimeWarpDissolveStep != 7 ||
            _transitions.TimeWarpDissolveBufferStep != 5 ||
            _transitions.TimeWarpAppliedDissolveStep != 7 || !_player.Visible ||
            _entities.Entities<TimePortal>().Count != 0 ||
            !Mathf.IsEqualApprox(_roomView.BackgroundFadeAlpha, 1.0f))
        {
            throw new InvalidOperationException(
                "The eight six-buffer object-graphics masks did not make non-Link objects " +
                "transparent over black, retain Link, and clear the source objects.");
        }

        UpdateRoomWarpTransition(1.0 / 60.0);
        if (_transitions.ActiveTimeWarpEffect is not null ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndTimewarpInitiated) != 0 ||
            !_player.Visible)
        {
            throw new InvalidOperationException("The tilemap reload substep created the source effect early.");
        }
        UpdateRoomWarpTransition(1.0 / 60.0);
        TimeWarpEffect sourceEffect = _transitions.ActiveTimeWarpEffect ??
            throw new InvalidOperationException("INTERAC_TIMEWARP $dd:$00 was not created.");
        if (_transitions.TimeWarpPhaseName != "TimeWarpSourceEffect" ||
            !sourceEffect.PrimaryVisible || !_player.Visible ||
            sourceEffect.BackgroundZIndex != NpcCharacter.BehindLinkZIndex ||
            sourceEffect.ForegroundZIndex != NpcCharacter.InFrontOfLinkZIndex ||
            sourceEffect.UsesIndoorBeamPalette != sourceUsesIndoorBeamPalette ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndTimewarpInitiated) != 1)
        {
            throw new InvalidOperationException(
                "The source effect did not begin with priority-3 ground below Link, its " +
                "priority-2 beam layer above Link, and SND_TIMEWARP_INITIATED $d1.");
        }
        UpdateRoomWarpTransition(119.0 / 60.0);
        if (_transitions.TimeWarpPhaseName != "TimeWarpSourceEffect" ||
            !sourceEffect.PrimaryVisible || !sourceEffect.BeamVisible || !_player.Visible ||
            sourceEffect.ParticleSpawnCount != 10)
        {
            throw new InvalidOperationException(
                "The source $dd:$00 effect or intact Link ended before the 120-count handoff.");
        }
        UpdateRoomWarpTransition(1.0 / 60.0);
        if (_transitions.TimeWarpPhaseName != "TimeWarpSourceTrail" ||
            !sourceEffect.PrimaryVisible || !sourceEffect.BeamVisible ||
            sourceEffect.BeamContracting || !sourceEffect.TrailVisible ||
            sourceEffect.SourceCounter != 24 ||
            sourceEffect.ParticleSpawnCount != 10 || _player.Visible)
        {
            throw new InvalidOperationException(
                "The cutscene's 120-count handoff did not delete w1Link, retain the " +
                "$dd:$00 interaction's final 24 counts and purple child, emit ten $2b " +
                "particles, and create $dd:$02.");
        }
        UpdateRoomWarpTransition(12.0 / 60.0);
        if (sourceEffect.SparkleSpawnCount != 1 || sourceEffect.ActiveSparkleCount != 1 ||
            sourceEffect.SourceCounter != 12 || sourceEffect.BeamContracting)
        {
            throw new InvalidOperationException(
                "The rising -$0400 trail did not create INTERAC_SPARKLE $84:$01 after six moves.");
        }
        UpdateRoomWarpTransition(11.0 / 60.0);
        if (sourceEffect.SourceCounter != 1 || sourceEffect.BeamContracting ||
            !sourceEffect.PrimaryVisible || !sourceEffect.BeamVisible)
        {
            throw new InvalidOperationException(
                "The source ground or purple child began contracting before $dd:$00 counter1 reached zero.");
        }
        UpdateRoomWarpTransition(1.0 / 60.0);
        if (sourceEffect.SourceCounter != 0 || !sourceEffect.BeamContracting ||
            sourceEffect.BeamFrameIndex != 0 || !sourceEffect.PrimaryVisible ||
            !sourceEffect.BeamVisible)
        {
            throw new InvalidOperationException(
                "$dd:$00 counter1 zero did not select ground animation $01 and the purple " +
                "child's horizontal-fold animation $04 on source-trail update 24.");
        }
        UpdateRoomWarpTransition(10.0 / 60.0);
        if (!sourceEffect.BeamVisible || sourceEffect.BeamFrameIndex != 10)
        {
            throw new InvalidOperationException(
                "The source purple child did not retain all 11 visible animation-$04 fold frames.");
        }
        UpdateRoomWarpTransition(1.0 / 60.0);
        if (sourceEffect.BeamVisible || !sourceEffect.PrimaryVisible)
        {
            throw new InvalidOperationException(
                "The source purple child did not delete immediately after its 11-update horizontal fold.");
        }
        UpdateRoomWarpTransition(13.0 / 60.0);
        if (sourceEffect.PrimaryVisible)
        {
            throw new InvalidOperationException(
                "The source ground did not finish its independent 24-update contraction.");
        }
        UpdateRoomWarpTransition(12.0 / 60.0);
        if (_transitions.TimeWarpPhaseName != "TimeWarpBlackFadeIn" ||
            sourceEffect.BeamVisible || sourceEffect.PrimaryVisible || _player.Visible)
        {
            throw new InvalidOperationException(
                "The source trail did not hold for exactly 60 updates after both portal " +
                "components had collapsed and vanished.");
        }
        UpdateRoomWarpTransition(RoomTransitionController.FastPaletteFadeFrames / 60.0);
        if (_transitions.TimeWarpPhaseName != "TimeWarpWhiteFadeOut" ||
            _transitions.TimeWarpPhaseFrame != 1 ||
            !Mathf.IsEqualApprox(_roomView.BackgroundFadeAlpha, 1.0f) ||
            !Mathf.IsEqualApprox(_warpFade.Color.A, 1.0f / WarpFadeFrames) ||
            _player.Visible)
        {
            throw new InvalidOperationException(
                "The source tilemap was not kept black-covered while the palette handoff " +
                "started the first fadeoutToWhite step.");
        }
        UpdateRoomWarpTransition((WarpFadeFrames - 2.0f) / 60.0);
        if (_transitions.TimeWarpPhaseName != "TimeWarpWhiteFadeOut" ||
            _transitions.TimeWarpPhaseFrame != WarpFadeFrames - 1 ||
            !Mathf.IsEqualApprox(_roomView.BackgroundFadeAlpha, 1.0f) ||
            !Mathf.IsEqualApprox(
                _warpFade.Color.A, (WarpFadeFrames - 1.0f) / WarpFadeFrames) ||
            _activeGroup != 0)
        {
            throw new InvalidOperationException(
                "The source tilemap became visible before the white overlay reached opacity.");
        }
        UpdateRoomWarpTransition(1.0 / 60.0);
        if (_activeGroup != 1 || _currentRoom.Id != 0x39 ||
            _currentRoom.GetPackedPosition(_player.Position) != 0x22 ||
            _player.Visible || !_hud.Visible ||
            _transitions.TimeWarpPhaseName != "TimeWarpArrivalFadeIn" ||
            _transitions.TimeWarpDissolveStep != -1)
        {
            throw new InvalidOperationException(
                $"Time portal 0:39/`$22 landed at {_activeGroup:x1}:{_currentRoom.Id:x2}/" +
                $"`${_currentRoom.GetPackedPosition(_player.Position):x2} instead of 1:39/`$22.");
        }

        UpdateRoomWarpTransition(WarpFadeFrames / 60.0);
        if (_transitions.TimeWarpPhaseName != "TimeWarpArrivalWait")
            throw new InvalidOperationException("The destination did not fade in from white for 32 updates.");
        UpdateRoomWarpTransition(30.0 / 60.0);
        TimeWarpEffect arrivalEffect = _transitions.ActiveTimeWarpEffect ??
            throw new InvalidOperationException("Destination INTERAC_TIMEWARP $dd:$01 was not created.");
        if (_transitions.TimeWarpPhaseName != "TimeWarpArrivalEffect" || _player.Visible ||
            !arrivalEffect.PrimaryVisible ||
            arrivalEffect.BackgroundZIndex != NpcCharacter.BehindLinkZIndex ||
            arrivalEffect.ForegroundZIndex != NpcCharacter.InFrontOfLinkZIndex ||
            arrivalEffect.UsesIndoorBeamPalette != sourceUsesIndoorBeamPalette ||
            ((_currentRoom.TilesetFlags & 0x80) != 0) !=
                destinationUsesIndoorBeamPalette)
        {
            throw new InvalidOperationException(
                "The hidden 30-update arrival wait did not create the destination effect " +
                "with the source-carried wcc50 beam palette variant.");
        }
        UpdateRoomWarpTransition(15.0 / 60.0);
        if (_player.Visible ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndTimewarpCompleted) != 0)
        {
            throw new InvalidOperationException("Link or SND_TIMEWARP_COMPLETED appeared before update 16.");
        }
        UpdateRoomWarpTransition(1.0 / 60.0);
        if (!_player.Visible ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndTimewarpCompleted) != 1 ||
            _transitions.TimeWarpPhaseName != "TimeWarpArrivalFlicker" ||
            !arrivalEffect.BeamVisible || arrivalEffect.BeamContracting)
        {
            throw new InvalidOperationException(
                "Destination update 16 did not reveal Link and play SND_TIMEWARP_COMPLETED $d4.");
        }

        int invisibleFlickerFrames = 0;
        for (int frame = 0; frame < 4; frame++)
        {
            UpdateRoomWarpTransition(1.0 / 60.0);
            if (!_player.Visible)
                invisibleFlickerFrames++;
        }
        if (invisibleFlickerFrames != 1)
        {
            throw new InvalidOperationException(
                "Destination objectFlickerVisibility b=$03 was not visible on three of four updates.");
        }
        UpdateRoomWarpTransition(4.0 / 60.0);
        if (!arrivalEffect.BeamContracting || !arrivalEffect.BeamVisible ||
            arrivalEffect.BeamFrameIndex != 0 || !arrivalEffect.PrimaryVisible)
        {
            throw new InvalidOperationException(
                "The completed $dd:$01 expansion did not start ground animation $01 and " +
                "purple-child animation $04 with their first contraction frames intact.");
        }
        UpdateRoomWarpTransition(11.0 / 60.0);
        if (arrivalEffect.BeamVisible || !arrivalEffect.PrimaryVisible)
        {
            throw new InvalidOperationException(
                "The purple $dd:$04 child did not collapse for 11 updates and delete itself " +
                "before the slower ground contraction.");
        }
        UpdateRoomWarpTransition(11.0 / 60.0);
        if (IsTransitioning || !_player.Visible ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndTimewarpCompleted) != 1 ||
            !_player.CutsceneControlled || !_roomEvents.EnterPast.HasState ||
            _roomEvents.EnterPast.Stage != EnterPastEvent.EventStage.PreJumpWait ||
            _roomEvents.EnterPast.Counter !=
                _roomEvents.EnterPast.Record.ExpectedArrivalCounter)
        {
            throw new InvalidOperationException(
                "The 30-update arrival flicker did not hand off to the partially elapsed " +
                "room 1:39 first-arrival script.");
        }

        GD.Print("Validated all 21 `$e1 portal records and the complete 0:39 -> 1:39 " +
            "CUTSCENE_TIMEWARP: centered Link, sound restart, 8x6 non-Link sprite dissolve, " +
            "intact-until-120 source Link, priority-3 ground below Link, priority-2/1 beam " +
            "and trail above Link, source update-24 horizontal beam fold, 11-update " +
            "source/arrival beam contraction, neutral down-facing Link on contact, " +
            "source-carried PALH_c1/c2 palette, hidden HUD, " +
            "$dd/$2b/$84 source effects, map-masked black-to-white fade, $d1/$d4 sounds, " +
            "and 30/16/30 arrival.");
    }

    private void ValidateEnterPastEvent()
    {
        EnterPastEvent enterPast = _roomEvents.EnterPast;
        EnterPastEventDatabase.EnterPastEventRecord record = enterPast.Record;
        NpcCharacter villager = _npcNodes.Find(npc =>
            npc.Record.Id == record.InteractionId && npc.Record.SubId == record.SubId) ??
            throw new InvalidOperationException(
                "Room 1:39 did not create INTERAC_MALE_VILLAGER $3a:$0d.");

        if (_activeGroup != record.Group || _currentRoom.Id != record.Room ||
            record.IntroWaitFrames != 100 || record.PreJumpWaitFrames != 40 ||
            record.PostJumpWaitFrames != 30 || record.PostTextWaitFrames != 30 ||
            record.JumpSpeedZ != -0x200 || record.JumpGravity != 0x30 ||
            record.FastSpeed != 0x28 || record.SlowSpeed != 0x14 ||
            record.FirstDownCounter != 0x11 || record.RightCounter != 0x11 ||
            record.SecondDownCounter != 0x09 || record.SlowDownCounter != 0x21 ||
            record.FinalDownCounter != 0x39 || record.TextId != 0x1622 ||
            record.JumpSound != OracleSoundEngine.SndJump ||
            record.GlobalFlag != OracleSaveData.GlobalFlagEnterPastCutsceneDone ||
            !enterPast.HasState || enterPast.Completed ||
            enterPast.Stage != EnterPastEvent.EventStage.PreJumpWait ||
            enterPast.Counter != record.ExpectedArrivalCounter ||
            villager.Position != new Vector2(0x18, 0x28) || !villager.Active ||
            !_player.CutsceneControlled)
        {
            throw new InvalidOperationException(
                "The portal handoff did not preserve the imported first-arrival actor, " +
                "script values, initial position, or wait overlap.");
        }

        _sound.ClearPlayRequestAudit();
        StepRoomEventFrames(record.ExpectedArrivalCounter - 1);
        if (enterPast.Counter != 1 || enterPast.Stage != EnterPastEvent.EventStage.PreJumpWait ||
            enterPast.ZFixed != 0 || _sound.PlayRequestsFor(record.JumpSound) != 0)
        {
            throw new InvalidOperationException(
                "The remaining pre-jump wait ended early after the time-warp arrival.");
        }
        StepRoomEventFrames(1);
        if (enterPast.Stage != EnterPastEvent.EventStage.BeginJump ||
            _sound.PlayRequestsFor(record.JumpSound) != 0)
        {
            throw new InvalidOperationException(
                "wait 40 did not return to jumpAndWaitUntilLanded on its zero update.");
        }
        StepRoomEventFrames(1);
        if (enterPast.Stage != EnterPastEvent.EventStage.Jump ||
            enterPast.ZFixed != -0x200 || villager.ScriptDrawOffset.Y != -2 ||
            _sound.PlayRequestsFor(record.JumpSound) != 1)
        {
            throw new InvalidOperationException(
                "beginJump did not apply speedZ -$0200 and SND_JUMP $53 on its own update.");
        }
        StepRoomEventFrames(21);
        if (enterPast.ZFixed != -0xb0 || villager.ScriptDrawOffset.Y != -1)
        {
            throw new InvalidOperationException(
                "The villager jump diverged before its 23rd $30-gravity update.");
        }
        StepRoomEventFrames(1);
        if (enterPast.Stage != EnterPastEvent.EventStage.InstallPostJumpWait ||
            enterPast.ZFixed != 0 || villager.ScriptDrawOffset != Vector2.Zero)
        {
            throw new InvalidOperationException(
                "The villager did not land on the 23rd gravity update.");
        }

        StepRoomEventFrames(1);
        StepRoomEventFrames(record.PostJumpWaitFrames - 1);
        if (enterPast.Counter != 1 || _dialogue.IsOpen)
            throw new InvalidOperationException("The post-jump wait ended early.");
        StepRoomEventFrames(1);
        const string expectedText =
            "Another one?!?\nFirst, that guy\nwith the weird\nhat appears,\nthen you...\n" +
            "Ever since that\ngirl Nayru came,\nthere's been all\nsorts o' weird\ngoings on!";
        if (!_dialogue.IsOpen || _dialogue.CurrentMessage != expectedText ||
            !record.Text.Contains("\\stop", StringComparison.Ordinal) ||
            !record.Text.Contains("\\col(3)Nayru\\col(0)", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "TX_1622 did not retain its stop command, blue Nayru span, and exact text.");
        }

        _dialogue.Close();
        StepRoomEventFrames(1);
        if (enterPast.Stage != EnterPastEvent.EventStage.PostTextWait ||
            enterPast.Counter != record.PostTextWaitFrames)
        {
            throw new InvalidOperationException(
                "The script did not install its post-text wait on the first update after closing TX_1622.");
        }
        StepRoomEventFrames(record.PostTextWaitFrames - 1);
        if (enterPast.Counter != 1 || villager.Position != new Vector2(0x18, 0x28))
            throw new InvalidOperationException("The post-text wait or stationary position ended early.");
        StepRoomEventFrames(1);
        if (enterPast.Stage != EnterPastEvent.EventStage.StartFirstDown)
            throw new InvalidOperationException("setspeed SPEED_100 lost its script-command update.");

        StepRoomEventFrames(1);
        if (enterPast.Stage != EnterPastEvent.EventStage.FirstDown ||
            enterPast.Counter != record.FirstDownCounter ||
            villager.CurrentScriptAnimationSource != record.DownAnimation)
        {
            throw new InvalidOperationException(
                "movedown $11 did not install its counter and down animation.");
        }
        StepRoomEventFrames(6);
        if (villager.Position != new Vector2(0x18, 0x2e) ||
            enterPast.Counter != 0x0b || villager.CurrentAnimationFrame != 0)
        {
            throw new InvalidOperationException(
                "The first SPEED_100 leg or doubled animation cadence diverged before update 7.");
        }
        StepRoomEventFrames(1);
        if (villager.Position != new Vector2(0x18, 0x2f) ||
            enterPast.Counter != 0x0a || villager.CurrentAnimationFrame != 1)
        {
            throw new InvalidOperationException(
                "interactionAnimateBasedOnSpeed did not advance twice at SPEED_100.");
        }
        StepRoomEventFrames(9);
        if (villager.Position != new Vector2(0x18, 0x38) || enterPast.Counter != 1)
            throw new InvalidOperationException("movedown $11 did not move exactly 16 pixels.");
        StepRoomEventFrames(1);
        if (enterPast.Stage != EnterPastEvent.EventStage.FirstDown ||
            enterPast.CurrentCommandIndex != 10 || enterPast.CurrentCommandUpdates != 0 ||
            enterPast.Counter != 0 ||
            villager.CurrentScriptAnimationSource != record.DownAnimation)
        {
            throw new InvalidOperationException(
                "movedown $11's counter2-zero update incorrectly dispatched moveright.");
        }
        StepRoomEventFrames(1);
        if (enterPast.Stage != EnterPastEvent.EventStage.Right ||
            enterPast.Counter != record.RightCounter ||
            villager.CurrentScriptAnimationSource != record.RightAnimation)
        {
            throw new InvalidOperationException(
                "moveright $11 did not start on the update after counter2 reached zero.");
        }
        StepRoomEventFrames(record.RightCounter - 1);
        if (villager.Position != new Vector2(0x28, 0x38) || enterPast.Counter != 1)
            throw new InvalidOperationException("moveright $11 did not move exactly 16 pixels.");
        StepRoomEventFrames(1);
        if (enterPast.Stage != EnterPastEvent.EventStage.Right ||
            enterPast.CurrentCommandIndex != 11 || enterPast.CurrentCommandUpdates != 0 ||
            enterPast.Counter != 0 ||
            villager.CurrentScriptAnimationSource != record.RightAnimation)
        {
            throw new InvalidOperationException(
                "moveright $11's counter2-zero update incorrectly dispatched movedown.");
        }
        StepRoomEventFrames(1);
        if (enterPast.Stage != EnterPastEvent.EventStage.SecondDown ||
            enterPast.Counter != record.SecondDownCounter ||
            villager.CurrentScriptAnimationSource != record.DownAnimation)
        {
            throw new InvalidOperationException(
                "movedown $09 did not start on the update after counter2 reached zero.");
        }
        StepRoomEventFrames(record.SecondDownCounter - 1);
        if (villager.Position != new Vector2(0x28, 0x40) || enterPast.Counter != 1)
            throw new InvalidOperationException("movedown $09 did not move exactly eight pixels.");
        StepRoomEventFrames(1);
        if (enterPast.Stage != EnterPastEvent.EventStage.StartSlowDown ||
            enterPast.CurrentCommandIndex != 12 || enterPast.CurrentCommandUpdates != 0 ||
            enterPast.Counter != 0)
        {
            throw new InvalidOperationException(
                "movedown $09's counter2-zero update incorrectly dispatched SPEED_080.");
        }
        StepRoomEventFrames(1);
        if (enterPast.Stage != EnterPastEvent.EventStage.StartSlowDown ||
            enterPast.CurrentCommandIndex != 13 || enterPast.CurrentCommandUpdates != 0)
        {
            throw new InvalidOperationException("setspeed SPEED_080 lost its script-command update.");
        }
        StepRoomEventFrames(1);
        if (enterPast.Stage != EnterPastEvent.EventStage.SlowDown ||
            enterPast.Counter != record.SlowDownCounter)
        {
            throw new InvalidOperationException("applyspeed $21 lost its script-command update.");
        }
        StepRoomEventFrames(record.SlowDownCounter - 1);
        if (villager.Position != new Vector2(0x28, 0x50) || enterPast.Counter != 1)
        {
            throw new InvalidOperationException(
                "SPEED_080 applyspeed $21 did not move exactly 16 pixels.");
        }
        StepRoomEventFrames(1);
        if (enterPast.Stage != EnterPastEvent.EventStage.StartFinalDown ||
            enterPast.CurrentCommandIndex != 14 || enterPast.CurrentCommandUpdates != 0 ||
            enterPast.Counter != 0)
        {
            throw new InvalidOperationException(
                "applyspeed $21's counter2-zero update incorrectly dispatched SPEED_100.");
        }
        StepRoomEventFrames(1);
        if (enterPast.Stage != EnterPastEvent.EventStage.StartFinalDown ||
            enterPast.CurrentCommandIndex != 15 || enterPast.CurrentCommandUpdates != 0)
        {
            throw new InvalidOperationException("The final SPEED_100 command lost its own update.");
        }
        StepRoomEventFrames(1);
        if (enterPast.Stage != EnterPastEvent.EventStage.FinalDown ||
            enterPast.Counter != record.FinalDownCounter)
        {
            throw new InvalidOperationException("The final applyspeed $39 lost its command update.");
        }
        StepRoomEventFrames(record.FinalDownCounter - 1);
        if (villager.Position != new Vector2(0x28, 0x88) || enterPast.Counter != 1 ||
            enterPast.Completed || !villager.Active)
        {
            throw new InvalidOperationException(
                "The final SPEED_100 applyspeed $39 path did not cover 56 pixels.");
        }
        StepRoomEventFrames(1);
        if (!enterPast.HasState || enterPast.Completed || !villager.Active ||
            enterPast.CurrentCommandIndex != 16 || enterPast.CurrentCommandUpdates != 0 ||
            enterPast.Counter != 0 || !_player.CutsceneControlled)
        {
            throw new InvalidOperationException(
                "applyspeed $39's counter2-zero update incorrectly completed the script.");
        }
        StepRoomEventFrames(1);
        if (enterPast.HasState || !enterPast.Completed || villager.Active ||
            _player.CutsceneControlled ||
            !_saveData.HasGlobalFlag(OracleSaveData.GlobalFlagEnterPastCutsceneDone) ||
            _sound.PlayRequestsFor(record.JumpSound) != 1)
        {
            throw new InvalidOperationException(
                "The first-past-arrival script did not set flag $41, delete the villager, and restore input.");
        }

        LoadValidationRoom(record.Group, record.Room);
        NpcCharacter? completedVillager = _npcNodes.Find(npc =>
            npc.Record.Id == record.InteractionId && npc.Record.SubId == record.SubId);
        if (completedVillager is null || completedVillager.Active || enterPast.HasState ||
            _player.CutsceneControlled)
        {
            throw new InvalidOperationException(
                "villagerSubid0dScript did not redirect to stubScript and delete on re-entry.");
        }

        ValidationCutsceneTrace commandTrace = _enterPastCommandTrace ??
            throw new InvalidOperationException(
                "The first-past-arrival command trace was not installed before time-warp arrival.");
        CutsceneCommandTraceEntry[] starts = commandTrace.Entries
            .Where(entry => entry.Phase == CutsceneCommandTracePhase.Started)
            .ToArray();
        string[] expectedOpcodes =
        {
            "setdisabledobjects", "wait", "disableinput", "wait", "jump",
            "wait", "showtext", "wait", "setspeed", "move", "move", "move",
            "setspeed", "applyspeed", "setspeed", "applyspeed",
            "setglobalflag", "enableinput", "scriptend"
        };
        if (starts.Length != expectedOpcodes.Length ||
            starts.Where((entry, index) =>
                entry.Source.Script != "villagerSubid0dScript" ||
                entry.Source.Label != "villagerSubid0dScript" ||
                entry.Source.CommandIndex != index ||
                entry.Source.Opcode != expectedOpcodes[index] ||
                entry.Source.SourceLine <= 0 ||
                (index > 0 && entry.Source.SourceLine <= starts[index - 1].Source.SourceLine))
            .Any())
        {
            throw new InvalidOperationException(
                "The imported first-past-arrival trace lost source lines, command order, " +
                "or typed opcodes.");
        }

        int CompletedUpdate(int commandIndex) => commandTrace.Entries.Single(entry =>
            entry.Source.CommandIndex == commandIndex &&
            entry.Phase == CutsceneCommandTracePhase.Completed).ScriptUpdate;
        if (starts[3].ScriptUpdate != starts[2].ScriptUpdate ||
            starts[6].ScriptUpdate != CompletedUpdate(5) ||
            starts[10].ScriptUpdate != CompletedUpdate(9) + 1 ||
            starts[11].ScriptUpdate != CompletedUpdate(10) + 1 ||
            starts[12].ScriptUpdate != CompletedUpdate(11) + 1 ||
            starts[13].ScriptUpdate != starts[12].ScriptUpdate + 1 ||
            starts[14].ScriptUpdate != CompletedUpdate(13) + 1 ||
            starts[15].ScriptUpdate != starts[14].ScriptUpdate + 1 ||
            starts[16].ScriptUpdate != CompletedUpdate(15) + 1 ||
            starts[17].ScriptUpdate != starts[16].ScriptUpdate ||
            starts[18].ScriptUpdate != starts[16].ScriptUpdate)
        {
            throw new InvalidOperationException(
                "villagerSubid0dScript did not preserve carry-through commands, " +
                "counter2 zero-update yields, or the final same-update completion chain.");
        }
        _roomEvents.CommandTraceSink = null;
        _enterPastCommandTrace = null;

        GD.Print("Validated room 1:39's first time-portal arrival: transition-overlapped " +
            "100/40 waits, -$0200/$30 jump and SND_JUMP, TX_1622 controls, 30/30 waits, " +
            "$11/$11/$09/$21/$39 movement counters, SPEED_100/SPEED_080 animation cadence, " +
            "counter2 zero-update command boundaries, exact $18,$28 -> $28,$88 path, " +
            "imported source trace, input gating, deletion, and persistent flag $41.");
    }

    private void ValidateMapScreen()
    {
        if (!Mathf.IsEqualApprox(MapMenuController.FastFadeFrames, 11.0f))
            throw new InvalidOperationException("The map menu must use the 11-update fast palette fade.");
        if (MapScreen.GetSpriteShadeForValidation(Colors.Black, true) != 0 ||
            MapScreen.GetSpriteShadeForValidation(Color.Color8(85, 85, 85), true) != 1 ||
            MapScreen.GetSpriteShadeForValidation(Color.Color8(170, 170, 170), true) != 2 ||
            MapScreen.GetSpriteShadeForValidation(Colors.White, true) != 3 ||
            MapScreen.GetSpriteShadeForValidation(Colors.White, false) != 0 ||
            MapScreen.GetSpriteShadeForValidation(Colors.Black, false) != 3)
        {
            throw new InvalidOperationException(
                "Map OAM grayscale no longer handles inverted spr_ and normal gfx sheets separately.");
        }
        if (MapScreen.UsesInvertedSpriteGrayscale(MapScreen.MapMode.Present, 0x0e) ||
            MapScreen.UsesInvertedSpriteGrayscale(MapScreen.MapMode.Present, 0x22) ||
            MapScreen.UsesInvertedSpriteGrayscale(MapScreen.MapMode.Dungeon, 0x88) ||
            !MapScreen.UsesInvertedSpriteGrayscale(MapScreen.MapMode.Dungeon, 0x00))
        {
            throw new InvalidOperationException(
                "Map OAM sheets no longer honor spr_minimap_icons invert:false separately " +
                "from the default-inverted dungeon item sheet.");
        }
        if ((MapScreen.LocationArrowAttributes & 0x40) == 0)
            throw new InvalidOperationException("The map location arrow lost OAM Y-flip attribute $47.");

        LoadValidationRoom(0, 0x11);
        _mapMenu.OpenImmediatelyForValidation();
        if (_mapScreen.Mode != MapScreen.MapMode.Present || _mapScreen.CursorRoom != 0x11)
            throw new InvalidOperationException(
                $"Present map should open at room 11, got {_mapScreen.Mode} / {_mapScreen.CursorRoom:x2}.");
        if (!_gameplayPause.IsOwnedBy(_mapMenu) ||
            _player.IsPhysicsProcessing() || _player.IsProcessing())
            throw new InvalidOperationException("Link continued updating while the map menu was open.");

        _mapScreen.MoveOverworldCursor(Vector2I.Left);
        _mapScreen.MoveOverworldCursor(Vector2I.Left);
        if (_mapScreen.CursorRoom != 0x1d)
            throw new InvalidOperationException(
                $"The 14-column overworld cursor did not wrap 11 -> 10 -> 1d; got {_mapScreen.CursorRoom:x2}.");

        if (!_mapScreen.LocationArrowVisible)
            throw new InvalidOperationException("The map location arrow was hidden on frame 0.");
        _mapScreen.Update(32.0 / 60.0);
        if (_mapScreen.LocationArrowVisible)
            throw new InvalidOperationException("The map location arrow did not toggle after 32 updates.");
        _mapScreen.Update(32.0 / 60.0);
        if (!_mapScreen.LocationArrowVisible)
            throw new InvalidOperationException("The map location arrow did not restore after 64 updates.");
        _mapMenu.CloseImmediatelyForValidation();
        if (!_player.IsPhysicsProcessing() || !_player.IsProcessing())
            throw new InvalidOperationException("Link processing was not restored after closing the map.");

        LoadValidationRoom(0, 0x45);
        _mapMenu.OpenImmediatelyForValidation();
        if (!_mapScreen.TryGetSelectedAreaText(out MapDataDatabase.MapText areaText) ||
            areaText.TextId != 0x0343 || string.IsNullOrWhiteSpace(areaText.Message))
        {
            throw new InvalidOperationException(
                "Present room 45 did not resolve its imported TX_0343 map text.");
        }
        _mapScreen.Update(7.0 / 60.0);
        if (_mapScreen.PopupPrimary != 0x01 || _mapScreen.PopupSize != 4)
        {
            throw new InvalidOperationException(
                $"Room 45's house popup did not expand in 7 updates: " +
                $"icon={_mapScreen.PopupPrimary:x2}, size={_mapScreen.PopupSize}.");
        }
        _mapMenu.CloseImmediatelyForValidation();

        LoadValidationRoom(1, 0x11);
        _mapMenu.OpenImmediatelyForValidation();
        if (_mapScreen.Mode != MapScreen.MapMode.Past || _mapScreen.CursorRoom != 0x11)
            throw new InvalidOperationException(
                $"Past map should open at room 11, got {_mapScreen.Mode} / {_mapScreen.CursorRoom:x2}.");
        _mapMenu.CloseImmediatelyForValidation();

        DungeonMapDatabase.DungeonInfo dungeon = _rooms.DungeonMaps.GetDungeon(0x0d);
        if (!dungeon.TryGetRoom(0x09, out DungeonMapDatabase.DungeonCell cell) ||
            cell.Floor != 0 || cell.X != 2 || cell.Y != 2)
        {
            throw new InvalidOperationException(
                "Dungeon 0d room 09 was not imported at floor 0, cell (2,2).");
        }
        LoadValidationRoom(4, 0x09);
        _mapMenu.OpenImmediatelyForValidation();
        if (_mapScreen.Mode != MapScreen.MapMode.Dungeon || _mapScreen.DisplayedDungeonFloor != 0)
            throw new InvalidOperationException("Dungeon 0d map did not open on room 09's floor.");
        _mapMenu.CloseImmediatelyForValidation();

        byte oldCompasses = _saveData.ReadWramByte(0xc684);
        byte oldMaps = _saveData.ReadWramByte(0xc686);
        byte oldRoom30Flags = _saveData.GetRoomFlags(4, 0x30);
        _saveData.WriteWramByte(0xc684, (byte)(oldCompasses | 0x04));
        _saveData.WriteWramByte(0xc686, (byte)(oldMaps | 0x04));
        _saveData.SetRoomFlag(4, 0x30, OracleSaveData.RoomFlagItem, value: false);
        var mapInventory = new InventoryState(_treasures, _saveData);
        _mapScreen.Initialize(_rooms, mapInventory);
        LoadValidationRoom(4, 0x46);
        _mapMenu.OpenImmediatelyForValidation();
        DungeonMapDatabase.DungeonInfo dungeon02 = _rooms.DungeonMaps.GetDungeon(0x02);
        if (dungeon02.CompassFloors != 0x01 ||
            _mapScreen.DisplayedDungeonFloor != 1 || _mapScreen.DungeonTileAt(6, 1) != 0x83)
        {
            throw new InvalidOperationException(
                "Dungeon 02's map/compass did not reveal its imported boss room and floor mask.");
        }
        _mapScreen.SelectDungeonFloorForValidation(0);
        if (_mapScreen.DisplayedDungeonFloor != 0 || _mapScreen.DungeonTileAt(5, 1) != 0xae)
        {
            throw new InvalidOperationException(
                "Dungeon 02's compass did not reveal the unopened room 30 treasure on floor 0.");
        }
        _mapMenu.CloseImmediatelyForValidation();
        _saveData.WriteWramByte(0xc684, oldCompasses);
        _saveData.WriteWramByte(0xc686, oldMaps);
        _saveData.SetRoomFlag(4, 0x30, 0xff, value: false);
        if (oldRoom30Flags != 0)
            _saveData.SetRoomFlag(4, 0x30, oldRoom30Flags);
        _mapScreen.Initialize(_rooms, _inventory);
        LoadValidationRoom(4, 0x09);

        _mapMenu.OpenDebugImmediatelyForValidation();
        if (!_mapScreen.DebugFastTravel || _mapScreen.Mode != MapScreen.MapMode.Past)
            throw new InvalidOperationException(
                "Debug fast travel did not retain the most recent past-overworld map from dungeon 0d.");
        _mapScreen.ToggleDebugWorld();
        if (_mapScreen.Mode != MapScreen.MapMode.Present)
            throw new InvalidOperationException("Debug fast travel did not switch from past to present.");
        _mapScreen.MoveOverworldCursor(Vector2I.Right);
        if (!_mapScreen.TryGetFastTravelTarget(out int group, out int room) ||
            group != 0 || room != 0x12)
        {
            throw new InvalidOperationException(
                $"Expected present fast-travel target 0:12, got {group}:{room:x2}.");
        }
        if (!_mapMenu.BeginTravelToSelectionForValidation())
            throw new InvalidOperationException("Debug map rejected the valid present target 0:12.");
        for (int frame = 0; frame < MapMenuController.FastFadeFrames - 1; frame++)
        {
            _mapMenu.Update(1.0 / 60.0);
            if (_rooms.ActiveGroup != 4 || _rooms.CurrentRoom.Id != 0x09)
                throw new InvalidOperationException("Debug fast travel loaded before the fade reached white.");
        }
        _mapMenu.Update(1.0 / 60.0);
        if (_rooms.ActiveGroup != 0 || _rooms.CurrentRoom.Id != 0x12)
        {
            throw new InvalidOperationException(
                "Debug map fast travel did not load present room 0:12 at full white.");
        }
        for (int frame = 0; frame < MapMenuController.FastFadeFrames; frame++)
            _mapMenu.Update(1.0 / 60.0);
        if (_mapMenu.IsActive || !_player.IsPhysicsProcessing() || !_player.IsProcessing())
            throw new InvalidOperationException("Debug fast travel did not restore gameplay processing.");

        GD.Print("Validated original present/past/dungeon map tilemaps, imported TX_03XX area " +
            "text, source-specific color-0 OAM transparency, arrow Y-flip, 7-update popup " +
            "expansion, map/compass floor and " +
            "boss/treasure reveals, 14x14 cursor wrapping, 32-update marker blink, 11-update " +
            "fast fades, Link input freezing, and dungeon-to-overworld debug fast travel.");
    }

    private void LoadValidationRoom(int group, int room)
    {
        LoadDebugRoom(group, room);
        _player.WarpTo(FindSpawn());
        _player.Face(Vector2I.Down);
    }

    private void ValidatePushBlocks()
    {
        OracleRoomData directionalRoom = _rooms.Load(4, 0x09);
        _roomView.SetRoom(directionalRoom.Texture);
        _entities.LoadRoom(4, directionalRoom);

        // The left dungeon wall at this position sets the original $0c
        // adjacent-wall mask. Link should use the pushing walk variant only
        // while the held direction matches the side he is facing.
        _player.WarpTo(new Vector2(20, 24));
        _player.Face(Vector2I.Left);
        _player.UpdatePushingState(Vector2.Left);
        if (!_player.IsPushing)
        {
            throw new InvalidOperationException(
                "Link did not enter his pushing animation against the ordinary wall in 4:09.");
        }
        _player.UpdatePushingState(Vector2.Right);
        if (_player.IsPushing)
        {
            throw new InvalidOperationException(
                "Link retained his pushing animation without holding toward the wall.");
        }

        Vector2 rightOnlyBlock = new(0x0b * 16 + 8, 0x01 * 16 + 8);
        Vector2 linkAbove = rightOnlyBlock + new Vector2(0, -10);
        for (int frame = 0; frame < PushBlockController.PushDelayFrames; frame++)
        {
            _pushBlocks.UpdatePushAttempt(
                linkAbove, Vector2I.Down, Vector2.Down);
        }
        if (_pushBlocks.Active || _pushBlocks.RemainingPushFrames !=
            PushBlockController.PushDelayFrames ||
            directionalRoom.GetMetatile(rightOnlyBlock) != 0x19 ||
            directionalRoom.GetCollision(
                directionalRoom.GetMetatile(rightOnlyBlock + Vector2.Down * 16)) != 0)
        {
            throw new InvalidOperationException(
                "Right-only block $19 accepted a downward push toward clear floor in 4:09.");
        }

        OracleRoomData room = _rooms.Load(4, 0x08);
        _roomView.SetRoom(room.Texture);
        _entities.LoadRoom(4, room);

        // Dungeon 0 room $08 has an all-direction tile $1c at packed
        // position $4b. Its right neighbor is the solid one-way block $19,
        // while the tile above is clear floor, so both rejection and a full
        // successful push can be checked against unmodified original data.
        Vector2 blockCenter = new(0x0b * 16 + 8, 0x04 * 16 + 8);
        Vector2 linkBelow = blockCenter + new Vector2(0, 10);
        Vector2 linkLeft = blockCenter + new Vector2(-10, 0);
        Vector2 linkRight = blockCenter + new Vector2(10, 0);
        if (room.ActiveCollisions != 2 || room.GetMetatile(blockCenter) != 0x1c)
        {
            throw new InvalidOperationException(
                $"Expected dungeon collision mode 2 and block $1c at 4:08/$4b, got " +
                $"mode {room.ActiveCollisions} / tile ${room.GetMetatile(blockCenter):x2}.");
        }

        _player.WarpTo(linkBelow);
        _player.Face(Vector2I.Up);
        _player.UpdatePushingState(Vector2.Up);
        if (!_player.IsPushing)
        {
            throw new InvalidOperationException(
                "Link did not enter his pushing animation while pressing block $1c in 4:08.");
        }

        Vector2 cornerApproach = new(blockCenter.X - 6, linkBelow.Y);
        for (int frame = 0; frame < PushBlockController.PushDelayFrames; frame++)
        {
            _pushBlocks.UpdatePushAttempt(
                cornerApproach, Vector2I.Up, Vector2.Up);
        }
        if (_pushBlocks.Active || _pushBlocks.RemainingPushFrames !=
            PushBlockController.PushDelayFrames)
        {
            throw new InvalidOperationException(
                "Block $1c accepted a push while Link occupied a metatile corner.");
        }

        for (int frame = 0; frame < PushBlockController.PushDelayFrames; frame++)
        {
            _pushBlocks.UpdatePushAttempt(
                linkRight, Vector2I.Left, Vector2.Left);
        }
        for (int frame = 0; frame < PushBlockController.MoveFrames; frame++)
        {
            _pushBlocks.Advance(1.0 / 60.0);
        }
        Vector2 holeCenter = blockCenter + Vector2.Left * 16;
        if (_pushBlocks.Active || room.GetMetatile(blockCenter) != 0xa0 ||
            room.GetMetatile(holeCenter) != 0xf5)
        {
            throw new InvalidOperationException(
                "Block $1c did not disappear while preserving destination hole $f5.");
        }
        if (!room.ReplaceMetatile(blockCenter, 0xa0, 0x1c, (long)_animationTicks))
            throw new InvalidOperationException("Could not restore 4:08/$4b after the hole test.");

        _pushBlocks.UpdatePushAttempt(linkLeft, Vector2I.Right, Vector2.Right);
        if (_pushBlocks.RemainingPushFrames != PushBlockController.PushDelayFrames ||
            _pushBlocks.Active)
        {
            throw new InvalidOperationException(
                "Block $1c started moving right even though destination tile $19 is solid.");
        }

        for (int frame = 0; frame < PushBlockController.PushDelayFrames - 1; frame++)
        {
            _pushBlocks.UpdatePushAttempt(
                linkBelow, Vector2I.Up, Vector2.Up);
        }
        if (_pushBlocks.Active || _pushBlocks.RemainingPushFrames != 1 ||
            room.GetMetatile(blockCenter) != 0x1c)
        {
            throw new InvalidOperationException(
                "Block $1c moved before Link completed the original 20-update push delay.");
        }

        _pushBlocks.UpdatePushAttempt(linkBelow, Vector2I.Up, Vector2.Zero);
        if (_pushBlocks.RemainingPushFrames != PushBlockController.PushDelayFrames)
        {
            throw new InvalidOperationException(
                "Releasing the direction did not reset wPushingAgainstTileCounter to 20.");
        }

        for (int frame = 0; frame < PushBlockController.PushDelayFrames; frame++)
        {
            _pushBlocks.UpdatePushAttempt(
                linkBelow, Vector2I.Up, Vector2.Up);
        }
        if (!_pushBlocks.Active || room.GetMetatile(blockCenter) != 0xa0 ||
            !_collision.Collides(blockCenter + new Vector2(0, -2)))
        {
            throw new InvalidOperationException(
                "Block $1c did not become a Link-blocking object over source floor $a0.");
        }

        for (int frame = 0; frame < PushBlockController.MoveFrames - 1; frame++)
        {
            _pushBlocks.Advance(1.0 / 60.0);
        }
        Vector2 expectedTopLeft = new(blockCenter.X - 8, blockCenter.Y - 8 - 15.5f);
        if (!_pushBlocks.Active ||
            !_pushBlocks.BlockTopLeft.IsEqualApprox(expectedTopLeft) ||
            room.GetMetatile(blockCenter + Vector2.Up * 16) != 0xa0)
        {
            throw new InvalidOperationException(
                "Block $1c did not move at SPEED_80 for the first 31 updates.");
        }

        _pushBlocks.Advance(1.0 / 60.0);
        if (_pushBlocks.Active || room.GetMetatile(blockCenter) != 0xa0 ||
            room.GetMetatile(blockCenter + Vector2.Up * 16) != 0x1d)
        {
            throw new InvalidOperationException(
                "Block $1c did not finish after 32 updates as destination tile $1d.");
        }

        GD.Print("Validated Link's wall/block pushing animation, directional/corner " +
            "restrictions, hazard disposal, 4:08/$4b push delay reset, blocked " +
            "destination, source $a0 replacement, SPEED_80 movement, moving " +
            "collision, and destination tile $1d.");
    }

    private void ValidateChests()
    {
        WarpToChestTest();
        const int chestPosition = 0x51;
        Vector2 chestPoint = new(24, 88);
        if (_activeGroup != 0 || _currentRoom.Id != 0x49 ||
            _currentRoom.GetPackedPosition(chestPoint) != chestPosition ||
            _currentRoom.GetMetatile(chestPoint) != 0xf1)
        {
            throw new InvalidOperationException(
                "The canonical 0:49/$51 30-rupee chest was not available for testing.");
        }

        Image roomImage = _currentRoom.Texture.GetImage();
        int redChestPixels = 0;
        for (int y = 80; y < 96; y++)
        for (int x = 16; x < 32; x++)
        {
            Color pixel = roomImage.GetPixel(x, y);
            if (pixel.R > 0.5f && pixel.G < 0.2f && pixel.B < 0.25f)
                redChestPixels++;
        }
        if (redChestPixels == 0)
        {
            throw new InvalidOperationException(
                "Chest $51 did not render with PALH_0f background palette 0.");
        }

        _player.WarpTo(new Vector2(24, 74));
        _player.Face(Vector2I.Down);
        if (!TryInteract(_player) || !_dialogue.IsOpen ||
            _dialogue.CurrentMessage != "It won't open\nfrom this side!")
        {
            throw new InvalidOperationException("Chest $51 did not use TX_510d from the wrong side.");
        }
        _dialogue.Close();

        _player.WarpTo(new Vector2(24, 100));
        _player.Face(Vector2I.Up);
        int rupeesBefore = _player.Rupees;
        if (!TryInteract(_player) || !_interactions.ChestRewardActive ||
            _currentRoom.GetMetatile(chestPoint) != 0xf0)
        {
            throw new InvalidOperationException("Chest $51 did not open from below into tile $f0.");
        }

        _interactions.Update(31.0 / 60.0, _player);
        if (!_interactions.ChestRewardActive || _player.Rupees != rupeesBefore)
            throw new InvalidOperationException("The chest reward completed before its 32-frame rise.");
        _interactions.Update(1.0 / 60.0, _player);
        if (!_interactions.ChestRewardActive || _player.Rupees != rupeesBefore + 30 ||
            !_dialogue.IsOpen || _dialogue.CurrentMessage != "You got\n30 Rupees!\nThat's nice.")
        {
            throw new InvalidOperationException(
                "TREASURE_OBJECT_RUPEES_04 did not remain visible while showing TX_0005.");
        }

        _dialogue.Close();
        if (!_interactions.ChestRewardActive)
            throw new InvalidOperationException("The chest reward disappeared before its textbox closed.");
        _interactions.Update(0.0, _player);
        if (_interactions.ChestRewardActive)
            throw new InvalidOperationException("The chest reward remained after its textbox closed.");

        _currentRoom = _world.LoadRoom(0, 0x49);
        _roomView.SetRoom(_currentRoom.Texture);
        if (_currentRoom.GetMetatile(chestPoint) != 0xf0 || TryInteract(_player))
            throw new InvalidOperationException("The opened chest room flag did not persist for the session.");

        GD.Print("Validated 0:49/$51 red chest palette, direction, 32-frame reward rise, " +
            "reward visibility through TX_0005, 30 rupees, and opened state.");
    }

    private void ValidateInventoryFoundation()
    {
        if (!_inventory.HasTreasure(TreasureDatabase.TreasureSword) ||
            _inventory.EquippedB != InventoryState.ItemSword ||
            _inventory.SwordLevel != 1)
        {
            throw new InvalidOperationException(
                "Impa's TREASURE_OBJECT_SWORD_00 gift was not added to the first empty " +
                "inventory slot, wInventoryB.");
        }

        // The menu swap checks below begin from their established sword-on-A
        // arrangement; move Impa's B-slot gift there through the normal path.
        _inventory.EquipA(InventoryState.ItemSword);

        var chests = new ChestDatabase();
        if (!chests.TryGet(4, 0x87, 0x65, out ChestDatabase.ChestRecord switchHookChest) ||
            switchHookChest.TreasureObject != "TREASURE_OBJECT_SWITCH_HOOK_00" ||
            switchHookChest.TreasureId != TreasureDatabase.TreasureSwitchHook ||
            switchHookChest.Parameter != 1)
        {
            throw new InvalidOperationException(
                "The original 4:87/$65 Switch Hook chest did not resolve to TREASURE_SWITCH_HOOK parameter 1.");
        }

        var tempInventory = new InventoryState(_treasures);
        tempInventory.GiveTreasure(new TreasureDatabase.TreasureObjectRecord(
            switchHookChest.TreasureObject,
            switchHookChest.TreasureId,
            switchHookChest.SubId,
            switchHookChest.Parameter,
            switchHookChest.TextId,
            switchHookChest.Graphic,
            switchHookChest.Message));
        if (!tempInventory.HasTreasure(TreasureDatabase.TreasureSwitchHook) ||
            tempInventory.SwitchHookLevel != 1 ||
            tempInventory.EquippedB != TreasureDatabase.TreasureSwitchHook)
        {
            throw new InvalidOperationException(
                "TREASURE_OBJECT_SWITCH_HOOK_00 did not update obtained flags, level, and first free button slot.");
        }

        GD.Print("Validated disassembly-backed inventory state, equipped A/B slots, " +
            "Impa's TREASURE_OBJECT_SWORD_00 gift, and non-rupee chest treasure give path.");
    }

    private void ValidateInventoryMenu()
    {
        ValidateItemIconShadeMapping();
        if (!Mathf.IsEqualApprox(InventoryMenuController.FastFadeFrames, 11.0f))
            throw new InvalidOperationException("The inventory menu must use the 11-update fast palette fade.");

        _inventoryMenu.BeginOpeningForValidation();
        if (!_gameplayPause.IsOwnedBy(_inventoryMenu) ||
            _mapMenu.CanOpenNormalForValidation)
        {
            throw new InvalidOperationException(
                "Inventory opening did not exclusively own the shared menu pause/load state.");
        }
        for (int frame = 0; frame < InventoryMenuController.FastFadeFrames - 1; frame++)
        {
            _inventoryMenu.Update(1.0 / 60.0);
            if (_inventoryScreen.Visible)
                throw new InvalidOperationException("The inventory screen appeared before the fade reached white.");
        }
        if (_scene.MenuFade.Color.A <= 0.0f || _scene.MenuFade.Color.A >= 1.0f)
            throw new InvalidOperationException("The inventory opening fade did not remain partial for 10 updates.");
        _inventoryMenu.Update(1.0 / 60.0);
        if (!_inventoryScreen.Visible || !Mathf.IsEqualApprox(_scene.MenuFade.Color.A, 1.0f))
            throw new InvalidOperationException("The inventory screen was not swapped in at full white.");
        for (int frame = 0; frame < InventoryMenuController.FastFadeFrames; frame++)
            _inventoryMenu.Update(1.0 / 60.0);
        if (!_inventoryMenu.IsActive || !_inventoryScreen.Visible ||
            !_inventoryMenu.IsOpen || !Mathf.IsZeroApprox(_scene.MenuFade.Color.A) ||
            _player.IsPhysicsProcessing() || _player.IsProcessing())
        {
            throw new InvalidOperationException(
                "The inventory opening fade did not finish with the menu open and gameplay frozen.");
        }

        if (_inventoryScreen.Cursor != 0 || _inventory.StorageItemAt(0) != InventoryState.ItemNone ||
            _inventory.EquippedA != InventoryState.ItemSword)
        {
            throw new InvalidOperationException("Inventory menu validation expected sword on A and slot 0 empty.");
        }

        _inventoryScreen.EquipToA();
        if (_inventory.EquippedA != InventoryState.ItemNone ||
            _inventory.StorageItemAt(0) != InventoryState.ItemSword)
        {
            throw new InvalidOperationException(
                "Pressing A on empty storage slot 0 did not unequip the sword into wInventoryStorage.");
        }

        _inventoryScreen.EquipToB();
        if (_inventory.EquippedB != InventoryState.ItemSword ||
            _inventory.StorageItemAt(0) != InventoryState.ItemNone)
        {
            throw new InvalidOperationException(
                "Pressing B on storage slot 0 did not equip the sword to wInventoryB.");
        }

        _inventoryScreen.MoveCursor(Vector2I.Left);
        if (_inventoryScreen.Cursor != 15)
            throw new InvalidOperationException("Inventory cursor did not wrap left with the original & $0f rule.");
        _inventoryScreen.MoveCursor(Vector2I.Right);
        if (_inventoryScreen.Cursor != 0)
            throw new InvalidOperationException("Inventory cursor did not return to slot 0 after wrapping.");

        _inventoryScreen.EquipToB();
        _inventoryScreen.EquipToA();
        if (_inventory.EquippedA != InventoryState.ItemSword ||
            _inventory.EquippedB != InventoryState.ItemNone ||
            _inventory.StorageItemAt(0) != InventoryState.ItemNone)
        {
            throw new InvalidOperationException("Inventory menu did not restore the sword to A through storage swaps.");
        }

        _inventoryScreen.BeginNextSubscreen();
        for (int frame = 0; frame < InventoryScreen.PageScrollUpdates - 1; frame++)
            _inventoryScreen.UpdatePageTransition(1.0 / 60.0);
        if (!_inventoryScreen.PageTransitionActive ||
            _inventoryScreen.Subscreen != InventoryScreen.InventorySubscreen.Items)
        {
            throw new InvalidOperationException(
                "The secondary inventory page arrived before the original 13-update scroll finished.");
        }
        _inventoryScreen.UpdatePageTransition(1.0 / 60.0);
        if (_inventoryScreen.PageTransitionActive ||
            _inventoryScreen.Subscreen != InventoryScreen.InventorySubscreen.SecondaryItems)
        {
            throw new InvalidOperationException("The secondary inventory page did not finish after 13 updates.");
        }
        _inventoryScreen.MoveCursor(Vector2I.Left);
        if (_inventoryScreen.ActiveCursor != 14)
            throw new InvalidOperationException("The empty secondary page did not wrap 0 -> 14 to the left.");
        _inventoryScreen.MoveCursor(Vector2I.Right);

        OracleSaveData ringSave = OracleSaveData.CreateStandardGame();
        ringSave.WriteWramByte(0xc6cc, 2);
        ringSave.WriteWramByte(0xc6c6, 7);
        ringSave.WriteWramByte(0xc6c7, 8);
        ringSave.WriteWramByte(0xc6c8, 9);
        var ringInventory = new InventoryState(_treasures, ringSave);
        if (ringInventory.RingBoxCapacity != 3 || ringInventory.RingAt(2) != 9 ||
            !ringInventory.EquipRingAt(1) || ringInventory.ActiveRing != 8 ||
            !OracleSaveData.TryDeserialize(ringSave.Serialize(), out OracleSaveData? restoredRingSave) ||
            new InventoryState(_treasures, restoredRingSave!).ActiveRing != 8)
        {
            throw new InvalidOperationException(
                "Ring-box capacity, contents, active-ring toggle, or save-image persistence regressed.");
        }

        _inventoryScreen.BeginNextSubscreen();
        for (int frame = 0; frame < InventoryScreen.PageScrollUpdates; frame++)
            _inventoryScreen.UpdatePageTransition(1.0 / 60.0);
        if (_inventoryScreen.Subscreen != InventoryScreen.InventorySubscreen.EssencesAndSave)
            throw new InvalidOperationException("The essence/save inventory page did not follow page 2.");
        _inventoryScreen.MoveCursor(Vector2I.Right);
        _inventoryScreen.MoveCursor(Vector2I.Down);
        _inventoryScreen.MoveCursor(Vector2I.Down);
        if (!_inventoryScreen.SaveAndQuitSelected || _inventoryScreen.ActiveCursor != 0x82)
            throw new InvalidOperationException("The page-3 cursor did not reach the original Save & Quit entry.");

        _inventoryMenu.OpenSaveMenuFromInventoryForValidation();
        if (!_saveQuitScreen.Visible || _inventoryScreen.Visible ||
            !Mathf.IsEqualApprox(_scene.MenuFade.Color.A, 1.0f))
        {
            throw new InvalidOperationException(
                "Selecting Save & Quit did not swap screens immediately at full white.");
        }
        for (int frame = 0; frame < InventoryMenuController.FastFadeFrames; frame++)
            _inventoryMenu.Update(1.0 / 60.0);
        if (!_inventoryMenu.SaveMenuOpen || !_saveQuitScreen.Visible ||
            !Mathf.IsZeroApprox(_scene.MenuFade.Color.A) || _saveQuitScreen.Cursor != 0)
        {
            throw new InvalidOperationException(
                "The three-option save menu did not finish its 11-update fade-in on Continue.");
        }
        _saveQuitScreen.Move(1);
        int saveRequests = _inventoryMenu.SaveRequests;
        int saveWrites = _saveWriteRequests;
        _inventoryMenu.SelectSaveOptionForValidation();
        if (_inventoryMenu.SaveRequests != saveRequests + 1 ||
            _saveWriteRequests != saveWrites + 1 ||
            _saveQuitScreen.DelayCounter != InventoryMenuController.SaveSelectionDelayFrames)
        {
            throw new InvalidOperationException("Save and Continue did not save immediately and start its 30-update delay.");
        }
        for (int frame = 0; frame < InventoryMenuController.SaveSelectionDelayFrames - 1; frame++)
            _inventoryMenu.Update(1.0 / 60.0);
        if (!_saveQuitScreen.Visible || !_inventoryMenu.SaveMenuOpen)
            throw new InvalidOperationException("Save and Continue exited before its original 30-update delay.");
        _inventoryMenu.Update(1.0 / 60.0);

        for (int frame = 0; frame < InventoryMenuController.FastFadeFrames - 1; frame++)
        {
            _inventoryMenu.Update(1.0 / 60.0);
            if (!_saveQuitScreen.Visible)
                throw new InvalidOperationException("The save menu disappeared before the fade reached white.");
        }
        _inventoryMenu.Update(1.0 / 60.0);
        if (_saveQuitScreen.Visible || !Mathf.IsEqualApprox(_scene.MenuFade.Color.A, 1.0f))
            throw new InvalidOperationException("The save menu was not removed at full white.");
        for (int frame = 0; frame < InventoryMenuController.FastFadeFrames; frame++)
            _inventoryMenu.Update(1.0 / 60.0);
        if (_inventoryMenu.IsActive || _inventoryScreen.Visible || _saveQuitScreen.Visible ||
            !Mathf.IsZeroApprox(_scene.MenuFade.Color.A) ||
            !_player.IsPhysicsProcessing() || !_player.IsProcessing())
        {
            throw new InvalidOperationException(
                "The inventory closing fade did not restore gameplay processing.");
        }

        _inventoryMenu.BeginSaveOpeningForValidation();
        for (int frame = 0; frame < InventoryMenuController.FastFadeFrames - 1; frame++)
        {
            _inventoryMenu.Update(1.0 / 60.0);
            if (_saveQuitScreen.Visible)
                throw new InvalidOperationException("Start+Select save menu appeared before the fade reached white.");
        }
        _inventoryMenu.Update(1.0 / 60.0);
        for (int frame = 0; frame < InventoryMenuController.FastFadeFrames; frame++)
            _inventoryMenu.Update(1.0 / 60.0);
        if (!_inventoryMenu.SaveMenuOpen || !_saveQuitScreen.Visible)
            throw new InvalidOperationException("Start+Select did not open the save menu through the shared fast fade.");
        _inventoryMenu.CloseImmediatelyForValidation();

        GD.Print("Validated inventory 11-update fast white fades, 13-update three-page scrolling, " +
            "secondary cursor/ring persistence, essence/save navigation, Start+Select, three save choices, " +
            "30-update selection delay, A/B storage swaps, and gameplay freezing.");
    }

    private static void ValidateItemIconShadeMapping()
    {
        if (ItemIconAtlas.ShadeFromPng(Color.Color8(0, 0, 0), out bool transparent) != 0 || !transparent ||
            ItemIconAtlas.ShadeFromPng(Color.Color8(85, 85, 85), out transparent) != 1 || transparent ||
            ItemIconAtlas.ShadeFromPng(Color.Color8(170, 170, 170), out transparent) != 2 || transparent ||
            ItemIconAtlas.ShadeFromPng(Color.Color8(255, 255, 255), out transparent) != 3 || transparent)
        {
            throw new InvalidOperationException(
                "Item icon PNG shade mapping no longer treats black as transparent and white as highlight.");
        }

        Image icons1 = Image.CreateEmpty(128, 16, false, Image.Format.Rgba8);
        Image icons2 = Image.CreateEmpty(128, 16, false, Image.Format.Rgba8);
        Image icons3 = Image.CreateEmpty(128, 16, false, Image.Format.Rgba8);
        if (!ItemIconAtlas.Select(0x99, icons1, icons2, icons3, out Image bracelet, out int braceletCell) ||
            bracelet != icons2 || braceletCell != 9 ||
            !ItemIconAtlas.Select(0xaf, icons1, icons2, icons3, out Image glove, out int gloveCell) ||
            glove != icons3 || gloveCell != 15)
        {
            throw new InvalidOperationException(
                "Display `$99/`$af no longer resolve to item icon sheets 2:9 and 3:15.");
        }

        Color[,] palettes = ItemIconAtlas.LoadStandardSpritePalettes();
        if (!palettes[0, 2].IsEqualApprox(new Color(0x02 / 31.0f, 0x15 / 31.0f, 0x08 / 31.0f)) ||
            !palettes[5, 1].IsEqualApprox(new Color(0x1f / 31.0f, 0x16 / 31.0f, 0x06 / 31.0f)) ||
            !palettes[5, 2].IsEqualApprox(new Color(0x1b / 31.0f, 0x00, 0x00)))
        {
            throw new InvalidOperationException(
                "Standard sprite palettes no longer match bracelet palette `$05 and sword palette `$00.");
        }
    }

    private void ValidateBraceletChestAndPushGate()
    {
        var chests = new ChestDatabase();
        if (!chests.TryGet(5, 0xa6, 0x37, out ChestDatabase.ChestRecord braceletChest) ||
            braceletChest.TreasureObject != "TREASURE_OBJECT_BRACELET_02" ||
            braceletChest.TreasureId != TreasureDatabase.TreasureBracelet ||
            braceletChest.Parameter != 2)
        {
            throw new InvalidOperationException(
                "The original 5:a6/$37 chest did not resolve to TREASURE_OBJECT_BRACELET_02.");
        }

        var pushables = new PushableTileDatabase();
        if (!pushables.TryGet(2, 0x10, out PushableTileDatabase.PushableTileRecord braceletBlock) ||
            !braceletBlock.RequiresBracelet ||
            !braceletBlock.AllowsEveryDirection ||
            braceletBlock.SourceReplacement != 0xa0 ||
            braceletBlock.DestinationTile != 0x10)
        {
            throw new InvalidOperationException(
                "Collision mode 2 tile $10 did not retain interactable parameter $c0 and pushblock data.");
        }

        var breakables = new BreakableTileDatabase();
        if (!breakables.TryGet(2, 0x10, out BreakableTileDatabase.BreakableTileRecord liftablePot) ||
            !liftablePot.AllowsSource(BreakableTileDatabase.SourceBracelet) ||
            liftablePot.Replacement != 0xa0)
        {
            throw new InvalidOperationException(
                "Collision mode 2 tile $10 did not import as a bracelet-breakable tile with replacement $a0.");
        }

        LoadValidationRoom(4, 0x08);
        Vector2 blockCenter = new(0x0b * 16 + 8, 0x04 * 16 + 8);
        Vector2 linkBelow = blockCenter + new Vector2(0, 10);
        if (_currentRoom.GetMetatile(blockCenter) != 0x1c ||
            !_currentRoom.ReplaceMetatile(blockCenter, 0x1c, 0x10, (long)_animationTicks))
        {
            throw new InvalidOperationException("Could not prepare 4:08/$4b as bracelet-required tile $10.");
        }

        for (int frame = 0; frame < PushBlockController.PushDelayFrames; frame++)
            _playerWorld.UpdatePushableBlocks(linkBelow, Vector2I.Up, Vector2.Up);
        if (_pushBlocks.Active || _currentRoom.GetMetatile(blockCenter) != 0x10)
        {
            throw new InvalidOperationException(
                "Bracelet-required tile $10 moved before TREASURE_BRACELET was obtained.");
        }

        LoadValidationRoom(4, 0xce);
        _interactions.ResetChestForTesting(4, 0xce, 0x67, "TREASURE_OBJECT_BRACELET_00");
        Vector2 debugBraceletChest = new(7 * OracleRoomData.MetatileSize + 8, 6 * OracleRoomData.MetatileSize + 8);
        if (_currentRoom.GetMetatile(debugBraceletChest) != 0xf1)
            throw new InvalidOperationException("The debug 4:ce/$67 Power Bracelet chest was not closed.");

        _player.WarpTo(new Vector2(debugBraceletChest.X, debugBraceletChest.Y + 12));
        _player.Face(Vector2I.Up);
        if (!TryInteract(_player) || !_interactions.ChestRewardActive ||
            _currentRoom.GetMetatile(debugBraceletChest) != 0xf0)
        {
            throw new InvalidOperationException("The debug 4:ce/$67 Power Bracelet chest did not open from below.");
        }

        _interactions.Update(32.0 / 60.0, _player);
        if (!_inventory.HasTreasure(TreasureDatabase.TreasureBracelet) ||
            _inventory.BraceletLevel != 1 ||
            _inventory.EquippedB != InventoryState.ItemBracelet ||
            !_dialogue.IsOpen ||
            _dialogue.CurrentMessage != "You got the\nPower Bracelet!\nHold the button\nand press \\item(0x00)\nto lift heavy\nobjects!")
        {
            throw new InvalidOperationException(
                "TREASURE_OBJECT_BRACELET_00 did not set obtained flags, wBraceletLevel, wInventoryB, and TX_0026.");
        }
        _dialogue.Close();
        _interactions.Update(0.0, _player);

        Vector2 liftPoint = new(7 * OracleRoomData.MetatileSize + 8, 2 * OracleRoomData.MetatileSize + 8);
        if (_currentRoom.GetMetatile(liftPoint) != 0x10)
            throw new InvalidOperationException("The 4:ce bracelet-use test tile was not dungeon tile $10.");
        _player.WarpTo(new Vector2(liftPoint.X, liftPoint.Y + 12));
        _player.Face(Vector2I.Up);
        if (!_playerWorld.TryUseBracelet(_player) || _currentRoom.GetMetatile(liftPoint) != 0xa0)
        {
            throw new InvalidOperationException(
                "Equipped bracelet use did not break/lift the tile in front using BREAKABLETILESOURCE_BRACELET.");
        }

        LoadValidationRoom(5, 0xa6);
        _interactions.ResetChestForTesting(5, 0xa6, 0x37);
        Vector2 chestPoint = new(7 * OracleRoomData.MetatileSize + 8, 3 * OracleRoomData.MetatileSize + 8);
        if (_currentRoom.GetMetatile(chestPoint) != 0xf1)
            throw new InvalidOperationException("The original 5:a6/$37 Power Glove chest was not closed.");

        _player.WarpTo(new Vector2(chestPoint.X, chestPoint.Y + 12));
        _player.Face(Vector2I.Up);
        if (!TryInteract(_player) || !_interactions.ChestRewardActive ||
            _currentRoom.GetMetatile(chestPoint) != 0xf0)
        {
            throw new InvalidOperationException("The 5:a6/$37 Power Glove chest did not open from below.");
        }

        _interactions.Update(32.0 / 60.0, _player);
        if (!_inventory.HasTreasure(TreasureDatabase.TreasureBracelet) ||
            _inventory.BraceletLevel != 2 ||
            _inventory.EquippedB != InventoryState.ItemBracelet ||
            !_dialogue.IsOpen ||
            _dialogue.CurrentMessage != "You got the\nPower Glove!\nYou can now lift\nheavy objects.")
        {
            throw new InvalidOperationException(
                "TREASURE_OBJECT_BRACELET_02 did not set obtained flags, wBraceletLevel, wInventoryB, and TX_002f.");
        }
        _dialogue.Close();
        _interactions.Update(0.0, _player);

        LoadValidationRoom(4, 0x08);
        if (_currentRoom.GetMetatile(blockCenter) != 0x10)
        {
            if (_currentRoom.GetMetatile(blockCenter) != 0x1c ||
                !_currentRoom.ReplaceMetatile(blockCenter, 0x1c, 0x10, (long)_animationTicks))
            {
                throw new InvalidOperationException("Could not restore bracelet push validation tile $10.");
            }
        }

        for (int frame = 0; frame < PushBlockController.PushDelayFrames; frame++)
            _playerWorld.UpdatePushableBlocks(linkBelow, Vector2I.Up, Vector2.Up);
        if (!_pushBlocks.Active || _currentRoom.GetMetatile(blockCenter) != 0xa0)
        {
            throw new InvalidOperationException(
                "Bracelet-required tile $10 did not start moving after TREASURE_BRACELET was obtained.");
        }
        _pushBlocks.Cancel();
        _currentRoom.ReplaceMetatile(blockCenter, 0xa0, 0x1c, (long)_animationTicks);

        GD.Print("Validated debug Power Bracelet chest TREASURE_OBJECT_BRACELET_00, " +
            "bracelet tile use via BREAKABLETILESOURCE_BRACELET, original Power Glove upgrade, " +
            "and bracelet-required pushblock tile $10.");
    }

    private void ValidateHouseWarp()
    {
        WarpToHouseTest();
        for (float y = 54; y >= 47; y--)
        {
            if (Collides(new Vector2(88, y)))
                throw new InvalidOperationException($"The path into exterior door $25 is blocked at y={y}.");
        }
        _player.WarpTo(new Vector2(88, 47));
        if (!CheckTileWarp(_player) || _activeGroup != 2 || _currentRoom.Id != 0xea)
            throw new InvalidOperationException(
                $"Expected exterior 0:47/$25 to enter house 2:ea, got {_activeGroup}:{_currentRoom.Id:x2}.");
        if (!IsTransitioning || !Mathf.IsEqualApprox(_player.Position.Y, _currentRoom.Height))
            throw new InvalidOperationException("House entry did not begin at the bottom edge of the interior.");
        UpdateRoomWarpTransition(WarpEnterFrames / 60.0);
        if (!IsTransitioning || !Mathf.IsEqualApprox(_player.Position.Y, _currentRoom.Height - WarpEnterFrames))
            throw new InvalidOperationException("Link did not perform the 28-frame interior entry walk.");
        UpdateRoomWarpTransition((WarpFadeFrames - WarpEnterFrames) / 60.0);
        if (IsTransitioning)
            throw new InvalidOperationException("The 32-frame room fade did not finish after entering the house.");
        if (_saveData.RespawnGroup != 2 || _saveData.RespawnRoom != 0xea ||
            _saveData.RespawnFacing != 0 || _saveData.RespawnY != 0x64 ||
            _saveData.RespawnX != 0x50)
        {
            throw new InvalidOperationException(
                "TRANSITION_DEST_ENTERSCREEN did not record house 2:ea's final entry checkpoint.");
        }

        for (float y = _player.Position.Y; y <= _currentRoom.Height + 2; y++)
        {
            if (Collides(new Vector2(_currentRoom.Width / 2.0f, y)))
                throw new InvalidOperationException($"The house's bottom exit is blocked at y={y}.");
        }
        _player.WarpTo(new Vector2(_currentRoom.Width / 2.0f, _currentRoom.Height + 2));
        CheckRoomExit(_player);
        if (!IsTransitioning || _activeGroup != 2 || _currentRoom.Id != 0xea)
            throw new InvalidOperationException("The house exit did not begin with its scripted walk offscreen.");
        UpdateRoomWarpTransition(WarpLeaveFrames / 60.0);
        if (_activeGroup != 0 || _currentRoom.Id != 0x47 || !IsTransitioning)
            throw new InvalidOperationException("The exterior was not loaded after the 16-frame exit walk.");
        UpdateRoomWarpTransition(WarpFadeFrames / 60.0);
        if (_activeGroup != 0 || _currentRoom.Id != 0x47 ||
            _currentRoom.GetPackedPosition(_player.Position) != 0x35)
            throw new InvalidOperationException(
                $"Expected house 2:ea bottom exit to step out below 0:47/$25, got " +
                $"{_activeGroup}:{_currentRoom.Id:x2}/${_currentRoom.GetPackedPosition(_player.Position):x2}.");
        if (Collides(_player.Position + Vector2.Down))
            throw new InvalidOperationException("The exterior landing spot below 0:47/$25 is blocked.");
        if (_saveData.RespawnGroup != 0 || _saveData.RespawnRoom != 0x47 ||
            _saveData.RespawnFacing != 2 || _saveData.RespawnY != 0x38 ||
            _saveData.RespawnX != 0x58 ||
            !OracleSaveData.TryDeserialize(_saveData.Serialize(), out OracleSaveData? exteriorSave) ||
            exteriorSave!.RespawnGroup != 0 || exteriorSave.RespawnRoom != 0x47 ||
            exteriorSave.RespawnY != 0x38 || exteriorSave.RespawnX != 0x58)
        {
            throw new InvalidOperationException(
                "TRANSITION_DEST_SET_RESPAWN did not persist exterior 0:47's stepped-out checkpoint.");
        }

        int checkpointGroup = _saveData.RespawnGroup;
        int checkpointRoom = _saveData.RespawnRoom;
        int checkpointY = _saveData.RespawnY;
        int checkpointX = _saveData.RespawnX;

        _activeGroup = 2;
        ClearDeactivatedWarp();
        _currentRoom = _world.LoadRoom(_activeGroup, 0xeb);
        _roomView.SetRoom(_currentRoom.Texture);
        _player.WarpTo(new Vector2(-2, _currentRoom.Height / 2.0f));
        CheckRoomExit(_player);
        if (_activeGroup != 2 || _currentRoom.Id != 0xea)
            throw new InvalidOperationException(
                $"Expected room 2:eb left edge to scroll to 2:ea, got {_activeGroup}:{_currentRoom.Id:x2}.");
        ValidateLinkScrollsForOneTransitionFrame();
        FinishActiveScrollingTransitionForValidation();
        if (_currentRoom.GetPackedPosition(_player.Position) != 0x49)
            throw new InvalidOperationException(
                $"Expected Link to finish 2:eb -> 2:ea near the right edge, got " +
                $"${_currentRoom.GetPackedPosition(_player.Position):x2}.");
        if (_saveData.RespawnGroup != checkpointGroup || _saveData.RespawnRoom != checkpointRoom ||
            _saveData.RespawnY != checkpointY || _saveData.RespawnX != checkpointX)
        {
            throw new InvalidOperationException(
                "An ordinary scrolling transition incorrectly replaced the death checkpoint.");
        }

        GD.Print("Validated original house entry/exit fades, destination checkpoint updates, " +
            "save-image round trip, and non-checkpoint 2:eb -> 2:ea scrolling.");
    }

    private void ValidateCaveWarps()
    {
        ValidateLargeRoomCaveWarp(0x21, 0x04);
        ValidateLargeDungeonTopTransition();
        ValidateLargeRoomCaveWarp(0x28, 0xce);
        GD.Print("Validated 0:48 cave entries and dungeon00 room 4:04 -> 4:03 top transition.");
    }

    private void ValidateLargeDungeonTopTransition()
    {
        float exitX = -1.0f;
        for (float x = 8.0f; x < _currentRoom.Width; x++)
        {
            if (!Collides(new Vector2(x, -2.0f)))
            {
                exitX = x;
                break;
            }
        }
        if (exitX < 0.0f)
            throw new InvalidOperationException("Could not find 4:04's open northern dungeon exit.");

        _player.WarpTo(new Vector2(exitX, -2.0f));
        CheckRoomExit(_player);
        if (_activeGroup != 4 || _currentRoom.Id != 0x03 || !_scrollTransitionActive)
            throw new InvalidOperationException(
                $"Expected dungeon00 room 4:04 north to lead to 4:03, got {_activeGroup}:{_currentRoom.Id:x2}.");
        if (_scrollTransitionFrames != 32 || !Mathf.IsEqualApprox(_scrollTransitionDistance, 128.0f))
            throw new InvalidOperationException("Large-room vertical scrolling did not use the 128px playfield distance.");

        FinishActiveScrollingTransitionForValidation();
        if (Mathf.Abs(WorldToScreen(_player.Position).Y - 118.0f) > 0.01f)
            throw new InvalidOperationException("Link did not finish 4:04 -> 4:03 at the lower playfield edge.");
    }

    private void ValidateLargeRoomCaveWarp(int sourcePosition, int destinationRoom)
    {
        WarpToNpcTest();
        int tileX = sourcePosition & 0x0f;
        int tileY = (sourcePosition >> 4) & 0x0f;
        _player.WarpTo(new Vector2(
            tileX * OracleRoomData.MetatileSize + 8,
            tileY * OracleRoomData.MetatileSize + 8));
        if (!CheckTileWarp(_player) || _activeGroup != 4 || _currentRoom.Id != destinationRoom)
            throw new InvalidOperationException(
                $"Expected 0:48/${sourcePosition:x2} to enter 4:{destinationRoom:x2}, got " +
                $"{_activeGroup}:{_currentRoom.Id:x2}.");
        int expectedWidth = OracleRoomData.LargeRoomWidthInTiles * OracleRoomData.MetatileSize;
        int expectedHeight = OracleRoomData.LargeRoomHeightInTiles * OracleRoomData.MetatileSize;
        if (_currentRoom.Width != expectedWidth || _currentRoom.Height != expectedHeight ||
            _currentRoom.Texture.GetWidth() != expectedWidth ||
            _currentRoom.Texture.GetHeight() != expectedHeight)
            throw new InvalidOperationException(
                $"Expected 4:{destinationRoom:x2} to use the original 240x176 playable large-room dimensions.");
        if (_player.Position != new Vector2(0x78, 0xb0))
            throw new InvalidOperationException(
                $"Expected the original large-room entry coordinate $b0/$78, got {_player.Position}.");

        UpdateRoomCamera();
        if (WorldToScreen(_player.Position).DistanceSquaredTo(new Vector2(80, 128)) > 0.01f)
            throw new InvalidOperationException(
                $"Link did not begin the 4:{destinationRoom:x2} cave entry at screen position (80,128).");
        UpdateRoomWarpTransition(WarpEnterFrames / 60.0);
        UpdateRoomCamera();
        if (WorldToScreen(_player.Position).DistanceSquaredTo(new Vector2(80, 100)) > 0.01f)
            throw new InvalidOperationException(
                $"Link did not finish the 28-frame 4:{destinationRoom:x2} cave entry at screen position (80,100).");
        UpdateRoomWarpTransition((WarpFadeFrames - WarpEnterFrames) / 60.0);
        if (IsTransitioning)
            throw new InvalidOperationException($"The 4:{destinationRoom:x2} cave fade did not finish.");

        _player.WarpTo(new Vector2(_currentRoom.Width - 1, _currentRoom.Height / 2.0f));
        UpdateRoomCamera();
        if (Mathf.Abs(WorldToScreen(new Vector2(_currentRoom.Width, 0)).X -
            OracleRoomData.ViewportWidth) > 0.01f)
        {
            throw new InvalidOperationException(
                $"The 4:{destinationRoom:x2} camera exposed the padded 16th large-room column.");
        }
        if (!_collision.Collides(new Vector2(
            OracleRoomData.LargeRoomWidthInTiles * OracleRoomData.MetatileSize + 5,
            _currentRoom.Height / 2.0f)))
        {
            throw new InvalidOperationException(
                $"The 4:{destinationRoom:x2} padded 16th large-room column allowed Link out of bounds.");
        }
    }

    private void ValidateSwordBush()
    {
        OracleRandom.Result ExpectedNextRandom()
        {
            var replay = new OracleRandom();
            for (int call = 0; call < _random.Calls; call++)
                replay.Next();
            return replay.Next();
        }

        int SoundFor(OracleRandom.Result result) => (result.Value & 0x07) switch
        {
            0 or 3 or 4 or 6 or 7 => OracleSoundEngine.SndSwordSlash,
            1 or 5 => OracleSoundEngine.SndUnknown5,
            _ => OracleSoundEngine.SndBoomerang
        };

        WarpToBushTest();
        Vector2 bushPoint = new(24, 56);
        if (_currentRoom.GetMetatile(bushPoint) != 0xc5)
            throw new InvalidOperationException("Expected overworld bush $c5 in room 69 at $31.");
        Vector2 objectPosition = _player.Position;
        _sound.ClearPlayRequestAudit();
        int randomCalls = _random.Calls;
        OracleRandom.Result expectedRandom = ExpectedNextRandom();
        _player.StartSwordAttack();
        int slashRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndSwordSlash) +
            _sound.PlayRequestsFor(OracleSoundEngine.SndUnknown5) +
            _sound.PlayRequestsFor(OracleSoundEngine.SndBoomerang);
        if (slashRequests != 1 || _player.SwordState != Player.SwordActionState.Swing ||
            _player.SwordStateFrame != 0 || _player.SwordArcIndex != 0 ||
            _random.Calls != randomCalls + 1 || _random.LastResult != expectedRandom ||
            _sound.PlayRequestsFor(SoundFor(expectedRandom)) != 1)
        {
            throw new InvalidOperationException(
                "Starting ITEM_SWORD did not select one entry from the original 8-sound table " +
                "from shared RNG and initialize LINK_ANIM_MODE_22 at sword arc $00.");
        }
        _player.AdvanceSwordForValidation(2, buttonHeld: false);
        _sound.ClearPlayRequestAudit();
        randomCalls = _random.Calls;
        _player.StartSwordAttack();
        if (_player.SwordCanRestart || _player.SwordStateFrame != 2 ||
            _random.Calls != randomCalls ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSwordSlash) +
            _sound.PlayRequestsFor(OracleSoundEngine.SndUnknown5) +
            _sound.PlayRequestsFor(OracleSoundEngine.SndBoomerang) != 0)
        {
            throw new InvalidOperationException(
                "The protected first three sword updates accepted an equal-priority restart.");
        }
        _player.AdvanceSwordForValidation(1, buttonHeld: false);
        if (!_player.SwordCanRestart)
            throw new InvalidOperationException(
                "The sword did not become restartable when animation parameter `$02 cleared enabled bit 7.");
        randomCalls = _random.Calls;
        expectedRandom = ExpectedNextRandom();
        _player.StartSwordAttackForValidation(Vector2.Right);
        slashRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndSwordSlash) +
            _sound.PlayRequestsFor(OracleSoundEngine.SndUnknown5) +
            _sound.PlayRequestsFor(OracleSoundEngine.SndBoomerang);
        if (slashRequests != 1 || _player.SwordStateFrame != 0 ||
            _player.SwordArcIndex != 1 || _player.FacingVector != Vector2I.Right ||
            _player.SwordCanRestart || _random.Calls != randomCalls + 1 ||
            _random.LastResult != expectedRandom ||
            _sound.PlayRequestsFor(SoundFor(expectedRandom)) != 1)
        {
            throw new InvalidOperationException(
                "An equal-priority sword press did not consume shared RNG, restart, and " +
                "retarget the single swing after update 3.");
        }
        _player.AdvanceSwordForValidation(3, buttonHeld: false);
        _sound.ClearPlayRequestAudit();
        randomCalls = _random.Calls;
        expectedRandom = ExpectedNextRandom();
        _player.StartSwordAttackForValidation(Vector2.Up);
        slashRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndSwordSlash) +
            _sound.PlayRequestsFor(OracleSoundEngine.SndUnknown5) +
            _sound.PlayRequestsFor(OracleSoundEngine.SndBoomerang);
        if (slashRequests != 1 || _player.SwordStateFrame != 0 ||
            _player.SwordArcIndex != 0 || _player.FacingVector != Vector2I.Up ||
            _player.SwordCanRestart || _random.Calls != randomCalls + 1 ||
            _random.LastResult != expectedRandom ||
            _sound.PlayRequestsFor(SoundFor(expectedRandom)) != 1)
        {
            throw new InvalidOperationException(
                "Spammed sword input did not consume shared RNG and retarget a subsequent swing upward.");
        }
        if (_player.AttackSpriteOrigin != new Vector2(-8, -8))
            throw new InvalidOperationException(
                $"Sword frame $ac displaced Link from the standard OAM origin: {_player.AttackSpriteOrigin}.");
        if (_player.SwordSpritePosition != new Vector2(16, -4))
            throw new InvalidOperationException(
                $"Sword arc phase $00 did not include the child item's -2 Z draw offset: {_player.SwordSpritePosition}.");
        _player._Process(7.0 / 60.0);
        if (_player.Position != objectPosition)
            throw new InvalidOperationException("Swinging the sword changed Link's object position.");
        if (_player.AttackSpriteOrigin != new Vector2(-8, -11))
            throw new InvalidOperationException(
                $"Sword frame $b4 did not apply only its original OAM $08 pose offset: {_player.AttackSpriteOrigin}.");
        if (_player.SwordSpritePosition != new Vector2(-4, -19))
            throw new InvalidOperationException(
                $"Sword arc phase $08 did not include the child item's -2 Z draw offset: {_player.SwordSpritePosition}.");
        if (_currentRoom.GetMetatile(bushPoint) != 0x3a)
            throw new InvalidOperationException("The level-1 sword did not replace bush $c5 with ground $3a.");
        if (_currentRoom.IsSolid(bushPoint))
            throw new InvalidOperationException("The cut bush's replacement tile remained solid.");
        if (_sound.PlayRequestsFor(OracleSoundEngine.SndCutGrass) != 1)
            throw new InvalidOperationException("INTERAC_GRASSDEBRIS did not request SND_CUTGRASS once.");

        // Complete LINK_ANIM_MODE_22 while preserving the initiating button.
        // State 6 must re-enable movement but keep turning disabled and expose
        // the fourth normal swordArcData row continuously while charging.
        _player.AdvanceSwordForValidation(9, buttonHeld: true);
        if (_player.SwordState != Player.SwordActionState.Swing ||
            _player.SwordStateFrame != 16)
            throw new InvalidOperationException("The sword swing ended before its 17th update.");
        _player.AdvanceSwordForValidation(1, buttonHeld: true);
        if (_player.SwordState != Player.SwordActionState.Held ||
            !_player.SwordAllowsMovement || !_player.SwordCanRestart || _player.SwordArcIndex != 12 ||
            _player.SwordSpritePosition != new Vector2(-4, -12))
        {
            throw new InvalidOperationException(
                "Holding the sword button did not enter the movable ITEMCOLLISION_SWORD_HELD state " +
                "with the original up-facing arc $0c.");
        }
        if (Player.GetHeldSwordBodyAnimationFrameForValidation(
                Player.SwordActionState.Held, walking: true, walkTime: 0.0f) != 0 ||
            Player.GetHeldSwordBodyAnimationFrameForValidation(
                Player.SwordActionState.Held, walking: true, walkTime: 0.10f) != 1 ||
            Player.GetHeldSwordBodyAnimationFrameForValidation(
                Player.SwordActionState.Charged, walking: true, walkTime: 0.20f) != 0 ||
            Player.GetHeldSwordBodyAnimationFrameForValidation(
                Player.SwordActionState.Held, walking: false, walkTime: 0.10f) != 0 ||
            Player.GetHeldSwordBodyAnimationFrameForValidation(
                Player.SwordActionState.Swing, walking: true, walkTime: 0.10f) != -1)
        {
            throw new InvalidOperationException(
                "Held/charged sword state did not select Link's ordinary standing/walking body.");
        }
        if (Player.GetSwordSpritePositionForValidation(13) != new Vector2(12, 0) ||
            Player.GetSwordSpritePositionForValidation(15) != new Vector2(-12, 0))
        {
            throw new InvalidOperationException(
                "Held horizontal sword sprites did not apply the child item's -2 Z draw offset.");
        }

        _player.AdvanceSwordForValidation(40, buttonHeld: true);
        if (_player.SwordState != Player.SwordActionState.Held ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndChargeSword) != 0)
            throw new InvalidOperationException("Sword counter `$28 charged without the original underflow update.");
        _player.AdvanceSwordForValidation(1, buttonHeld: true);
        if (_player.SwordState != Player.SwordActionState.Charged ||
            _player.SwordCanRestart || _player.SwordUsesChargedPalette ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndChargeSword) != 1)
            throw new InvalidOperationException("The 41st held update did not enter the charged state with SND_CHARGE_SWORD.");
        _player.AdvanceSwordForValidation(3, buttonHeld: true);
        if (_player.SwordUsesChargedPalette)
            throw new InvalidOperationException("The charged sword selected palette 5 before counter bit 2 was set.");
        _player.AdvanceSwordForValidation(1, buttonHeld: true);
        if (!_player.SwordUsesChargedPalette)
            throw new InvalidOperationException("The charged sword did not select palette 5 when counter bit 2 became set.");

        _player.AdvanceSwordForValidation(1, buttonHeld: false);
        if (_player.SwordState != Player.SwordActionState.Spin ||
            _player.SwordStateFrame != 0 || _player.SwordAllowsMovement ||
            _player.SwordArcIndex != 16 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSwordSpin) != 1)
        {
            throw new InvalidOperationException(
                "Releasing a charged up-facing sword did not begin the immobilized arc `$10 spin with SND_SWORDSPIN.");
        }
        _player.AdvanceSwordForValidation(2, buttonHeld: false);
        if (_player.SwordArcIndex != 16)
            throw new InvalidOperationException("Swordspin arc `$10 did not retain its original 3-update duration.");
        _player.AdvanceSwordForValidation(1, buttonHeld: false);
        if (_player.SwordArcIndex != 17)
            throw new InvalidOperationException("Swordspin did not enter diagonal arc `$11 on update 3.");
        _player.AdvanceSwordForValidation(2, buttonHeld: false);
        if (_player.SwordArcIndex != 18)
            throw new InvalidOperationException("Swordspin did not enter right-facing arc `$12 on update 5.");
        _player.AdvanceSwordForValidation(17, buttonHeld: false);
        if (_player.SwordState != Player.SwordActionState.Spin ||
            _player.SwordStateFrame != 22 || _player.SwordArcIndex != 16)
            throw new InvalidOperationException("Swordspin did not retain its wrapped arc through update 22.");
        _player.AdvanceSwordForValidation(1, buttonHeld: false);
        if (_player.IsAttacking)
            throw new InvalidOperationException("Swordspin did not end on its original 23rd update.");

        // A held sword pressed into a full wall switches to LINK_ANIM_MODE_1f,
        // clears weapon collision for 12 updates, and emits the ordinary clink.
        _currentRoom.SetPositionTileAndCollision(
            bushPoint, 0x3a, 0x0f, (long)_animationTicks);
        _player.WarpTo(new Vector2(bushPoint.X, 66));
        _player.Face(Vector2I.Up);
        _player.StartSwordAttack();
        _player.AdvanceSwordForValidation(17, buttonHeld: true);
        _sound.ClearPlayRequestAudit();
        _combat.ClearClinkEffectAudit();
        _player.AdvanceSwordForValidation(1, buttonHeld: true, movementInput: Vector2.Up);
        if (_player.SwordState != Player.SwordActionState.Poke ||
            !_player.SwordCanRestart || _player.GetSwordHitbox().Size != Vector2.Zero ||
            _player.AttackSpriteOrigin != new Vector2(-8, -11) ||
            _player.SwordSpritePosition != new Vector2(-4, -19) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndClink) != 1)
        {
            throw new InvalidOperationException(
                "Held-sword wall pressure did not enter the collision-disabled 12-update poke and play SND_CLINK.");
        }
        ClinkEffect? ordinaryClink = _combat.LastClinkEffect;
        Vector2 expectedClinkPosition = _player.Position + new Vector2(0, -14);
        if (_combat.ClinkEffectsSpawned != 1 || ordinaryClink is null ||
            ordinaryClink.Position != expectedClinkPosition || !ordinaryClink.Flickers ||
            ordinaryClink.DurationFrames != 8 || ordinaryClink.AnimationFrame != 0 ||
            !ordinaryClink.EffectVisible)
        {
            throw new InvalidOperationException(
                "Ordinary wall pressure did not spawn flickering INTERAC_CLINK at the up-facing `$f2/$00 probe.");
        }
        ordinaryClink.AdvanceForValidation(1.0 / 60.0);
        if (ordinaryClink.EffectVisible || ordinaryClink.AnimationFrame != 0)
            throw new InvalidOperationException("INTERAC_CLINK did not flicker during its first 4-update frame.");
        ordinaryClink.AdvanceForValidation(3.0 / 60.0);
        if (!ordinaryClink.EffectVisible || ordinaryClink.AnimationFrame != 1)
            throw new InvalidOperationException("INTERAC_CLINK did not enter its second OAM frame after 4 updates.");
        _player.AdvanceSwordForValidation(11, buttonHeld: true);
        if (_player.SwordState != Player.SwordActionState.Poke ||
            _player.SwordStateFrame != 11)
            throw new InvalidOperationException("LINK_ANIM_MODE_1f ended before update 12.");
        _player.AdvanceSwordForValidation(1, buttonHeld: true);
        if (_player.SwordState != Player.SwordActionState.Held ||
            _player.SwordArcIndex != 12)
            throw new InvalidOperationException("A wall poke did not reinitialize the held sword after update 12.");
        _player.AdvanceSwordForValidation(1, buttonHeld: false);
        if (_player.IsAttacking)
            throw new InvalidOperationException("Releasing an uncharged held sword did not clear the parent item.");

        // Bombable wall tiles bypass the poke-only ordinary clink condition.
        _currentRoom.SetPositionTileAndCollision(
            bushPoint, 0xc1, null, (long)_animationTicks);
        _sound.ClearPlayRequestAudit();
        _combat.ClearClinkEffectAudit();
        _player.StartSwordAttack();
        _player.AdvanceSwordForValidation(6, buttonHeld: false);
        ClinkEffect? bombableClink = _combat.LastClinkEffect;
        if (_sound.PlayRequestsFor(OracleSoundEngine.SndClink2) != 1 ||
            _combat.ClinkEffectsSpawned != 1 || bombableClink is null ||
            bombableClink.Position != expectedClinkPosition || bombableClink.Flickers ||
            !bombableClink.EffectVisible)
        {
            throw new InvalidOperationException(
                "Bombable overworld tile `$c1 did not play SND_CLINK2 and spawn non-flickering INTERAC_CLINK.");
        }
        _player.AdvanceSwordForValidation(11, buttonHeld: false);
        _currentRoom.SetPositionTileAndCollision(
            bushPoint, 0x3a, null, (long)_animationTicks);

        (int RadiusY, int RadiusX, int OffsetY, int OffsetX)[] expectedArcs =
        {
            (9, 6, -2, 16), (6, 9, -14, 0), (9, 6, 0, -15), (6, 9, -14, 0),
            (7, 7, -11, 13), (7, 7, -11, 13), (7, 7, 17, -13), (7, 7, -11, -13),
            (9, 6, -17, -4), (6, 9, 2, 19), (9, 6, 21, 3), (6, 9, 2, -19),
            (9, 6, -10, -4), (4, 9, 2, 12), (9, 6, 16, 3), (6, 9, 2, -12),
            (9, 9, -17, -4), (9, 9, -14, 16), (9, 9, 2, 19), (9, 9, 18, 16),
            (9, 9, 21, 3), (9, 9, 17, -13), (9, 9, 2, -19), (9, 9, -11, -13)
        };
        Vector2 auditPosition = new(80, 64);
        for (int index = 0; index < expectedArcs.Length; index++)
        {
            var arc = expectedArcs[index];
            Rect2 expected = new(
                auditPosition + new Vector2(
                    arc.OffsetX - arc.RadiusX,
                    arc.OffsetY - arc.RadiusY),
                new Vector2(arc.RadiusX * 2, arc.RadiusY * 2));
            Rect2 actual = Player.GetSwordHitboxForValidation(auditPosition, index);
            if (actual != expected)
                throw new InvalidOperationException(
                    $"swordArcData row `${index:x2} mismatch: expected {expected}, got {actual}.");
        }

        // wScrollMode $08 freezes initialized item objects. The held sword
        // parent and its locked facing must therefore survive both ends of a
        // scrolling transition without charging or observing button release.
        _player.Face(Vector2I.Up);
        _player.StartSwordAttack();
        _player.AdvanceSwordForValidation(17, buttonHeld: true);
        _transitions.BeginScroll(_player, Vector2I.Right, 0x6a);
        if (!_transitions.ScrollActive ||
            _player.SwordState != Player.SwordActionState.Held ||
            _player.FacingVector != Vector2I.Up || _player.SwordArcIndex != 12)
        {
            throw new InvalidOperationException(
                "Scrolling right did not preserve the up-facing held sword parent item.");
        }
        _player._Process(1.0);
        if (_player.SwordState != Player.SwordActionState.Held ||
            _player.SwordStateFrame != 0 || _player.FacingVector != Vector2I.Up)
        {
            throw new InvalidOperationException(
                "wScrollMode $08 did not freeze the held sword for the scrolling transition.");
        }
        _transitions.UpdateScroll(1.0);
        if (_transitions.ScrollActive ||
            _player.SwordState != Player.SwordActionState.Held ||
            _player.FacingVector != Vector2I.Up || _player.SwordArcIndex != 12)
        {
            throw new InvalidOperationException(
                "Finishing the scrolling transition cleared or redirected the held sword.");
        }
        _player.AdvanceSwordForValidation(1, buttonHeld: false);

        GD.Print(
            "Validated ITEM_SWORD's 17-update swing/3-update directional restart gate, " +
            "held collision/movement and scrolling-transition persistence, " +
            "41-update charge, " +
            "held/charged standing/walking body, child-item Z/layer rendering, charged palette cadence, " +
            "12-update wall poke/clinks with 8-update INTERAC_CLINK sprites, 23-update swordspin, " +
            "shared-RNG slash sounds, blocked-restart RNG preservation, grass break, " +
            "and all 24 swordArcData hitboxes.");
    }

    private void ValidateEnemyPlacementRules()
    {
        var spawnTiles = new EnemySpawnTileDatabase();
        var normal = new OracleRoomData.TerrainInfo(
            0x1a, 0x00, OracleRoomData.TerrainType.Normal,
            OracleRoomData.HazardType.None);
        var collision10Water = new OracleRoomData.TerrainInfo(
            0xfc, 0x10, OracleRoomData.TerrainType.SeaWater,
            OracleRoomData.HazardType.Water);
        var overworldWhirlpool = new OracleRoomData.TerrainInfo(
            0xe9, 0x00, OracleRoomData.TerrainType.Whirlpool,
            OracleRoomData.HazardType.Water);
        var dungeonException = new OracleRoomData.TerrainInfo(
            0x44, 0x00, OracleRoomData.TerrainType.Normal,
            OracleRoomData.HazardType.None);
        var sidescrollHoleIndex = new OracleRoomData.TerrainInfo(
            0xf3, 0x00, OracleRoomData.TerrainType.Normal,
            OracleRoomData.HazardType.None);
        if (spawnTiles.RecordCount != 63 ||
            !spawnTiles.IsValid(0, normal) ||
            spawnTiles.IsValid(0, collision10Water) ||
            spawnTiles.IsValid(0, overworldWhirlpool) ||
            spawnTiles.IsValid(2, dungeonException) ||
            !spawnTiles.IsValid(3, sidescrollHoleIndex))
        {
            throw new InvalidOperationException(
                "checkTileValidForEnemySpawn did not require collision `$00 or " +
                "select the original exception list by wActiveCollisions.");
        }

        OracleRoomData room1db = _world.LoadRoom(1, 0xdb);
        int validCells = 0;
        int rejectedWaterCells = 0;
        int centerOpenButRejected = 0;
        for (int tileY = 0; tileY < room1db.HeightInTiles; tileY++)
        for (int tileX = 0; tileX < room1db.WidthInTiles; tileX++)
        {
            Vector2 center = new(
                tileX * OracleRoomData.MetatileSize + 8,
                tileY * OracleRoomData.MetatileSize + 8);
            OracleRoomData.TerrainInfo terrain = room1db.GetTerrainInfo(center);
            bool valid = spawnTiles.IsValid(room1db.ActiveCollisions, terrain);
            if (valid)
                validCells++;
            if (terrain is { Tile: 0xfc, Collision: 0x10 })
            {
                if (valid)
                    throw new InvalidOperationException(
                        "Room 1:db deep water remained valid for random enemies.");
                rejectedWaterCells++;
            }
            if (!room1db.IsSolid(center) && !valid)
                centerOpenButRejected++;
        }
        if (validCells != 32 || rejectedWaterCells != 18 ||
            centerOpenButRejected != 19)
        {
            throw new InvalidOperationException(
                $"Room 1:db enemy-placement pool mismatch: valid={validCells}, " +
                $"water={rejectedWaterCells}, center-open rejected={centerOpenButRejected}.");
        }

        EnemyPlacementContext warp = EnemyPlacementContext.Warp(0x33);
        EnemyPlacementContext scrollUp = EnemyPlacementContext.Scrolling(Vector2I.Up);
        EnemyPlacementContext scrollRight = EnemyPlacementContext.Scrolling(Vector2I.Right);
        EnemyPlacementContext scrollDown = EnemyPlacementContext.Scrolling(Vector2I.Down);
        EnemyPlacementContext scrollLeft = EnemyPlacementContext.Scrolling(Vector2I.Left);
        OracleRoomData largeRoom = _world.LoadRoom(4, 0x39);
        if (warp.Allows(room1db, 0x11) || !warp.Allows(room1db, 0x03) ||
            scrollUp.Allows(room1db, 0x59) || !scrollUp.Allows(room1db, 0x49) ||
            scrollRight.Allows(room1db, 0x42) || !scrollRight.Allows(room1db, 0x43) ||
            scrollDown.Allows(room1db, 0x29) || !scrollDown.Allows(room1db, 0x39) ||
            scrollLeft.Allows(room1db, 0x47) || !scrollLeft.Allows(room1db, 0x46) ||
            scrollLeft.Allows(largeRoom, 0x5b) || !scrollLeft.Allows(largeRoom, 0x5a) ||
            EnemyPlacementContext.FromWarpDestination(0xf5).Allows(room1db, 0x59))
        {
            throw new InvalidOperationException(
                "checkPositionValidForEnemySpawn lost its warp square, incoming " +
                "three-tile edge, or large-room DIR_LEFT `$0b boundary.");
        }

        var validationRoot = new Node { Name = "EnemySpawnTerrainValidation" };
        AddChild(validationRoot);
        var enemies = new EnemyDatabase();
        for (int rngAdvance = 0; rngAdvance <= 24; rngAdvance++)
        {
            var random = new OracleRandom();
            for (int index = 0; index < rngAdvance; index++)
                random.Next();
            var manager = new RoomEntityManager(
                validationRoot, new NpcDatabase(), enemies,
                new ItemDropDatabase(), new TimePortalDatabase(), random);
            manager.LoadRoom(1, room1db);
            List<OctorokCharacter> octoroks = manager.Entities<OctorokCharacter>();
            if (octoroks.Count != 2 || octoroks.Any(octorok =>
                !spawnTiles.IsValid(
                    room1db.ActiveCollisions,
                    room1db.GetTerrainInfo(octorok.Position))))
            {
                throw new InvalidOperationException(
                    $"Room 1:db placed a blue Octorok on ROM-invalid terrain " +
                    $"after {rngAdvance} pre-parse RNG calls.");
            }
            manager.Clear();
        }
        RemoveChild(validationRoot);
        validationRoot.Free();

        GD.Print("Validated 63 enemy-unspawnable tile records, whole-metatile collision " +
            "checks, room 1:db's 32-cell land pool / 18 rejected water cells, 25 RNG " +
            "states, warp-distance exclusion, and all scrolling entry boundaries.");
    }

    private void ValidateEnemyObjectPlacementOrder()
    {
        var database = new EnemyDatabase();
        IReadOnlyList<EnemyDatabase.RoomObjectRecord> room5b0 =
            database.GetRoomObjects(5, 0xb0);
        IReadOnlyList<EnemyDatabase.RoomObjectRecord> room5db =
            database.GetRoomObjects(5, 0xdb);
        IReadOnlyList<EnemyDatabase.RoomObjectRecord> room501 =
            database.GetRoomObjects(5, 0x01);
        IReadOnlyList<EnemyDatabase.RoomObjectRecord> room036 =
            database.GetRoomObjects(0, 0x36);
        IReadOnlyList<EnemyDatabase.RoomObjectRecord> room006 =
            database.GetRoomObjects(0, 0x06);
        if (database.RoomObjectRecordCount != 1141 ||
            room5b0.Count != 3 ||
            room5b0[0] is not { Order: 0, Kind: EnemyDatabase.RoomObjectKind.FixedEnemy,
                Id: 0x1b, PackedPosition: 0x63 } ||
            room5b0[1] is not { Order: 1, Kind: EnemyDatabase.RoomObjectKind.FixedEnemy,
                Id: 0x34, PackedPosition: 0x75 } ||
            room5b0[2] is not { Order: 2, Kind: EnemyDatabase.RoomObjectKind.RandomEnemy,
                Id: 0x32, Count: 2 } ||
            room5db.Count != 7 ||
            room5db.Take(4).Any(record =>
                record.Kind != EnemyDatabase.RoomObjectKind.ItemDrop) ||
            room5db[6] is not { Kind: EnemyDatabase.RoomObjectKind.RandomEnemy,
                Id: 0x32, Count: 3 } ||
            room501.Count != 3 ||
            room501[0].Kind != EnemyDatabase.RoomObjectKind.ReservingPart ||
            room501[1].Kind != EnemyDatabase.RoomObjectKind.ReservingPart ||
            room501[2].Kind != EnemyDatabase.RoomObjectKind.RandomEnemy ||
            room036.Count != 2 || room036[0].ConditionMask != 0x01 ||
            room036[1].ConditionMask != 0xff ||
            room006.Count != 1 || room006[0].Order != 0)
        {
            throw new InvalidOperationException(
                "The ordered room-object stream lost source order, aliases, conditions, " +
                "unsupported enemies, item drops, or reserving parts.");
        }

        var wrappedReservations = new EnemyPlacementReservations();
        for (int position = 0; position < 15; position++)
            wrappedReservations.Add(position);
        if (wrappedReservations.Count != 15 || !wrappedReservations.Contains(0x00) ||
            !wrappedReservations.Contains(0x0e))
        {
            throw new InvalidOperationException(
                "wPlacedEnemyPositions did not retain its first 15 packed positions.");
        }
        wrappedReservations.Add(0x0f);
        if (wrappedReservations.Count != 0 || wrappedReservations.Contains(0x00) ||
            wrappedReservations.Contains(0x0f))
        {
            throw new InvalidOperationException(
                "wEnemyPlacement.numEnemies did not wrap after its 16th reservation.");
        }
        wrappedReservations.Add(0xaa);
        if (wrappedReservations.Count != 1 || !wrappedReservations.Contains(0xaa) ||
            wrappedReservations.Contains(0x01))
        {
            throw new InvalidOperationException(
                "The wrapped placement table did not restart from entry zero.");
        }

        var validationRoot = new Node { Name = "EnemyPlacementOrderValidation" };
        AddChild(validationRoot);
        var npcs = new NpcDatabase();
        var itemDrops = new ItemDropDatabase();
        var timePortals = new TimePortalDatabase();

        static int Pack(Vector2 position) =>
            ((int)position.Y & 0xf0) | (((int)position.X >> 4) & 0x0f);

        void ValidateRoom(
            int group,
            int roomId,
            int rngAdvance,
            int expectedKeese,
            int expectedZols,
            params int[] reservedPositions)
        {
            var random = new OracleRandom();
            for (int index = 0; index < rngAdvance; index++)
                random.Next();
            var manager = new RoomEntityManager(
                validationRoot, npcs, database, itemDrops, timePortals, random);
            manager.LoadRoom(group, _world.LoadRoom(group, roomId));

            List<KeeseCharacter> keese = manager.Entities<KeeseCharacter>();
            List<ZolCharacter> zols = manager.Entities<ZolCharacter>();
            int[] allPacked = keese.Select(enemy => Pack(enemy.Position))
                .Concat(zols.Select(enemy => Pack(enemy.Position)))
                .ToArray();
            HashSet<int> reserved = reservedPositions.ToHashSet();
            if (keese.Count != expectedKeese || zols.Count != expectedZols ||
                allPacked.Distinct().Count() != allPacked.Length ||
                keese.Any(enemy => reserved.Contains(Pack(enemy.Position))))
            {
                throw new InvalidOperationException(
                    $"Room {group:x1}:{roomId:x2} lost ordered placement reservations " +
                    $"(Keese={string.Join(',', keese.Select(enemy => $"${Pack(enemy.Position):x2}"))}, " +
                    $"Zols={string.Join(',', zols.Select(enemy => $"${Pack(enemy.Position):x2}"))}).");
            }
            manager.Clear();
        }

        // These advances deliberately make the first shuffled candidate collide
        // with a reservation: fixed Zol $43, unsupported enemy $63, and item $1d.
        ValidateRoom(4, 0x9a, 11, 2, 2, 0x39, 0x43);
        ValidateRoom(5, 0xb0, 30, 2, 1, 0x63, 0x75);
        ValidateRoom(5, 0xdb, 16, 3, 2, 0x1d, 0x92, 0x82, 0x83, 0x73, 0x63);

        RemoveChild(validationRoot);
        validationRoot.Free();
        GD.Print("Validated 1,141 ordered room placement records, mid-stream aliases, " +
            "condition masks, 16-entry reservation wrapping, and fixed/unsupported/item " +
            "reservations before random Keese in rooms 4:9a, 5:b0, and 5:db.");
    }

    private void ValidateKeese()
    {
        var database = new EnemyDatabase();
        if (database.KeeseRecordCount != 53 || database.KeeseInstanceCount != 158)
            throw new InvalidOperationException(
                $"Expected 53 ENEMY_KEESE room records / 158 instances, got " +
                $"{database.KeeseRecordCount} / {database.KeeseInstanceCount}.");

        LoadValidationRoom(4, 0x39);
        if (_entities.Entities<KeeseCharacter>().Count != 2 || _entities.Entities<KeeseCharacter>().Exists(keese => keese.Record.SubId != 1))
            throw new InvalidOperationException(
                $"Room 4:39 should contain two random-position ENEMY_KEESE subid `$01 objects, " +
                $"got {_entities.Entities<KeeseCharacter>().Count}.");

        KeeseCharacter approachKeese = _entities.Entities<KeeseCharacter>()[0];
        if (approachKeese.SpriteHeight != -1)
            throw new InvalidOperationException("Keese subid `$01 did not preserve its original z-height `$ff.");
        approachKeese.Position = new Vector2(80, 80);
        _player.WarpTo(new Vector2(128, 80));
        _entities.Update(1.0 / 60.0, _player);
        if (approachKeese.State != KeeseCharacter.KeeseState.Moving ||
            approachKeese.Counter1 != 12 || approachKeese.Counter2 != 12 ||
            !approachKeese.Flying)
            throw new InvalidOperationException(
                "Keese subid `$01 did not wake inside strict Manhattan distance `$31 with 12x12 counters.");

        for (int frame = 0; frame < 4; frame++)
            _entities.Update(1.0 / 60.0, _player);
        if (approachKeese.CurrentAnimationFrame != 1)
            throw new InvalidOperationException(
                "Flying Keese did not switch OAM frames after the original 4 animation calls.");
        for (int frame = 4; frame < 144; frame++)
            _entities.Update(1.0 / 60.0, _player);
        if (approachKeese.State != KeeseCharacter.KeeseState.Resting || approachKeese.Flying)
            throw new InvalidOperationException(
                "Keese subid `$01 did not return to rest after 12 turning intervals of 12 updates.");

        LoadValidationRoom(4, 0xcb);
        if (_entities.Entities<KeeseCharacter>().Count != 4 || _entities.Entities<KeeseCharacter>().Exists(keese => keese.Record.SubId != 0))
            throw new InvalidOperationException(
                $"Room 4:cb should contain four random-position ENEMY_KEESE subid `$00 objects, " +
                $"got {_entities.Entities<KeeseCharacter>().Count}.");

        KeeseCharacter normalKeese = _entities.Entities<KeeseCharacter>()[0];
        normalKeese.Position = new Vector2(48, 48);
        _player.WarpTo(new Vector2(160, 120));
        _entities.Update(31.0 / 60.0, _player);
        if (normalKeese.State != KeeseCharacter.KeeseState.Resting || normalKeese.Counter1 != 1)
            throw new InvalidOperationException(
                "Normal Keese did not preserve its original 32-update initial rest counter.");
        _entities.Update(1.0 / 60.0, _player);
        if (normalKeese.State != KeeseCharacter.KeeseState.Moving || !normalKeese.Flying ||
            normalKeese.Counter1 is < 0xc0 or > 0xff)
            throw new InvalidOperationException(
                "Normal Keese did not choose its original random `$c0-`$ff flight counter and angle.");

        _player.RefillHealth();
        _player.WarpTo(normalKeese.Position, recordSafe: false);
        int healthBeforeContact = _player.HealthQuarters;
        _entities.Update(0.0, _player);
        if (_player.HealthQuarters != healthBeforeContact - 2 ||
            !Mathf.IsEqualApprox(_player.InvincibilityFrames, 0x22) ||
            !Mathf.IsEqualApprox(_player.KnockbackFrames, 0x0f))
            throw new InvalidOperationException(
                "Keese contact did not apply half-heart damage, 34 invincibility updates, and 15 knockback updates.");
        _entities.Update(0.0, _player);
        if (_player.HealthQuarters != healthBeforeContact - 2)
            throw new InvalidOperationException("Keese contact bypassed Link's invincibility counter.");

        _player.WarpTo(normalKeese.Position + Vector2.Down * 16.0f);
        Vector2 expectedPuffPosition = normalKeese.Position +
            Vector2.Down * normalKeese.SpriteHeight;
        int countBeforeSword = _entities.Entities<KeeseCharacter>().Count;
        if (!_entities.ApplySwordHit(normalKeese.CollisionBounds.Grow(1.0f)) ||
            _entities.Entities<KeeseCharacter>().Count != countBeforeSword - 1)
            throw new InvalidOperationException(
                "The level-1 sword did not defeat the 1-health ENEMY_KEESE in one hit.");
        if (_entities.Entities<EnemyDeathPuffEffect>().Count != 1 ||
            _entities.Entities<EnemyDeathPuffEffect>()[0].Position != expectedPuffPosition ||
            _entities.Entities<EnemyDeathPuffEffect>()[0].HighKnockback ||
            _entities.Entities<EnemyDeathPuffEffect>()[0].EnemyId != 0x32 ||
            _entities.Entities<EnemyDeathPuffEffect>()[0].DurationFrames != 20 ||
            _entities.Entities<EnemyDeathPuffEffect>()[0].CurrentPalette != 2)
        {
            throw new InvalidOperationException(
                "Defeating Keese did not create the ordinary 20-update PART_ENEMY_DESTROYED puff at its visual position.");
        }

        KeeseCharacter transitionKeese = _entities.Entities<KeeseCharacter>()[0];
        int transitionCounter = transitionKeese.Counter1;
        OracleRoomData incomingRoom = _world.LoadRoom(4, 0x39);
        _entities.BeginScreenTransition(4, incomingRoom, Vector2.Left * incomingRoom.Width);
        _entities.Update(1.0, _player);
        if (_entities.OutgoingEntities<KeeseCharacter>().Count != 3 || _entities.Entities<KeeseCharacter>().Count != 2 ||
            _entities.OutgoingEntities<EnemyDeathPuffEffect>().Count != 1 ||
            _entities.OutgoingEntities<EnemyDeathPuffEffect>()[0].ElapsedFrames != 0 ||
            transitionKeese.Counter1 != transitionCounter ||
            !_entities.Entities<KeeseCharacter>()[0].TransitionDrawOffset.IsEqualApprox(Vector2.Left * incomingRoom.Width))
            throw new InvalidOperationException(
                "Scrolling did not retain/freeze outgoing Keese/death puffs and preload/freeze destination Keese.");
        _entities.SetScreenTransitionOffsets(
            Vector2.Right * 4.0f,
            Vector2.Left * (incomingRoom.Width - 4.0f));
        if (!_entities.OutgoingEntities<EnemyDeathPuffEffect>()[0].TransitionDrawOffset.IsEqualApprox(Vector2.Right * 4.0f))
            throw new InvalidOperationException("The death puff did not move with its outgoing room during scrolling.");
        _entities.FinishScreenTransition();
        if (_entities.OutgoingEntities<KeeseCharacter>().Count != 0 || _entities.OutgoingEntities<EnemyDeathPuffEffect>().Count != 0 ||
            !_entities.Entities<KeeseCharacter>()[0].TransitionDrawOffset.IsEqualApprox(Vector2.Zero))
            throw new InvalidOperationException(
                "Keese/death-puff transition ownership and offsets were not normalized after scrolling.");

        var normalPuff = new EnemyDeathPuffEffect();
        normalPuff.Initialize(Vector2.Zero);
        for (int frame = 1; frame <= 20; frame++)
        {
            normalPuff.UpdateFrame(frame);
            if (frame == 1 &&
                (normalPuff.AnimationFrame != 0 || normalPuff.CurrentPalette != 2))
            {
                throw new InvalidOperationException(
                    "PART_ENEMY_DESTROYED changed frame/palette before its first 2-update record elapsed.");
            }
            if (frame == 2 &&
                (normalPuff.AnimationFrame != 1 || normalPuff.CurrentPalette != 3))
            {
                throw new InvalidOperationException(
                    "PART_ENEMY_DESTROYED did not advance after 2 updates and toggle OAM flags `$0a -> `$0b.");
            }
            if (frame < 20 && normalPuff.Finished)
                throw new InvalidOperationException("The ordinary enemy death puff ended before 20 updates.");
        }
        if (!normalPuff.Finished || normalPuff.ElapsedFrames != 20)
            throw new InvalidOperationException("The ordinary enemy death puff did not end after 20 updates.");
        normalPuff.Free();

        var highKnockbackPuff = new EnemyDeathPuffEffect();
        highKnockbackPuff.Initialize(Vector2.Zero, highKnockback: true);
        for (int frame = 1; frame <= 28; frame++)
        {
            highKnockbackPuff.UpdateFrame(frame);
            if (frame == 6 && highKnockbackPuff.AnimationFrame != 3)
                throw new InvalidOperationException(
                    "The high-knockback death puff did not enter its extra 8-update burst frame.");
            if (frame < 28 && highKnockbackPuff.Finished)
                throw new InvalidOperationException("The high-knockback enemy death puff ended before 28 updates.");
        }
        if (!highKnockbackPuff.Finished || highKnockbackPuff.DurationFrames != 28)
            throw new InvalidOperationException("The high-knockback enemy death puff did not end after 28 updates.");
        highKnockbackPuff.Free();
        _player.RefillHealth();

        GD.Print("Validated 53 imported ENEMY_KEESE room records, subid `$00/`$01 timing, " +
            "original RNG-driven flight, 4-update wing animation, collision radii, contact damage, " +
            "invincibility/knockback counters, one-hit level-1 sword defeat, common 20/28-update " +
            "death puffs with palette toggling, and retained/preloaded scrolling.");
    }

    private void ValidateOctoroks()
    {
        var database = new EnemyDatabase();
        if (database.OctorokRecordCount != 33 || database.OctorokInstanceCount != 48)
        {
            throw new InvalidOperationException(
                $"Expected 33 ENEMY_OCTOROK room records / 48 instances, got " +
                $"{database.OctorokRecordCount} / {database.OctorokInstanceCount}.");
        }
        EnemyDatabase.OctorokProjectileRecord projectile = database.OctorokProjectile;
        if (projectile.TileBase != 0x0c || projectile.Palette != 3 ||
            projectile.CollisionRadiusY != 2 || projectile.CollisionRadiusX != 2 ||
            projectile.DamageQuarters != 2 || projectile.SpeedRaw != 0x50)
        {
            throw new InvalidOperationException(
                "PART_OCTOROK_PROJECTILE did not retain tile `$0c, palette 3, radius 2x2, " +
                "half-heart damage, and SPEED_200 (`$50)." );
        }

        LoadValidationRoom(0, 0x74);
        if (_entities.Entities<OctorokCharacter>().Count != 2 ||
            _entities.Entities<OctorokCharacter>().Find(octorok => octorok.Record.SubId == 0) is not OctorokCharacter red ||
            _entities.Entities<OctorokCharacter>().Find(octorok => octorok.Record.SubId == 1) is not OctorokCharacter fastRed ||
            red.Record.SpeedRaw != 0x14 || fastRed.Record.SpeedRaw != 0x1e ||
            red.Record.Health != 2 || red.Record.DamageQuarters != 1)
        {
            throw new InvalidOperationException(
                "Room 0:74 did not load one random red and one fast-red Octorok with " +
                "SPEED_80/SPEED_c0, two health, and quarter-heart contact damage.");
        }

        int redCount = _entities.Entities<OctorokCharacter>().Count;
        if (!_entities.ApplySwordHit(red.CollisionBounds.Grow(1.0f), red.Position + Vector2.Down * 16.0f) ||
            _entities.Entities<OctorokCharacter>().Count != redCount - 1 ||
            _entities.Entities<EnemyDeathPuffEffect>().Count != 1 || _entities.Entities<EnemyDeathPuffEffect>()[0].EnemyId != 0x09)
        {
            throw new InvalidOperationException(
                "The level-1 sword did not defeat a two-health red Octorok in one hit and " +
                "create its ordinary ENEMY_OCTOROK `$09 death puff.");
        }

        LoadValidationRoom(1, 0xbc);
        if (_entities.Entities<OctorokCharacter>().Count != 2 ||
            _entities.Entities<OctorokCharacter>().Exists(octorok => octorok.Record.SubId != 2) ||
            !_entities.Entities<OctorokCharacter>().Exists(octorok => octorok.Position == new Vector2(0x48, 0x48)) ||
            !_entities.Entities<OctorokCharacter>().Exists(octorok => octorok.Position == new Vector2(0x58, 0x48)))
        {
            throw new InvalidOperationException(
                "Room 1:bc did not preserve its fixed blue Octoroks at `$48,`$48 and `$58,`$48.");
        }

        OctorokCharacter blue = _entities.Entities<OctorokCharacter>()[0];
        OctorokCharacter otherBlue = _entities.Entities<OctorokCharacter>()[1];
        if (blue.Record.Health != 3 || blue.Record.DamageQuarters != 2 ||
            blue.Record.CounterMask != 3)
        {
            throw new InvalidOperationException(
                "Blue Octorok subid `$02 did not retain three health, half-heart contact damage, " +
                "and decision mask `$03.");
        }

        _player.WarpTo(new Vector2(144, 120), recordSafe: false);
        otherBlue.SetStateForValidation(OctorokCharacter.OctorokState.Standing, counter1: 1000);
        blue.SetStateForValidation(
            OctorokCharacter.OctorokState.Shooting, counter1: 0x10, angle: 0x18);
        for (int frame = 1; frame < 0x10; frame++)
            _entities.Update(1.0 / 60.0, _player);
        if (_entities.Entities<OctorokRockProjectile>().Count != 0 || blue.Counter1 != 1)
        {
            throw new InvalidOperationException(
                "ENEMY_OCTOROK fired before completing its original `$10-update windup.");
        }
        Vector2 projectileOrigin = blue.Position;
        _entities.Update(1.0 / 60.0, _player);
        if (_entities.Entities<OctorokRockProjectile>().Count != 1 ||
            _entities.Entities<OctorokRockProjectile>()[0].State != OctorokRockProjectile.RockState.Flying ||
            _entities.Entities<OctorokRockProjectile>()[0].ElapsedFrames != 1 ||
            _entities.Entities<OctorokRockProjectile>()[0].Position != projectileOrigin ||
            blue.State != OctorokCharacter.OctorokState.Standing || blue.Counter1 != 0x20)
        {
            throw new InvalidOperationException(
                "ENEMY_OCTOROK did not spawn PART_OCTOROK_PROJECTILE after 16 updates and " +
                "enter its 32-update post-shot stand.");
        }

        OctorokRockProjectile rock = _entities.Entities<OctorokRockProjectile>()[0];
        _entities.Update(1.0 / 60.0, _player);
        if (rock.Position != projectileOrigin + Vector2.Left * 2.0f)
            throw new InvalidOperationException("The Octorok rock did not move at SPEED_200 (2 pixels/update).");
        for (int frame = 0; frame < 4; frame++)
            _entities.Update(1.0 / 60.0, _player);
        if (!_entities.ApplySwordHit(rock.CollisionBounds.Grow(1.0f), _player.Position) ||
            rock.State != OctorokRockProjectile.RockState.Bouncing ||
            rock.Angle != 0x08 || rock.Counter != 0x20)
        {
            throw new InvalidOperationException(
                "The level-1 sword did not reverse an Octorok rock and start its `$20-update bounce.");
        }
        for (int frame = 1; frame < 0x20; frame++)
            _entities.Update(1.0 / 60.0, _player);
        if (rock.Finished || _entities.Entities<OctorokRockProjectile>().Count != 1 || rock.Counter != 1)
            throw new InvalidOperationException("The deflected Octorok rock ended before bounce update `$20.");
        _entities.Update(1.0 / 60.0, _player);
        if (_entities.Entities<OctorokRockProjectile>().Count != 0)
            throw new InvalidOperationException("The deflected Octorok rock survived bounce update `$20.");

        Vector2 terrainCollisionOrigin = Vector2.Zero;
        bool foundTerrainCollision = false;
        for (int y = 8; y < _currentRoom.Height - 8 && !foundTerrainCollision; y++)
        {
            for (int x = 8; x < _currentRoom.Width - 2; x++)
            {
                var origin = new Vector2(x, y);
                if (!_currentRoom.IsSolid(origin) &&
                    _currentRoom.IsSolid(origin + Vector2.Right * 2.0f) &&
                    origin.DistanceTo(_player.Position) > 16.0f)
                {
                    terrainCollisionOrigin = origin;
                    foundTerrainCollision = true;
                    break;
                }
            }
        }
        if (!foundTerrainCollision)
            throw new InvalidOperationException("Room 1:bc has no usable Octorok-rock collision edge.");
        var terrainRock = new OctorokRockProjectile();
        terrainRock.Initialize(projectile, _currentRoom, terrainCollisionOrigin, angle: 0x08);
        terrainRock.UpdateFrame(_player);
        terrainRock.UpdateFrame(_player);
        if (terrainRock.State != OctorokRockProjectile.RockState.CollisionPending ||
            terrainRock.Position != terrainCollisionOrigin + Vector2.Right * 2.0f)
        {
            throw new InvalidOperationException(
                "A terrain-striking Octorok rock did not enter state 2 after applying its final flying step.");
        }
        terrainRock.UpdateFrame(_player);
        if (terrainRock.State != OctorokRockProjectile.RockState.Bouncing ||
            terrainRock.Counter != 0x20 || terrainRock.Angle != 0x18)
        {
            throw new InvalidOperationException(
                "Octorok-rock terrain collision state 2 did not initialize the reversed bounce on the next update.");
        }
        terrainRock.Free();

        blue = _entities.Entities<OctorokCharacter>()[0];
        otherBlue.SetStateForValidation(OctorokCharacter.OctorokState.Standing, counter1: 1000);
        int blueCount = _entities.Entities<OctorokCharacter>().Count;
        if (!_entities.ApplySwordHit(
                blue.CollisionBounds.Grow(1.0f), blue.Position + Vector2.Left * 16.0f) ||
            blue.Health != 1 || blue.InvincibilityCounter != 0x10 ||
            blue.KnockbackCounter != 0x08 || _entities.Entities<OctorokCharacter>().Count != blueCount)
        {
            throw new InvalidOperationException(
                "A blue Octorok did not survive its first level-1 sword hit with one health, " +
                "16 invincibility updates, and 8 SPEED_200 knockback updates. Got " +
                $"health {blue.Health}, invincibility {blue.InvincibilityCounter}, " +
                $"knockback {blue.KnockbackCounter}, count {_entities.Entities<OctorokCharacter>().Count}/{blueCount}.");
        }
        if (_entities.ApplySwordHit(blue.CollisionBounds.Grow(1.0f), _player.Position))
            throw new InvalidOperationException("Blue Octorok invincibility accepted a second immediate sword hit.");
        for (int frame = 0; frame < 0x10; frame++)
            blue.UpdateFrame(_player.Position);
        if (blue.InvincibilityCounter != 0 || blue.KnockbackCounter != 0 ||
            !_entities.ApplySwordHit(blue.CollisionBounds.Grow(1.0f), _player.Position) ||
            _entities.Entities<OctorokCharacter>().Count != blueCount - 1 ||
            _entities.Entities<EnemyDeathPuffEffect>().Count != 1 || _entities.Entities<EnemyDeathPuffEffect>()[0].EnemyId != 0x09)
        {
            throw new InvalidOperationException(
                "A blue Octorok did not become vulnerable after 16 updates and die on the second sword hit.");
        }

        OctorokCharacter transitionOctorok = _entities.Entities<OctorokCharacter>()[0];
        transitionOctorok.SetStateForValidation(
            OctorokCharacter.OctorokState.Standing, counter1: 1000, angle: 0x00);
        OctorokRockProjectile transitionRock = _entities.Spawn<OctorokRockProjectile>(
            new OctorokRockSpawn(transitionOctorok.Position, Angle: 0x00));
        OracleRoomData incomingRoom = _world.LoadRoom(0, 0x74);
        _entities.BeginScreenTransition(0, incomingRoom, Vector2.Left * incomingRoom.Width);
        int frozenCounter = transitionOctorok.Counter1;
        int frozenRockFrames = transitionRock.ElapsedFrames;
        _entities.Update(1.0, _player);
        if (_entities.OutgoingEntities<OctorokCharacter>().Count != 1 || _entities.OutgoingEntities<OctorokRockProjectile>().Count != 1 ||
            _entities.Entities<OctorokCharacter>().Count != 2 || transitionOctorok.Counter1 != frozenCounter ||
            transitionRock.ElapsedFrames != frozenRockFrames ||
            !_entities.Entities<OctorokCharacter>()[0].TransitionDrawOffset.IsEqualApprox(Vector2.Left * incomingRoom.Width))
        {
            throw new InvalidOperationException(
                "Scrolling did not retain/freeze outgoing Octoroks and rocks while preloading destination Octoroks.");
        }
        _entities.FinishScreenTransition();
        if (_entities.OutgoingEntities<OctorokCharacter>().Count != 0 || _entities.OutgoingEntities<OctorokRockProjectile>().Count != 0 ||
            !_entities.Entities<OctorokCharacter>()[0].TransitionDrawOffset.IsEqualApprox(Vector2.Zero))
        {
            throw new InvalidOperationException(
                "Octorok/rock transition ownership and offsets were not normalized after scrolling.");
        }

        var drops = new ItemDropDatabase();
        int octorokProbabilityRolls = 0;
        for (int roll = 0; roll < 64; roll++)
        {
            if (drops.ProbabilityAllows(4, roll))
                octorokProbabilityRolls++;
        }
        if (drops.EnemyTableRecord(0x09) != 0x8e || octorokProbabilityRolls != 24 ||
            drops.ChooseDrop(0x09, 0, 0) != ItemDropDatabase.Heart)
        {
            throw new InvalidOperationException(
                "ENEMY_OCTOROK `$09 did not preserve drop record `$8e, its 24-of-64 " +
                "probability `$04, and supported heart/rupee set `$0e.");
        }

        _player.RefillHealth();
        GD.Print("Validated 33 imported ENEMY_OCTOROK room records / 48 instances, random and fixed " +
            "red/fast-red/blue subids, movement/combat attributes, 16-update firing, SPEED_200 rocks, " +
            "sword deflection, 32-update bounce, two-hit blue combat, scrolling, and `$8e item drops.");
    }

    private void ValidateZolsAndGels()
    {
        var database = new EnemyDatabase();
        if (database.ZolRecordCount != 61 || database.ZolInstanceCount != 79 ||
            database.GelRecordCount != 1 || database.GelInstanceCount != 3)
        {
            throw new InvalidOperationException(
                $"Expected 61 ENEMY_ZOL records / 79 instances and one ENEMY_GEL record / " +
                $"3 instances, got {database.ZolRecordCount} / {database.ZolInstanceCount} " +
                $"and {database.GelRecordCount} / {database.GelInstanceCount}.");
        }
        if (database.Gel.Id != 0x43 || database.Gel.TileBase != 0 ||
            database.Gel.Palette != 2 || database.Gel.CollisionRadiusY != 2 ||
            database.Gel.CollisionRadiusX != 2 || database.Gel.DamageQuarters != 2 ||
            database.Gel.Health != 1)
        {
            throw new InvalidOperationException(
                "ENEMY_GEL did not retain id `$43, tile base 0, palette 2, radius 2x2, " +
                "half-heart damage, and one health.");
        }

        LoadValidationRoom(4, 0xcc);
        if (_entities.Entities<ZolCharacter>().Count != 6 ||
            _entities.Entities<ZolCharacter>().FindAll(zol => zol.Record.SubId == 0).Count != 3 ||
            _entities.Entities<ZolCharacter>().FindAll(zol => zol.Record.SubId == 1).Count != 3 ||
            !_entities.Entities<ZolCharacter>().Exists(zol => zol.Position == new Vector2(0x58, 0x78)) ||
            !_entities.Entities<ZolCharacter>().Exists(zol => zol.Position == new Vector2(0x48, 0x98)))
        {
            throw new InvalidOperationException(
                "Room 4:cc did not preserve its three fixed green and three fixed red Zols.");
        }

        ZolCharacter transitionZol = _entities.Entities<ZolCharacter>()[0];
        int frozenCounter = transitionZol.Counter1;
        OracleRoomData incomingRoom = _world.LoadRoom(4, 0x0b);
        _entities.BeginScreenTransition(4, incomingRoom, Vector2.Left * incomingRoom.Width);
        _entities.Update(1.0, _player);
        if (_entities.OutgoingEntities<ZolCharacter>().Count != 6 || _entities.Entities<ZolCharacter>().Count != 0 ||
            _entities.Entities<GelCharacter>().Count != 3 || transitionZol.Counter1 != frozenCounter ||
            !_entities.Entities<GelCharacter>()[0].TransitionDrawOffset.IsEqualApprox(Vector2.Left * incomingRoom.Width))
        {
            throw new InvalidOperationException(
                "Scrolling did not retain/freeze six outgoing Zols and preload/freeze " +
                "the three direct room 4:0b Gels.");
        }
        _entities.FinishScreenTransition();
        if (_entities.OutgoingEntities<ZolCharacter>().Count != 0 ||
            !_entities.Entities<GelCharacter>()[0].TransitionDrawOffset.IsEqualApprox(Vector2.Zero))
        {
            throw new InvalidOperationException(
                "Zol/Gel transition ownership and offsets were not normalized after scrolling.");
        }

        EnemyDatabase.ZolRecord greenRecord = default;
        foreach (EnemyDatabase.ZolRecord record in database.GetRoomZols(4, 0xcc))
        {
            if (record.SubId == 0)
            {
                greenRecord = record;
                break;
            }
        }
        if (greenRecord.Id != 0x34 || greenRecord.Health != 2 ||
            greenRecord.DamageQuarters != 2 || greenRecord.Palette != 0)
        {
            throw new InvalidOperationException(
                "Green ENEMY_ZOL `$34/`$00 attributes were not imported correctly.");
        }

        var timingZol = new ZolCharacter();
        var timingRandom = new OracleRandom();
        timingZol.Initialize(greenRecord, _world.LoadRoom(4, 0xcc), new Vector2(80, 80), timingRandom);
        timingZol.UpdateFrame(new Vector2(120, 80));
        if (timingZol.State != ZolCharacter.ZolState.GreenHidden)
            throw new InvalidOperationException("Green Zol woke at the excluded Manhattan distance `$28.");
        timingZol.UpdateFrame(new Vector2(119, 80));
        if (timingZol.State != ZolCharacter.ZolState.GreenEmerging ||
            timingZol.Counter2 != 4 || !timingZol.Visible)
        {
            throw new InvalidOperationException(
                "Green Zol did not wake inside Manhattan distance `$28 with four hops queued.");
        }
        for (int frame = 0; frame < 32; frame++)
            timingZol.UpdateFrame(new Vector2(119, 80));
        if (timingZol.State != ZolCharacter.ZolState.GreenEmerging ||
            timingZol.AnimationParameter != 1 || timingZol.ZFixed != 0)
        {
            throw new InvalidOperationException(
                "Green Zol emergence did not reach its terminal animation parameter after 32 updates.");
        }
        for (int frame = 0; frame < 26; frame++)
            timingZol.UpdateFrame(new Vector2(119, 80));
        if (timingZol.ZFixed >= 0 || timingZol.State != ZolCharacter.ZolState.GreenEmerging)
            throw new InvalidOperationException("Green Zol landed before its 27th gravity update.");
        timingZol.UpdateFrame(new Vector2(119, 80));
        if (timingZol.State != ZolCharacter.ZolState.GreenWaiting ||
            timingZol.Counter1 != 0x30 || !timingZol.CollisionEnabled)
        {
            throw new InvalidOperationException(
                "Green Zol did not land on gravity update 27 and begin its 48-update wait.");
        }
        for (int frame = 0; frame < 47; frame++)
            timingZol.UpdateFrame(new Vector2(119, 80));
        if (timingZol.Counter1 != 1 || timingZol.State != ZolCharacter.ZolState.GreenWaiting)
            throw new InvalidOperationException("Green Zol ended its 48-update wait early.");
        timingZol.UpdateFrame(new Vector2(119, 80));
        if (timingZol.State != ZolCharacter.ZolState.GreenHopping)
            throw new InvalidOperationException("Green Zol did not begin its first pursuit hop.");
        for (int frame = 0; frame < 27; frame++)
            timingZol.UpdateFrame(new Vector2(119, 80));
        if (timingZol.Counter2 != 3 || timingZol.State != ZolCharacter.ZolState.GreenWaiting)
            throw new InvalidOperationException("Green Zol did not consume exactly one of four hops.");
        for (int hop = 0; hop < 3; hop++)
        {
            for (int frame = 0; frame < 48; frame++)
                timingZol.UpdateFrame(new Vector2(119, 80));
            for (int frame = 0; frame < 27; frame++)
                timingZol.UpdateFrame(new Vector2(119, 80));
        }
        if (timingZol.State != ZolCharacter.ZolState.GreenDisappearing ||
            timingZol.Counter2 != 0 || timingZol.CollisionEnabled)
        {
            throw new InvalidOperationException(
                "Green Zol did not disable collision and disappear after exactly four hops.");
        }
        for (int frame = 0; frame < 40; frame++)
            timingZol.UpdateFrame(new Vector2(119, 80));
        if (timingZol.AnimationParameter != 1 ||
            timingZol.State != ZolCharacter.ZolState.GreenDisappearing)
        {
            throw new InvalidOperationException(
                "Green Zol disappearance did not reach its terminal parameter after 40 updates.");
        }
        timingZol.UpdateFrame(new Vector2(119, 80));
        for (int frame = 0; frame < 39; frame++)
            timingZol.UpdateFrame(new Vector2(119, 80));
        if (timingZol.State != ZolCharacter.ZolState.GreenGone || timingZol.Counter1 != 1)
            throw new InvalidOperationException("Green Zol ended its 40-update underground wait early.");
        timingZol.UpdateFrame(new Vector2(119, 80));
        if (timingZol.State != ZolCharacter.ZolState.GreenHidden)
            throw new InvalidOperationException("Green Zol did not return to its hidden proximity state.");
        timingZol.Free();

        LoadValidationRoom(4, 0xcc);
        ZolCharacter green = _entities.Entities<ZolCharacter>().Find(zol => zol.Record.SubId == 0)!;
        green.SetStateForValidation(
            ZolCharacter.ZolState.GreenWaiting,
            counter1: 1000,
            animation: 1);
        int greenCount = _entities.Entities<ZolCharacter>().Count;
        if (!_entities.ApplySwordHit(green.CollisionBounds.Grow(1.0f)) ||
            _entities.Entities<ZolCharacter>().Count != greenCount - 1 || _entities.Entities<EnemyDeathPuffEffect>().Count != 1 ||
            _entities.Entities<EnemyDeathPuffEffect>()[0].EnemyId != 0x34)
        {
            throw new InvalidOperationException(
                "The level-1 sword did not defeat a surfaced green Zol and create its normal `$34 death puff.");
        }

        LoadValidationRoom(4, 0xcc);
        _player.WarpTo(new Vector2(220, 160), recordSafe: false);
        ZolCharacter red = _entities.Entities<ZolCharacter>().Find(zol => zol.Record.SubId == 1)!;
        Vector2 splitPosition = red.Position;
        int redRoomCount = _entities.Entities<ZolCharacter>().Count;
        if (!_entities.ApplySwordHit(red.CollisionBounds.Grow(1.0f)) ||
            red.State != ZolCharacter.ZolState.RedSplitting ||
            _entities.Entities<ZolCharacter>().Count != redRoomCount || _entities.Entities<EnemyDeathPuffEffect>().Count != 0)
        {
            throw new InvalidOperationException(
                "A sword-hit red Zol did not enter its special split state without a normal death puff.");
        }
        _entities.Update(1.0 / 60.0, _player);
        if (red.State != ZolCharacter.ZolState.RedSplitDelay || red.Counter2 != 18 ||
            red.Visible || red.CollisionEnabled || _entities.Entities<KillEnemyPuffEffect>().Count != 1 ||
            _entities.Entities<KillEnemyPuffEffect>()[0].DurationFrames != 20)
        {
            throw new InvalidOperationException(
                "Red Zol did not create the 20-update INTERAC_KILLENEMYPUFF and begin its 18-update delay.");
        }
        for (int frame = 0; frame < 17; frame++)
            _entities.Update(1.0 / 60.0, _player);
        if (_entities.Entities<GelCharacter>().Count != 0 || red.Counter2 != 1)
            throw new InvalidOperationException("Red Zol spawned Gels before split delay update 18.");
        _entities.Update(1.0 / 60.0, _player);
        if (_entities.Entities<ZolCharacter>().Count != redRoomCount - 1 || _entities.Entities<GelCharacter>().Count != 2 ||
            !_entities.Entities<GelCharacter>().Exists(gel => gel.Position == splitPosition + Vector2.Right * 4.0f) ||
            !_entities.Entities<GelCharacter>().Exists(gel => gel.Position == splitPosition + Vector2.Left * 4.0f))
        {
            throw new InvalidOperationException(
                "Red Zol did not replace itself with two Gels at the original +/-4 X offsets.");
        }
        _entities.Update(2.0 / 60.0, _player);
        if (_entities.Entities<KillEnemyPuffEffect>().Count != 0 || _entities.Entities<ItemDropEffect>().Count != 0)
            throw new InvalidOperationException(
                "INTERAC_KILLENEMYPUFF did not end after 20 updates or incorrectly resolved an item drop.");

        GelCharacter defeatedGel = _entities.Entities<GelCharacter>()[0];
        int gelCount = _entities.Entities<GelCharacter>().Count;
        if (!_entities.ApplySwordHit(defeatedGel.CollisionBounds.Grow(1.0f)) ||
            _entities.Entities<GelCharacter>().Count != gelCount - 1 || _entities.Entities<EnemyDeathPuffEffect>().Count != 1 ||
            _entities.Entities<EnemyDeathPuffEffect>()[0].EnemyId != 0x43)
        {
            throw new InvalidOperationException(
                "The one-health ENEMY_GEL did not die to one level-1 sword hit with a `$43 death puff.");
        }

        GelCharacter latchGel = _entities.Entities<GelCharacter>()[0];
        Vector2 latchPosition = new(180, 140);
        latchGel.Position = latchPosition;
        _player.WarpTo(latchPosition, recordSafe: false);
        _player.RefillHealth();
        int healthBeforeLatch = _player.HealthQuarters;
        _entities.Update(0.0, _player);
        if (!latchGel.IsAttached || latchGel.Counter2 != 120 ||
            _player.HealthQuarters != healthBeforeLatch ||
            !_entities.PlayerSwordDisabled)
        {
            throw new InvalidOperationException(
                "Gel contact damaged Link or failed to latch for 120 updates and disable the sword.");
        }
        bool movementPhase = _entities.PlayerMovementDisabled;
        _entities.Update(1.0 / 60.0, _player);
        if (_entities.PlayerMovementDisabled == movementPhase)
            throw new InvalidOperationException("Attached Gel did not immobilize Link on alternating updates.");
        for (int frame = 0; frame < 118; frame++)
            _entities.Update(1.0 / 60.0, _player);
        if (!latchGel.IsAttached || latchGel.Counter2 != 1)
            throw new InvalidOperationException(
                "Gel did not remain attached through latch update 119: " +
                $"state={latchGel.State}, counter2={latchGel.Counter2}, " +
                $"entityFrame={_entities.FrameCounter}.");
        _entities.Update(1.0 / 60.0, _player);
        if (latchGel.IsAttached || latchGel.State != GelCharacter.GelState.Hopping ||
            latchGel.Angle != 0x00 || latchGel.CollisionEnabled ||
            _entities.PlayerSwordDisabled)
        {
            throw new InvalidOperationException(
                "Gel did not automatically release after 120 updates with hop collision disabled.");
        }
        _entities.Update(1.0 / 60.0, _player);
        if (latchGel.IsAttached || _entities.PlayerSwordDisabled)
            throw new InvalidOperationException(
                "A naturally released Gel immediately relatched before completing its hop.");

        Vector2 buttonLatchPosition = new(120, 140);
        GelCharacter buttonGel = _entities.Spawn<GelCharacter>(
            new GelSpawn(buttonLatchPosition, "ButtonReleaseGel"));
        _player.WarpTo(buttonLatchPosition, recordSafe: false);
        _entities.Update(0.0, _player);
        if (!buttonGel.IsAttached || buttonGel.Counter2 != 120)
            throw new InvalidOperationException("Button-release test Gel did not latch.");
        for (int press = 0; press < 30; press++)
            buttonGel.UpdateFrame(_player.Position, Vector2I.Down, anyButtonJustPressed: true);
        if (!buttonGel.IsAttached || buttonGel.Counter2 != 1)
            throw new InvalidOperationException(
                "Thirty button presses did not reduce the Gel latch counter to one.");
        buttonGel.UpdateFrame(_player.Position, Vector2I.Down, anyButtonJustPressed: false);
        if (buttonGel.IsAttached || buttonGel.State != GelCharacter.GelState.Hopping ||
            buttonGel.Angle != 0x00 || buttonGel.CollisionEnabled ||
            _entities.PlayerSwordDisabled)
        {
            throw new InvalidOperationException(
                "Button presses did not release the Gel with collision disabled and hop it away from Link.");
        }
        _entities.Update(1.0 / 60.0, _player);
        if (buttonGel.IsAttached || _entities.PlayerSwordDisabled)
            throw new InvalidOperationException(
                "A button-released Gel immediately relatched before completing its hop.");

        _player.RefillHealth();
        GD.Print("Validated 61 ENEMY_ZOL records / 79 instances, room 4:cc fixed placements, " +
            "strict `$28 emergence, 32/27/48-update green timing, four-hop disappearance, " +
            "red 18-update splitting with INTERAC_KILLENEMYPUFF, +/-4 Gel spawning, direct " +
            "room Gels, non-damaging 120-update latch/button release, collision-safe hop-off, " +
            "alternating movement suppression, " +
            "sword disabling, combat, drops, and retained/preloaded scrolling.");
    }

    private void ValidateItemDrops()
    {
        var database = new ItemDropDatabase();
        if (ItemDropDatabase.SelectionDataSize != 720 ||
            database.EnemyTableRecord(0x32) != 0xae)
        {
            throw new InvalidOperationException(
                "ENEMY_KEESE `$32 did not retain item-drop record `$ae in the 720-byte selection data.");
        }

        int allowedProbabilityRolls = 0;
        int allowedRoll = -1;
        int deniedRoll = -1;
        for (int roll = 0; roll < 64; roll++)
        {
            if (database.ProbabilityAllows(5, roll))
            {
                allowedProbabilityRolls++;
                if (allowedRoll < 0)
                    allowedRoll = roll;
            }
            else if (deniedRoll < 0)
            {
                deniedRoll = roll;
            }
        }
        if (allowedProbabilityRolls != 32 || allowedRoll < 0 || deniedRoll < 0 ||
            database.ChooseDrop(0x32, (byte)deniedRoll, 0).HasValue ||
            database.ChooseDrop(0x32, (byte)allowedRoll, 0) != ItemDropDatabase.Heart)
        {
            throw new InvalidOperationException(
                "Keese probability set 5 did not preserve its original 32-of-64 drop chance.");
        }

        int hearts = 0;
        int oneRupees = 0;
        int fiveRupees = 0;
        for (int roll = 0; roll < ItemDropDatabase.SetSize; roll++)
        {
            switch (database.DropSetValue(0x0e, roll))
            {
                case ItemDropDatabase.Heart: hearts++; break;
                case ItemDropDatabase.OneRupee: oneRupees++; break;
                case ItemDropDatabase.FiveRupees: fiveRupees++; break;
            }
        }
        if (hearts != 16 || oneRupees != 12 || fiveRupees != 4)
        {
            throw new InvalidOperationException(
                $"Keese drop set `$0e should contain 16 hearts, 12 single rupees, and 4 five-rupee drops; " +
                $"got {hearts}, {oneRupees}, and {fiveRupees}.");
        }

        ItemDropDatabase.VisualRecord heartVisual = database.GetVisual(ItemDropDatabase.Heart);
        ItemDropDatabase.VisualRecord oneRupeeVisual = database.GetVisual(ItemDropDatabase.OneRupee);
        ItemDropDatabase.VisualRecord fiveRupeeVisual = database.GetVisual(ItemDropDatabase.FiveRupees);
        if (heartVisual.TileBase != 2 || heartVisual.Palette != 5 ||
            oneRupeeVisual.TileBase != 4 || oneRupeeVisual.Palette != 0 ||
            fiveRupeeVisual.TileBase != 6 || fiveRupeeVisual.Palette != 5)
        {
            throw new InvalidOperationException(
                "Heart/rupee PART_ITEM_DROP visuals do not match spriteData tile bases `$02/`$04/`$06.");
        }

        var lifecycleRoot = new Node { Name = "ItemDropLifecycleValidation" };
        AddChild(lifecycleRoot);
        var lifecycleManager = new RoomEntityManager(
            lifecycleRoot,
            new NpcDatabase(),
            new EnemyDatabase(),
            database,
            new TimePortalDatabase(),
            new OracleRandom());
        lifecycleManager.LoadRoom(0, _world.LoadRoom(0, 0x00));
        _player.WarpTo(new Vector2(140, 120), recordSafe: false);
        for (int defeated = 0; defeated < 5; defeated++)
        {
            lifecycleManager.Spawn<EnemyDeathPuffEffect>(
                new EnemyDeathPuffSpawn(new Vector2(24, 24), EnemyId: 0x32));
            for (int frame = 0; frame < 20; frame++)
                lifecycleManager.Update(1.0 / 60.0, _player);
            if (lifecycleManager.Entities<EnemyDeathPuffEffect>().Count != 0)
            {
                throw new InvalidOperationException(
                    "A finished Keese death puff was not replaced/deleted by item-drop resolution.");
            }
        }
        if (lifecycleManager.Entities<ItemDropEffect>().Count != 1 ||
            lifecycleManager.Entities<ItemDropEffect>()[0].SubId != ItemDropDatabase.FiveRupees ||
            lifecycleManager.Entities<ItemDropEffect>()[0].ElapsedFrames != 0)
        {
            throw new InvalidOperationException(
                "The deterministic fifth Keese death did not replace its completed puff with ITEM_DROP_5_RUPEES.");
        }
        lifecycleManager.Clear();
        RemoveChild(lifecycleRoot);
        lifecycleRoot.Free();

        LoadValidationRoom(4, 0xcb);
        Vector2 dropPosition = FindOpenItemDropPosition(_currentRoom);
        Vector2 farPosition = dropPosition + Vector2.Right * 40.0f;
        _player.WarpTo(farPosition, recordSafe: false);

        var bounceDrop = new ItemDropEffect();
        bounceDrop.Initialize(
            ItemDropDatabase.Heart, dropPosition, _currentRoom, heartVisual);
        bounceDrop.UpdateFrame(_player, 1);
        if (bounceDrop.State != ItemDropEffect.DropState.Bouncing ||
            bounceDrop.ZFixed != 0 || bounceDrop.SpeedZ != -0x160)
        {
            throw new InvalidOperationException(
                "PART_ITEM_DROP did not spend its first update initializing speedZ to -`$160.");
        }
        for (int frame = 2; frame < 36; frame++)
        {
            bounceDrop.UpdateFrame(_player, frame);
            if (bounceDrop.State == ItemDropEffect.DropState.Grounded)
                throw new InvalidOperationException("PART_ITEM_DROP finished bouncing before update 36.");
        }
        bounceDrop.UpdateFrame(_player, 36);
        if (bounceDrop.State != ItemDropEffect.DropState.Grounded ||
            bounceDrop.ZFixed != 0 || bounceDrop.SpeedZ != 0 ||
            bounceDrop.Counter != 240 || !bounceDrop.CollisionEnabled)
        {
            throw new InvalidOperationException(
                "PART_ITEM_DROP did not complete its original fixed-point bounce and start counter `$f0 on update 36.");
        }
        bounceDrop.Free();

        _player.RefillHealth();
        _player.ApplyDamage(4);
        var heartDrop = new ItemDropEffect();
        heartDrop.Initialize(
            ItemDropDatabase.Heart, dropPosition, _currentRoom, heartVisual);
        for (int frame = 1; frame <= 36; frame++)
            heartDrop.UpdateFrame(_player, frame);
        _player.WarpTo(dropPosition, recordSafe: false);
        heartDrop.UpdateFrame(_player, 37);
        if (!heartDrop.Collected || !heartDrop.Finished ||
            _player.HealthQuarters != _player.MaxHealthQuarters)
        {
            throw new InvalidOperationException(
                "Collecting ITEM_DROP_HEART did not restore four quarter-heart units and delete the drop.");
        }
        heartDrop.Free();

        ValidateRupeeItemDrop(oneRupeeVisual, ItemDropDatabase.OneRupee, 1, dropPosition);
        ValidateRupeeItemDrop(fiveRupeeVisual, ItemDropDatabase.FiveRupees, 5, dropPosition);

        _player.WarpTo(farPosition, recordSafe: false);
        var expiryDrop = new ItemDropEffect();
        expiryDrop.Initialize(
            ItemDropDatabase.OneRupee, dropPosition, _currentRoom, oneRupeeVisual);
        for (int frame = 1; frame <= 395; frame++)
            expiryDrop.UpdateFrame(_player, frame);
        if (expiryDrop.Counter != 60 || !expiryDrop.Visible || expiryDrop.Finished)
        {
            throw new InvalidOperationException(
                "PART_ITEM_DROP did not retain visibility through countdown value 60.");
        }
        expiryDrop.UpdateFrame(_player, 396);
        expiryDrop.UpdateFrame(_player, 397);
        if (expiryDrop.Counter != 59 || expiryDrop.Visible)
            throw new InvalidOperationException("PART_ITEM_DROP did not begin flickering below counter 60.");
        expiryDrop.UpdateFrame(_player, 398);
        expiryDrop.UpdateFrame(_player, 399);
        if (!expiryDrop.Visible)
            throw new InvalidOperationException("PART_ITEM_DROP did not alternate visibility while flickering.");
        for (int frame = 400; frame <= 514; frame++)
            expiryDrop.UpdateFrame(_player, frame);
        if (expiryDrop.Finished || expiryDrop.Counter != 1)
            throw new InvalidOperationException("PART_ITEM_DROP expired before its 240th countdown tick.");
        expiryDrop.UpdateFrame(_player, 515);
        if (!expiryDrop.Finished || expiryDrop.Collected)
            throw new InvalidOperationException(
                "PART_ITEM_DROP did not expire after 240 alternating-frame ticks (480 update span).");
        expiryDrop.Free();

        ItemDropEffect transitionDrop = _entities.Spawn<ItemDropEffect>(
            new ItemDropSpawn(ItemDropDatabase.OneRupee, dropPosition));
        OracleRoomData incomingRoom = _world.LoadRoom(4, 0x39);
        _entities.BeginScreenTransition(4, incomingRoom, Vector2.Left * incomingRoom.Width);
        _entities.Update(1.0, _player);
        if (_entities.OutgoingEntities<ItemDropEffect>().Count != 1 ||
            _entities.OutgoingEntities<ItemDropEffect>()[0] != transitionDrop ||
            transitionDrop.ElapsedFrames != 0)
        {
            throw new InvalidOperationException(
                "Scrolling did not retain and freeze the outgoing PART_ITEM_DROP object.");
        }
        _entities.SetScreenTransitionOffsets(
            Vector2.Right * 4.0f,
            Vector2.Left * (incomingRoom.Width - 4.0f));
        if (!transitionDrop.TransitionDrawOffset.IsEqualApprox(Vector2.Right * 4.0f))
            throw new InvalidOperationException("The item drop did not move with its outgoing room.");
        _entities.FinishScreenTransition();
        if (_entities.OutgoingEntities<ItemDropEffect>().Count != 0)
            throw new InvalidOperationException("The outgoing item drop survived completed scrolling.");

        _player.RefillHealth();
        GD.Print("Validated all 144 enemy drop records, Keese `$ae probability/set data, " +
            "PART_ITEM_DROP heart/rupee visuals, -`$160 fixed-point bounce, pickup rewards, " +
            "240 alternating-frame lifetime ticks, final flicker, and frozen scrolling ownership.");
    }

    private void ValidateRupeeItemDrop(
        ItemDropDatabase.VisualRecord visual,
        int subId,
        int amount,
        Vector2 position)
    {
        int rupeesBefore = _player.Rupees;
        _player.WarpTo(position + Vector2.Right * 40.0f, recordSafe: false);
        var drop = new ItemDropEffect();
        drop.Initialize(subId, position, _currentRoom, visual);
        for (int frame = 1; frame <= 36; frame++)
            drop.UpdateFrame(_player, frame);
        _player.WarpTo(position, recordSafe: false);
        drop.UpdateFrame(_player, 37);
        if (!drop.Collected || _player.Rupees != Mathf.Min(999, rupeesBefore + amount) ||
            _hud.Rupees != _player.Rupees)
        {
            throw new InvalidOperationException(
                $"Collecting PART_ITEM_DROP ${subId:x2} did not add {amount} rupee(s) to Link and the HUD.");
        }
        drop.Free();
    }

    private static Vector2 FindOpenItemDropPosition(OracleRoomData room)
    {
        for (int y = 1; y < room.HeightInTiles - 1; y++)
        for (int x = 1; x < room.WidthInTiles - 1; x++)
        {
            Vector2 point = new(
                x * OracleRoomData.MetatileSize + 8,
                y * OracleRoomData.MetatileSize + 8);
            if (!room.IsSolid(point) &&
                room.GetTerrainInfo(point).Hazard == OracleRoomData.HazardType.None)
                return point;
        }
        throw new InvalidOperationException(
            $"Room {room.Group:x1}:{room.Id:x2} has no open item-drop validation position.");
    }

    private void ValidateTerrain()
    {
        _dialogue.Close();
        _player.RefillHealth();
        _activeGroup = 0;
        ClearDeactivatedWarp();

        _currentRoom = _world.LoadRoom(_activeGroup, 0xb8);
        _roomView.SetRoom(_currentRoom.Texture);
        Vector2 waterSafe = new(40, 8);
        _player.WarpTo(waterSafe);
        _player.WarpTo(new Vector2(8, 8), recordSafe: false);
        if (GetTerrainInfo(_player.Position).Hazard != OracleRoomData.HazardType.Water)
            throw new InvalidOperationException("Expected room b8/$00 to be water terrain.");
        ValidateDrowningSequence(waterSafe, OracleRoomData.HazardType.Water);

        _currentRoom = _world.LoadRoom(_activeGroup, 0x03);
        _roomView.SetRoom(_currentRoom.Texture);
        Vector2 lavaSafe = new(56, 8);
        _player.WarpTo(lavaSafe);
        _player.WarpTo(new Vector2(8, 24), recordSafe: false);
        if (GetTerrainInfo(_player.Position).Hazard != OracleRoomData.HazardType.Lava)
            throw new InvalidOperationException("Expected room 03/$10 to be lava terrain.");
        ValidateDrowningSequence(lavaSafe, OracleRoomData.HazardType.Lava);

        if (!TryFindTerrainSample(
            OracleRoomData.HazardType.Hole,
            out int holeGroup,
            out int holeRoom,
            out Vector2 holeCenter,
            out Vector2 holeSafe))
        {
            throw new InvalidOperationException("Could not find a testable hole terrain tile.");
        }

        _player.RefillHealth();
        _activeGroup = holeGroup;
        ClearDeactivatedWarp();
        _currentRoom = _world.LoadRoom(_activeGroup, holeRoom);
        _roomView.SetRoom(_currentRoom.Texture);
        _player.WarpTo(holeSafe);
        Vector2 offCenterHoleEntry = holeCenter + new Vector2(3, -2);
        int beforeHoleHealth = _player.HealthQuarters;
        _player.WarpTo(offCenterHoleEntry, recordSafe: false);
        Vector2 expectedHoleCenter = GetActiveTerrain(_player.Position).TileCenter;
        _player._PhysicsProcess(1.0 / 60.0);
        if (!_player.IsPullingIntoHole && !_player.IsFallingInHole)
            throw new InvalidOperationException(
                $"Room {holeGroup:x1}:{holeRoom:x2} hole terrain did not start Link's pull-in state.");
        if (_player.HealthQuarters != beforeHoleHealth)
            throw new InvalidOperationException("Hole damage was applied before the pull/fall animation finished.");

        AdvanceHolePullUntilFall(expectedHoleCenter);
        AdvanceHoleFallUntilRespawn(holeSafe);
        if (!_player.Visible || _player.Position.DistanceSquaredTo(holeSafe) > 1.0f)
            throw new InvalidOperationException("Hole terrain did not return Link to the last safe tile.");
        if (_player.HealthQuarters != beforeHoleHealth - 2)
            throw new InvalidOperationException("Hole terrain did not apply half-heart damage after respawn.");

        ValidateRoom01HoleBoundaryCase();

        _activeGroup = 0;
        _currentRoom = _world.LoadRoom(_activeGroup, 0x11);
        _roomView.SetRoom(_currentRoom.Texture);
        Vector2 ledgeStart = new(24, 56);
        _player.WarpTo(ledgeStart);
        _player.Face(Vector2I.Down);
        if (!TryStartLedgeHop(_player, _player.Position, Vector2.Down))
            throw new InvalidOperationException("Room 11's south cliff did not start a ledge hop.");
        for (int i = 0; i < 30; i++)
            _player._PhysicsProcess(1.0 / 60.0);
        if (_player.Position.Y <= ledgeStart.Y + OracleRoomData.MetatileSize)
            throw new InvalidOperationException("The ledge hop did not carry Link down across the cliff.");

        ValidateRoom56TileEdgeSlide();

        GD.Print("Validated terrain hazards, hole fall/respawn, ledge hop, and original tile-edge sliding.");
    }

    private void ValidateDrowningSequence(
        Vector2 safePosition,
        OracleRoomData.HazardType hazard)
    {
        string terrainName = hazard.ToString();
        Vector2 hazardPosition = _player.Position;
        int healthBeforeDrowning = _player.HealthQuarters;
        int worldChildCount = _scene.WorldRoot.GetChildCount();

        _player._PhysicsProcess(1.0 / 60.0);
        SplashEffect? splash = _terrain.ActiveSplash;
        if (_scene.WorldRoot.GetChildCount() != worldChildCount + 1 || splash is null ||
            splash.Position != hazardPosition ||
            splash.IsLava != (hazard == OracleRoomData.HazardType.Lava))
        {
            throw new InvalidOperationException(
                $"{terrainName} drowning did not create its original splash interaction at Link's position.");
        }
        int splashFrameDuration = splash.IsLava ? 2 : 4;
        int splashFrameCount = splash.IsLava ? 10 : 3;
        if (splash.DurationFrames != splashFrameDuration * splashFrameCount ||
            splash.AnimationFrame != 0)
        {
            throw new InvalidOperationException(
                $"{terrainName} splash did not start with the original interaction timing.");
        }
        splash.Advance((splashFrameDuration - 1.0) / 60.0);
        if (splash.AnimationFrame != 0)
            throw new InvalidOperationException(
                $"{terrainName} splash did not hold its first OAM record for {splashFrameDuration} updates.");
        splash.Advance(1.0 / 60.0);
        if (splash.AnimationFrame != 1)
            throw new InvalidOperationException(
                $"{terrainName} splash did not advance to its second OAM record.");
        splash.Advance((splash.DurationFrames - splashFrameDuration - 1.0) / 60.0);
        if (splash.AnimationFrame != splashFrameCount - 1 || splash.IsQueuedForDeletion())
            throw new InvalidOperationException(
                $"{terrainName} splash did not reach and hold its final OAM record.");
        splash.Advance(1.0 / 60.0);
        if (!splash.IsQueuedForDeletion())
            throw new InvalidOperationException(
                $"{terrainName} splash did not delete after {splash.DurationFrames} updates.");
        if (!_player.IsDrowning || !_player.Visible || _player.DrownAnimationFrame != 0)
            throw new InvalidOperationException(
                $"{terrainName} terrain did not begin visible LINK_ANIM_MODE_DROWN frame $d4.");
        if (_player.HealthQuarters != healthBeforeDrowning)
            throw new InvalidOperationException(
                $"{terrainName} damage was applied before the drowning animation finished.");

        _player._PhysicsProcess(5.0 / 60.0);
        if (!_player.Visible || _player.DrownAnimationFrame != 0)
            throw new InvalidOperationException(
                $"{terrainName} did not hold directional drowning frame $d4 for six updates.");
        _player._PhysicsProcess(1.0 / 60.0);
        if (!_player.Visible || _player.DrownAnimationFrame != 1)
            throw new InvalidOperationException(
                $"{terrainName} did not advance to drowning frame $0b after six updates.");

        _player._PhysicsProcess(15.0 / 60.0);
        if (!_player.Visible || _player.Position != hazardPosition)
            throw new InvalidOperationException(
                $"{terrainName} moved or hid Link before the 22-update drowning animation finished.");
        _player._PhysicsProcess(1.0 / 60.0);
        if (_player.Visible || !_player.IsDrowning)
            throw new InvalidOperationException(
                $"{terrainName} did not hide Link after the 22-update drowning animation.");
        if (_player.HealthQuarters != healthBeforeDrowning)
            throw new InvalidOperationException(
                $"{terrainName} damage was applied before the two-update respawn delay.");

        _player._PhysicsProcess(1.0 / 60.0);
        if (_player.Visible)
            throw new InvalidOperationException(
                $"{terrainName} did not preserve the first invisible respawn update.");
        _player._PhysicsProcess(1.0 / 60.0);
        if (_player.IsDrowning || !_player.Visible ||
            _player.Position.DistanceSquaredTo(safePosition) > 1.0f)
        {
            throw new InvalidOperationException(
                $"{terrainName} did not return Link to the last safe tile after drowning.");
        }
        if (_player.HealthQuarters != healthBeforeDrowning - 2)
            throw new InvalidOperationException(
                $"{terrainName} did not apply one half-heart after Link reappeared.");
    }

    private void ValidateRoom56TileEdgeSlide()
    {
        _activeGroup = 0;
        _currentRoom = _world.LoadRoom(_activeGroup, 0x56);
        _roomView.SetRoom(_currentRoom.Texture);
        RefreshRoomObjects();

        // The left end of bridge metatiles $1d/$1e starts at x=$30. These two
        // positions reproduce approaching its upper and lower rails from the
        // west, including the lower corner shown in the reported stuck case.
        ValidateBridgeSlidePath(new Vector2(44, 62), expectUp: true);
        ValidateBridgeSlidePath(new Vector2(44, 92), expectUp: false);

        // A fractional fixed-point remainder is possible when Link walks onto
        // the bridge after moving over slower terrain. At y=70.5 the upper
        // rail correctly blocks another upward step, while the original yh
        // coordinate (and therefore the rendered sprite) remains row 70.
        Vector2 fractionalStop = new(56, 70.5f);
        if (_collision.ResolveMovement(
                fractionalStop, Vector2.Up, allowWallSlide: true) != Vector2.Zero)
        {
            throw new InvalidOperationException(
                "Room 0:56's upper bridge rail did not block the fractional direct approach.");
        }

        _player.WarpTo(fractionalStop, recordSafe: false);
        if (_player.Position != new Vector2(56, 70))
        {
            throw new InvalidOperationException(
                "Link's fixed-point bridge position was rounded instead of rendered from yh/xh.");
        }
    }

    private void ValidateBridgeSlidePath(Vector2 start, bool expectUp)
    {
        Vector2 position = start;
        bool deflected = false;
        for (int frame = 0; frame < 16; frame++)
        {
            Vector2 resolved = _collision.ResolveMovement(
                position, Vector2.Right, allowWallSlide: true);
            if (resolved == Vector2.Zero)
                throw new InvalidOperationException(
                    $"Room 0:56 bridge slide became stuck at {position}.");
            deflected |= expectUp ? resolved.Y < 0.0f : resolved.Y > 0.0f;
            position += resolved;
        }

        Vector2 expected = start + new Vector2(12.0f, expectUp ? -4.0f : 4.0f);
        if (!deflected || position.DistanceSquaredTo(expected) > 0.01f)
            throw new InvalidOperationException(
                $"Room 0:56 bridge did not apply the symmetric four-frame " +
                $"{(expectUp ? "upward" : "downward")} deflection; " +
                $"start={start}, expected={expected}, end={position}.");
    }

    private void ValidateRoom01HoleBoundaryCase()
    {
        _activeGroup = 0;
        ClearDeactivatedWarp();
        _currentRoom = _world.LoadRoom(_activeGroup, 0x01);
        _roomView.SetRoom(_currentRoom.Texture);

        if (!TryFindHoleWithSafeNeighbor(_currentRoom, out Vector2 holeCenter, out Vector2 safePosition))
            throw new InvalidOperationException("Room 0:01 did not have a testable hole with a safe neighbor.");

        _player.RefillHealth();
        _player.WarpTo(safePosition);
        int beforeHealth = _player.HealthQuarters;

        float tileTop = Mathf.FloorToInt(holeCenter.Y / OracleRoomData.MetatileSize) *
            OracleRoomData.MetatileSize;
        Vector2 boundaryEntry = new(holeCenter.X, tileTop - 5.0f + 0.6f);
        _player.WarpTo(boundaryEntry, recordSafe: false);
        Vector2 expectedCenter = GetActiveTerrain(_player.Position).TileCenter;
        if (expectedCenter.DistanceSquaredTo(holeCenter) > 1.0f)
            throw new InvalidOperationException("Room 0:01 boundary setup did not sample the hole tile.");

        _player._PhysicsProcess(1.0 / 60.0);
        if (!_player.IsPullingIntoHole && !_player.IsFallingInHole)
            throw new InvalidOperationException(
                "Room 0:01 boundary hole entry did not start the pull-in state.");

        AdvanceHolePullUntilFall(holeCenter);
        AdvanceHoleFallUntilRespawn(safePosition);
        if (_player.IsFallingInHole)
            throw new InvalidOperationException("Room 0:01 boundary hole fall did not complete.");
        if (_player.Position.DistanceSquaredTo(safePosition) > 1.0f)
            throw new InvalidOperationException("Room 0:01 hole respawn did not return to the room entry anchor.");
        if (_player.HealthQuarters != beforeHealth - 2)
            throw new InvalidOperationException("Room 0:01 hole fall did not apply half-heart damage.");
    }

    private void AdvanceHolePullUntilFall(Vector2 expectedCenter)
    {
        for (int i = 0; i < 120 && !_player.IsFallingInHole; i++)
            _player._PhysicsProcess(1.0 / 60.0);

        if (!_player.IsFallingInHole)
            throw new InvalidOperationException("Hole pull-in did not transition to the fall animation.");
        if (_player.Position.DistanceSquaredTo(expectedCenter) > 1.0f)
            throw new InvalidOperationException("Hole pull-in did not center Link on the sampled hole tile.");
    }

    private void AdvanceHoleFallUntilRespawn(Vector2 expectedRespawn)
    {
        for (int i = 0; i < 80 && _player.IsFallingInHole; i++)
            _player._PhysicsProcess(1.0 / 60.0);

        if (_player.IsFallingInHole)
            throw new InvalidOperationException("The falling-in-hole animation did not finish.");
        if (_player.Position.DistanceSquaredTo(expectedRespawn) > 1.0f)
            throw new InvalidOperationException("Hole terrain did not return Link to the stored respawn anchor.");
    }

    private static bool TryFindHoleWithSafeNeighbor(
        OracleRoomData room,
        out Vector2 holeCenter,
        out Vector2 safePosition)
    {
        for (int tileY = 1; tileY < room.HeightInTiles; tileY++)
        for (int tileX = 0; tileX < room.WidthInTiles; tileX++)
        {
            Vector2 center = new(
                tileX * OracleRoomData.MetatileSize + 8,
                tileY * OracleRoomData.MetatileSize + 8);
            OracleRoomData.TerrainInfo terrain = room.GetTerrainInfo(center);
            if (terrain.Hazard != OracleRoomData.HazardType.Hole)
                continue;

            foreach (Vector2I direction in new[] { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down })
            {
                Vector2 candidate = center + (Vector2)direction * OracleRoomData.MetatileSize;
                if (candidate.X < 0 || candidate.X >= room.Width ||
                    candidate.Y < 0 || candidate.Y >= room.Height)
                {
                    continue;
                }

                OracleRoomData.TerrainInfo safeTerrain = room.GetTerrainInfo(candidate);
                if (safeTerrain.Hazard == OracleRoomData.HazardType.None &&
                    !RoomCollides(room, candidate))
                {
                    holeCenter = center;
                    safePosition = candidate;
                    return true;
                }
            }
        }

        holeCenter = Vector2.Zero;
        safePosition = Vector2.Zero;
        return false;
    }

    private bool TryFindTerrainSample(
        OracleRoomData.HazardType hazard,
        out int group,
        out int room,
        out Vector2 hazardCenter,
        out Vector2 safePosition)
    {
        for (int candidateGroup = 0; candidateGroup <= 5; candidateGroup++)
        for (int candidateRoom = 0; candidateRoom <= 0xff; candidateRoom++)
        {
            if (!_world.HasRoom(candidateGroup, candidateRoom))
                continue;

            OracleRoomData data = _world.LoadRoom(candidateGroup, candidateRoom);
            Vector2? safe = null;
            Vector2? target = null;

            for (int tileY = 0; tileY < data.HeightInTiles; tileY++)
            for (int tileX = 0; tileX < data.WidthInTiles; tileX++)
            {
                Vector2 center = new(
                    tileX * OracleRoomData.MetatileSize + 8,
                    tileY * OracleRoomData.MetatileSize + 8);
                OracleRoomData.TerrainInfo terrain = data.GetTerrainInfo(center);

                if (terrain.Hazard == OracleRoomData.HazardType.None &&
                    safe == null &&
                    !RoomCollides(data, center))
                {
                    safe = center;
                }
                if (terrain.Hazard == hazard &&
                    terrain.Type == OracleRoomData.TerrainType.Hole &&
                    !RoomCollides(data, center))
                {
                    target = center;
                }
            }

            if (safe != null && target != null)
            {
                group = candidateGroup;
                room = candidateRoom;
                hazardCenter = target.Value;
                safePosition = safe.Value;
                return true;
            }
        }

        group = -1;
        room = -1;
        hazardCenter = Vector2.Zero;
        safePosition = Vector2.Zero;
        return false;
    }

    private static bool RoomCollides(OracleRoomData room, Vector2 playerPosition)
    {
        foreach (Vector2 offset in new[]
        {
            new Vector2(-5, -2), new Vector2(5, -2),
            new Vector2(-5, 5), new Vector2(5, 5)
        })
        {
            Vector2 sample = playerPosition + offset;
            if (sample.X < 0 || sample.X >= room.Width ||
                sample.Y < 0 || sample.Y >= room.Height ||
                room.IsSolid(sample))
            {
                return true;
            }
        }
        return false;
    }

    private void ValidateHealth()
    {
        _dialogue.Close();
        _player.RefillHealth();
        SyncHudToInventory();

        if (_player.HealthQuarters != 12 || _hud.HealthQuarters != 12 ||
            _hud.MaxHealthQuarters != _player.MaxHealthQuarters)
            throw new InvalidOperationException("Expected Link and the HUD to start with three full hearts.");

        _player.ApplyDamage(1);
        if (_player.HealthQuarters != 11 || _hud.HealthQuarters != 11)
            throw new InvalidOperationException("Direct quarter-heart damage did not synchronize to the HUD.");

        _player.Heal(1);
        if (_player.HealthQuarters != 12 || _hud.HealthQuarters != 12)
            throw new InvalidOperationException("Direct quarter-heart healing did not synchronize to the HUD.");

        _activeGroup = 0;
        ClearDeactivatedWarp();
        _currentRoom = _world.LoadRoom(_activeGroup, 0x03);
        _roomView.SetRoom(_currentRoom.Texture);
        Vector2 safe = new(56, 8);
        _player.WarpTo(safe);
        _player.WarpTo(new Vector2(8, 24), recordSafe: false);

        ValidateDrowningSequence(safe, OracleRoomData.HazardType.Lava);
        if (_player.HealthQuarters != 10 || _hud.HealthQuarters != 10)
            throw new InvalidOperationException(
                "Lava hazard did not synchronize its delayed half-heart damage to the HUD.");

        GD.Print("Validated quarter-heart health, HUD synchronization, and delayed half-heart terrain damage.");
    }

    private void ValidateAnimations()
    {
        OracleRoomData water = _world.LoadRoom(0, 0xb8);
        ulong waterStart = water.GetAnimationChecksum(0);
        bool waterChanged = false;
        for (int tick = 1; tick <= 120 && !waterChanged; tick++)
            waterChanged = water.GetAnimationChecksum(tick) != waterStart;

        OracleRoomData lava = _world.LoadRoom(0, 0x03);
        ulong lavaStart = lava.GetAnimationChecksum(0);
        bool lavaChanged = false;
        for (int tick = 1; tick <= 60 && !lavaChanged; tick++)
            lavaChanged = lava.GetAnimationChecksum(tick) != lavaStart;

        if (!waterChanged || !lavaChanged)
            throw new InvalidOperationException(
                $"Expected animated water and lava frames; water={waterChanged}, lava={lavaChanged}.");

        OracleRoomData waterfall = _world.LoadRoom(0, 0x45);
        const int settledWaterfallTick = 32;
        if (waterfall.AnimationGroup != 0 ||
            !waterfall.HasAnimationOverride(4, settledWaterfallTick) ||
            !waterfall.HasAnimationOverride(236, settledWaterfallTick) ||
            !waterfall.HasAnimationOverride(238, settledWaterfallTick))
        {
            throw new InvalidOperationException(
                "Waterfall animation did not preserve its three alternating VRAM destination writes.");
        }

        // Room textures are cached independently, but the original engine has
        // one shared animated-tile VRAM state. Prime 0:56 with a stale phase,
        // then verify that beginning the 0:55 -> 0:56 scroll synchronizes it
        // to the outgoing room before both textures are shown together.
        OracleRoomData target = _world.LoadRoom(0, 0x56);
        target.UpdateAnimation(0);
        int staleTargetSignature = target.CurrentAnimationSignature;
        _activeGroup = 0;
        _currentRoom = _world.LoadRoom(0, 0x55);
        OracleRoomData source = _currentRoom;
        if (!Mathf.IsZeroApprox((float)_animationTicks))
            throw new InvalidOperationException("Changing animation groups did not reset their shared clock.");
        _animationTicks = 13.0;
        source.UpdateAnimation((long)_animationTicks);
        if (source.CurrentAnimationSignature == staleTargetSignature)
            throw new InvalidOperationException("Animation phase-lock validation chose indistinguishable ticks.");

        _roomView.SetRoom(source.Texture);
        _entities.LoadRoom(_activeGroup, source);
        _player.WarpTo(new Vector2(source.Width + 2.0f, source.Height / 2.0f));
        CheckRoomExit(_player);
        if (!IsTransitioning || _activeGroup != 0 || _currentRoom.Id != 0x56)
            throw new InvalidOperationException("Room 0:55 did not begin its rightward scroll into 0:56.");
        if (source.AnimationGroup != _currentRoom.AnimationGroup ||
            source.CurrentAnimationSignature != _currentRoom.CurrentAnimationSignature)
        {
            throw new InvalidOperationException(
                "Outgoing and incoming water/waterfall tiles began the scroll in different phases.");
        }

        ValidateLinkScrollsForOneTransitionFrame();
        if (source.CurrentAnimationSignature != _currentRoom.CurrentAnimationSignature)
            throw new InvalidOperationException("Animated-tile phases diverged during the frozen scroll.");
        FinishActiveScrollingTransitionForValidation();

        GD.Print("Validated disassembly-driven water and lava animation plus persistent " +
            "three-range waterfall VRAM updates and 0:55 -> 0:56 phase locking.");
    }

    private void ValidateSigns()
    {
        WarpToSignTest();
        if (_dialogue.MessageSpeed != _saveData.TextSpeed)
            throw new InvalidOperationException(
                "Gameplay dialogue did not consume the selected save's wTextSpeed value.");
        Color expectedDefaultText = new(0x1f / 31.0f, 0x1a / 31.0f, 0x11 / 31.0f);
        if (!DialogueBox.DefaultTextColorForValidation.IsEqualApprox(expectedDefaultText))
            throw new InvalidOperationException(
                "Default textbox text did not use paletteData48e0's white color 2.");
        if (DialogueBox.ContinueMarkerRectForValidation != new Rect2(144, 32, 8, 8) ||
            _dialogue.ContinueMarkerOpaquePixelCountForValidation() != 22)
        {
            throw new InvalidOperationException(
                "The textbox continue marker did not use gfx_hud tile $03 at its original tile position.");
        }
        if (_dialogue.VisibleLinesPerPage != 2 || _dialogue.TextLineSpacing != 16)
            throw new InvalidOperationException(
                "The textbox does not use the original two 8x16 text rows.");
        if (_currentRoom.GetMetatile(new Vector2(88, 58)) != 0xf2)
            throw new InvalidOperationException("Expected sign metatile $f2 in room 2a at $35.");
        if (!TryInteract(_player) || !_dialogue.IsOpen)
            throw new InvalidOperationException("The room 2a test sign did not open its dialogue.");

        _dialogue.RevealCurrentPageForValidation();
        if (!_dialogue.IsPageComplete || _dialogue.HasNextMessage || _dialogue.ArrowVisible)
        {
            throw new InvalidOperationException(
                "The final two-line sign message incorrectly displayed a continuation prompt.");
        }

        _dialogue.ShowMessage("First.\nSecond.\nThird.", _player.Position.Y);
        _dialogue.RevealCurrentPageForValidation();
        if (!_dialogue.HasNextMessage || _dialogue.ArrowVisible)
            throw new InvalidOperationException(
                "A multi-line textbox did not begin its continuation prompt on the blank phase.");
        _dialogue.AdvanceArrowClockForValidation(16.0 / 60.0);
        if (!_dialogue.ArrowVisible)
            throw new InvalidOperationException("The textbox arrow did not appear after 16 original-engine frames.");
        _dialogue.AdvanceArrowClockForValidation(16.0 / 60.0);
        if (_dialogue.ArrowVisible)
            throw new InvalidOperationException("The textbox arrow did not complete its 32-frame blink cycle.");

        int selectedSpeed = _dialogue.MessageSpeed;
        int[] expectedCharacterFrames = { 7, 5, 4, 3, 2 };
        for (int speed = 0; speed < expectedCharacterFrames.Length; speed++)
        {
            _dialogue.MessageSpeed = speed;
            if (_dialogue.CharacterDisplayFrameLength != expectedCharacterFrames[speed])
                throw new InvalidOperationException(
                    $"Message speed {speed} did not select {expectedCharacterFrames[speed]} updates per character.");
        }
        _dialogue.MessageSpeed = 0;
        _dialogue.ShowMessage("AB", _player.Position.Y);
        _dialogue.AdvanceCharacterClockForValidation(6.0 / 60.0);
        if (_dialogue.VisibleGlyphCount != 0)
            throw new InvalidOperationException("Message speed 0 displayed a character before update 7.");
        _dialogue.AdvanceCharacterClockForValidation(1.0 / 60.0);
        if (_dialogue.VisibleGlyphCount != 1)
            throw new InvalidOperationException("Message speed 0 did not display its first character on update 7.");
        _dialogue.MessageSpeed = 4;
        _dialogue.ShowMessage("AB", _player.Position.Y);
        _dialogue.AdvanceCharacterClockForValidation(1.0 / 60.0);
        if (_dialogue.VisibleGlyphCount != 0)
            throw new InvalidOperationException("Message speed 4 displayed a character before update 2.");
        _dialogue.AdvanceCharacterClockForValidation(1.0 / 60.0);
        if (_dialogue.VisibleGlyphCount != 1)
            throw new InvalidOperationException("Message speed 4 did not display its first character on update 2.");

        _dialogue.ShowMessage(
            "\\col(1)R\\col(3)B\\col(0)N\n\\sym(0x57)♪\\heart\\abtn\\bbtn",
            _player.Position.Y);
        if (_dialogue.GlyphColorForValidation(0, 0, 0) != 1 ||
            _dialogue.GlyphColorForValidation(0, 0, 1) != 3 ||
            _dialogue.GlyphColorForValidation(0, 0, 2) != 0 ||
            !_dialogue.GlyphUsesSymbolFontForValidation(0, 1, 0) ||
            _dialogue.GlyphCodeForValidation(0, 1, 0) != 0x57 ||
            !_dialogue.GlyphUsesSymbolFontForValidation(0, 1, 1) ||
            _dialogue.GlyphCodeForValidation(0, 1, 1) != 0x1c ||
            _dialogue.GlyphUsesSymbolFontForValidation(0, 1, 2) ||
            _dialogue.GlyphCodeForValidation(0, 1, 2) != 0x14 ||
            _dialogue.GlyphCodeForValidation(0, 1, 3) != 0xb8 ||
            _dialogue.GlyphCodeForValidation(0, 1, 4) != 0xb9 ||
            _dialogue.GlyphCodeForValidation(0, 1, 5) != 0xba ||
            _dialogue.GlyphCodeForValidation(0, 1, 6) != 0xbb)
        {
            throw new InvalidOperationException(
                "Textbox color commands or main/symbol-font glyph selection were not preserved.");
        }
        _dialogue.MessageSpeed = selectedSpeed;

        _dialogue.ShowMessage("First.\nSecond.\nThird.\nFourth.", _player.Position.Y);
        _dialogue.RevealCurrentPageForValidation();
        _dialogue.AdvanceOrClose();
        if (!_dialogue.IsScrollingText ||
            !Mathf.IsEqualApprox(_dialogue.TextScrollOffset, 8.0f))
        {
            throw new InvalidOperationException(
                "The button frame did not perform standardTextStateb's first 8px shift.");
        }
        _dialogue.AdvanceTextScrollForValidation(1.0 / 60.0);
        if (!_dialogue.IsScrollingText ||
            !Mathf.IsEqualApprox(_dialogue.TextScrollOffset, 8.0f))
        {
            throw new InvalidOperationException(
                "The standard textbox DMA state did not hold the first 8px shift for one frame.");
        }
        _dialogue.AdvanceTextScrollForValidation(1.0 / 60.0);
        if (!Mathf.IsEqualApprox(_dialogue.TextScrollOffset, 16.0f))
            throw new InvalidOperationException("standardTextState7 did not perform the second 8px shift.");
        _dialogue.AdvanceTextScrollForValidation(5.0 / 60.0);
        if (_dialogue.IsScrollingText || !Mathf.IsZeroApprox(_dialogue.TextScrollOffset))
            throw new InvalidOperationException(
                "The two discrete tile-row shifts did not finish the one-line text scroll.");

        _dialogue.ShowMessage("Last line.", _player.Position.Y);
        _dialogue.RevealCurrentPageForValidation();
        _dialogue.AdvanceArrowClockForValidation(32.0 / 60.0);
        if (_dialogue.HasNextMessage || _dialogue.ArrowVisible)
            throw new InvalidOperationException("The final dialogue message displayed a continuation arrow.");
        _dialogue.AdvanceOrClose();
        if (_dialogue.IsOpen || !_dialogue.BlocksPlayerInput)
            throw new InvalidOperationException("Closing the final textbox did not consume its button press.");
        _player._PhysicsProcess(1.0 / 60.0);
        if (_dialogue.IsOpen)
            throw new InvalidOperationException("The final textbox press immediately restarted the interaction.");

        GD.Print("Validated save-selected 7/5/4/3/2-update dialogue speed, white default " +
            "text, colored and symbol-font glyphs, gfx_hud tile $03 continuation marker, " +
            "one-line tile-row scrolling, continuation-only 32-update blink, and " +
            "final-message input consumption.");
    }

    private void ValidateNpcs()
    {
        WarpToNpcTest();
        NpcCharacter? villager = _npcNodes.Find(npc => npc.Record.Id == 0x3a && npc.Record.SubId == 0x03);
        if (villager is null)
            throw new InvalidOperationException($"Expected the room 0:48 villager among {_npcNodes.Count} extracted NPCs.");
        if (villager.TextId != 0x1420)
            throw new InvalidOperationException($"Expected room 0:48 villager to resolve TX_1420, got TX_{villager.TextId:x4}.");
        if (villager.Position != new Vector2(0x38, 0x48) ||
            villager.SpriteBounds.GetCenter() != villager.Position)
        {
            throw new InvalidOperationException("The room 0:48 villager sprite is not centered on its object tile.");
        }
        if (villager.ObjectCollisionBounds.Size != new Vector2(12.0f, 12.0f) ||
            villager.ObjectCollisionBounds.GetCenter() != villager.Position)
        {
            throw new InvalidOperationException("The room 0:48 villager object hitbox does not match the original $06/$06 collision radii.");
        }
        if (villager.LinkBlockingBounds.Size != new Vector2(24.0f, 24.0f) ||
            villager.LinkBlockingBounds.GetCenter() != villager.Position)
        {
            throw new InvalidOperationException("The room 0:48 villager does not combine NPC and Link $06 radii into a 24px blocking region.");
        }
        if (!villager.BlocksLinkCenter(villager.Position) ||
            villager.BlocksLinkCenter(villager.Position + new Vector2(0.0f, 12.0f)))
        {
            throw new InvalidOperationException("The room 0:48 villager's strict radius collision boundary is not centered correctly.");
        }
        if (!Collides(villager.Position + new Vector2(0.0f, 11.9f)) ||
            Collides(villager.Position + new Vector2(0.0f, 12.1f)))
        {
            throw new InvalidOperationException("The room 0:48 villager did not stop Link at the original bottom collision radius.");
        }
        if (!TryInteract(_player) || !_dialogue.IsOpen)
            throw new InvalidOperationException("The room 0:48 villager did not open dialogue.");
        int frameBase = villager.Record.TileBase / 4;
        if (villager.CurrentFrameColumn != frameBase)
            throw new InvalidOperationException("The room 0:48 villager did not face down toward Link after talking.");
        villager.UpdateNpc(16.0 / 60.0, _player.Position);
        if (villager.CurrentAnimationFrame != 1)
            throw new InvalidOperationException("The room 0:48 villager did not advance its original 16-frame idle animation.");
        villager.UpdateNpc(1.0 / 60.0, villager.Position + Vector2.Left * 20.0f);
        if (villager.FacingVector != Vector2I.Left || villager.CurrentAnimationFrame != 0)
            throw new InvalidOperationException("The room 0:48 villager did not face nearby Link and reset its animation.");
        villager.UpdateNpc(1.0 / 60.0, villager.Position + Vector2.Right * 20.0f);
        if (villager.FacingVector != Vector2I.Left)
            throw new InvalidOperationException("The NPC facing cooldown did not preserve the current direction.");
        villager.UpdateNpc(30.0 / 60.0, villager.Position + Vector2.Right * 20.0f);
        if (villager.FacingVector != Vector2I.Right || villager.CurrentFrameColumn != frameBase + 2)
            throw new InvalidOperationException("The villager did not use the mirrored side OAM after the 30-frame facing delay.");
        villager.UpdateNpc(30.0 / 60.0, villager.Position + Vector2.Right * 80.0f);
        if (villager.FacingVector != Vector2I.Down)
            throw new InvalidOperationException("The villager did not return to facing down when Link left the $28 awareness radius.");

        _dialogue.Close();
        _transitions.BeginScroll(_player, Vector2I.Down, 0x58);
        NpcCharacter? destinationNpc = _npcNodes.Find(npc =>
            npc.Record.Room == 0x58 && npc.Record.Id == 0x41 && npc.Record.SubId == 0x04);
        if (!_entities.ScreenTransitionActive || _currentRoom.Id != 0x58 ||
            _entities.OutgoingEntities<NpcCharacter>().Count != 2 || destinationNpc is null)
        {
            throw new InvalidOperationException(
                $"The 0:48 -> 0:58 scroll did not retain two outgoing NPCs and preload the destination NPC " +
                $"(active={_entities.ScreenTransitionActive}, room={_currentRoom.Id:x2}, " +
                $"outgoing={_entities.OutgoingEntities<NpcCharacter>().Count}, incoming={_npcNodes.Count}, " +
                $"destinationFound={destinationNpc is not null}).");
        }
        foreach (NpcCharacter outgoingNpc in _entities.OutgoingEntities<NpcCharacter>())
        {
            if (outgoingNpc.Record.Room != 0x48 ||
                !outgoingNpc.TransitionDrawOffset.IsEqualApprox(Vector2.Zero))
            {
                throw new InvalidOperationException(
                    "An outgoing room 0:48 NPC was not retained at its initial screen position.");
            }
        }
        if (!destinationNpc.TransitionDrawOffset.IsEqualApprox(
            Vector2.Down * OracleRoomData.ViewportHeight))
        {
            throw new InvalidOperationException(
                "The room 0:58 NPC was not staged one screen below the outgoing room.");
        }

        UpdateScrollingTransition(1.0 / 60.0);
        foreach (NpcCharacter outgoingNpc in _entities.OutgoingEntities<NpcCharacter>())
        {
            if (!outgoingNpc.TransitionDrawOffset.IsEqualApprox(Vector2.Up * 4.0f))
                throw new InvalidOperationException("An outgoing NPC did not move with its scrolling room.");
        }
        if (!destinationNpc.TransitionDrawOffset.IsEqualApprox(
            Vector2.Down * (OracleRoomData.ViewportHeight - 4.0f)))
        {
            throw new InvalidOperationException("The preloaded destination NPC did not scroll into view with room 0:58.");
        }

        FinishActiveScrollingTransitionForValidation();
        if (_entities.ScreenTransitionActive || _entities.OutgoingEntities<NpcCharacter>().Count != 0 ||
            !destinationNpc.TransitionDrawOffset.IsEqualApprox(Vector2.Zero) ||
            destinationNpc.GetParent() != _scene.WorldRoot)
        {
            throw new InvalidOperationException(
                "The destination NPC did not become the normal room NPC after the scroll completed.");
        }

        _activeGroup = 0;
        _currentRoom = _world.LoadRoom(_activeGroup, 0x66);
        _roomView.SetRoom(_currentRoom.Texture);
        RefreshRoomObjects();
        NpcCharacter? woman = _npcNodes.Find(npc =>
            npc.Record.Id == 0x3b && npc.Record.SubId == 0x01);
        if (woman is null || woman.Position != new Vector2(0x7a, 0x44))
            throw new InvalidOperationException("Expected female villager $3b/$01 at 0:66 $44/$7a.");

        woman.UpdateDrawPriority(woman.Position - Vector2.Down * 12.0f);
        if (woman.ZIndex != NpcCharacter.InFrontOfLinkZIndex)
            throw new InvalidOperationException(
                "Room 0:66's woman did not cover Link when yh exceeded w1Link.yh+$0b.");
        woman.UpdateDrawPriority(woman.Position - Vector2.Down * 11.0f);
        if (woman.ZIndex != NpcCharacter.BehindLinkZIndex)
            throw new InvalidOperationException(
                "Room 0:66's woman covered Link at the strict w1Link.yh+$0b boundary.");

        Color linkBlack = Player.RecolorLinkPixel(new Color(0.25f, 0.25f, 0.25f));
        Color linkGreen = Player.RecolorLinkPixel(new Color(0.75f, 0.75f, 0.75f));
        Color linkSkin = Player.RecolorLinkPixel(Colors.White);
        if (!linkBlack.IsEqualApprox(Colors.Black) ||
            !linkGreen.IsEqualApprox(new Color(0x02 / 31.0f, 0x15 / 31.0f, 0x08 / 31.0f)) ||
            !linkSkin.IsEqualApprox(new Color(0x1f / 31.0f, 0x1a / 31.0f, 0x11 / 31.0f)))
        {
            throw new InvalidOperationException(
                "Link did not use standardSpritePaletteData palette 0 selected by OAM flags $08.");
        }

        GD.Print("Validated villager idle animation, $28 Link awareness, 30-frame facing delay, " +
            "TX_1420 dialogue, retained/preloaded NPC screen scrolling, room 0:66 " +
            "Link-relative draw priority, and Link sprite palette 0.");
    }

    private void ValidateRoom149FamilyInteractions()
    {
        const double frame = 1.0 / 60.0;
        var validationRoot = new Node { Name = "Room149FamilyValidation" };
        AddChild(validationRoot);
        OracleSaveData save = OracleSaveData.CreateStandardGame();
        var manager = new RoomEntityManager(
            validationRoot, new NpcDatabase(), new EnemyDatabase(), save);
        var database = new Room149FamilyDatabase();
        manager.LoadRoom(1, _world.LoadRoom(1, 0x49));

        NpcCharacter? boy = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x3c && npc.Record.SubId == 0x0e);
        NpcCharacter? father = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x3a && npc.Record.SubId == 0x0c);
        NpcCharacter? observer = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x43 && npc.Record.SubId == 0x06);
        Room149Ball? ball = manager.Entities<Room149Ball>().SingleOrDefault();
        if (manager.Entities<NpcCharacter>().Count != 3 ||
            boy is null || father is null || observer is null || ball is null ||
            boy.Position != new Vector2(0x78, 0x48) || boy.TextId != 0x251d ||
            boy.TextPosition != 0 || father.Position != new Vector2(0x38, 0x48) ||
            father.TextId != 0x1442 || observer.Position != new Vector2(0x78, 0x28) ||
            observer.TextId != 0x1712 || !ball.Active || !ball.Idle ||
            ball.Position != new Vector2(0x75, 0x4a) ||
            boy.CurrentScriptAnimationSource != database.Visual("boy").Animation ||
            father.CurrentScriptAnimationSource !=
                database.Visual("father-default").Animation ||
            observer.CurrentScriptAnimationSource !=
                database.Visual("observer").Animation)
        {
            throw new InvalidOperationException(
                "Room 1:49 did not load the pre-D7 father/son catch interaction, " +
                "observer, imported animations, and TX_251d/TX_1442/TX_1712.");
        }

        ulong normalFatherHash = father.CurrentAnimationPixelHash;
        ulong normalObserverHash = observer.CurrentAnimationPixelHash;
        if (save.WriteWramByte(0xc6bf, 0xbf))
            save.CommitInventoryChange();
        save.SetRoomFlag(4, 0xfc, 0x7f);
        save.SetRoomFlag(4, 0xfb, OracleSaveData.RoomFlag80);
        save.SetRoomFlag(5, 0xfc, OracleSaveData.RoomFlag80);
        if (boy.Position != new Vector2(0x78, 0x48) || boy.TextId != 0x251d ||
            father.TextId != 0x1442 || observer.TextId != 0x1712 ||
            !ball.Active || !ball.Idle ||
            father.CurrentAnimationPixelHash != normalFatherHash ||
            observer.CurrentAnimationPixelHash != normalObserverHash)
        {
            throw new InvalidOperationException(
                "Unrelated wEssencesObtained bits, room 4:fc bits $01-$40, " +
                "room 4:fb bit $80, or group-5 room fc bit $80 changed room " +
                "1:49's pre-D7 family state.");
        }

        if (save.WriteWramByte(0xc6bf, 0xff))
            save.CommitInventoryChange();
        if (boy.Position != new Vector2(0x48, 0x48) ||
            boy.TextId != 0x251b || boy.TextPosition != 2 ||
            father.TextId != 0 || observer.TextId != 0 || ball.Active ||
            father.CurrentScriptAnimationSource !=
                database.Visual("father-stone").Animation ||
            father.CurrentAnimationPixelHash == normalFatherHash ||
            observer.CurrentAnimationPixelHash == normalObserverHash ||
            !father.CurrentAnimationUsesColor(database.StonePalette[1]) &&
            !father.CurrentAnimationUsesColor(database.StonePalette[2]) &&
            !father.CurrentAnimationUsesColor(database.StonePalette[3]))
        {
            throw new InvalidOperationException(
                "D7 essence bit 6 did not move the room 1:49 boy to $48/$48, " +
                "select TX_251b with \\pos(2), petrify the father/observer with " +
                "PALH_a2, suppress their dialogue, and remove INTERAC_BALL $95.");
        }

        save.SetRoomFlag(4, 0xfc, OracleSaveData.RoomFlag80);
        if (boy.Position != new Vector2(0x78, 0x48) || boy.TextId != 0x251e ||
            boy.TextPosition != 0 || father.TextId != 0x1443 ||
            observer.TextId != 0x1712 || !ball.Active || !ball.Idle ||
            ball.Position != new Vector2(0x75, 0x4a) ||
            father.CurrentScriptAnimationSource !=
                database.Visual("father-default").Animation ||
            father.CurrentAnimationPixelHash != normalFatherHash ||
            observer.CurrentAnimationPixelHash != normalObserverHash)
        {
            throw new InvalidOperationException(
                "Room 4:fc flag $80 did not restore room 1:49's family, ball, " +
                "normal palettes, positions, and TX_251e/TX_1443/TX_1712 live.");
        }

        if (save.WriteWramByte(0xc6bf, 0xbf))
            save.CommitInventoryChange();
        if (boy.TextId != 0x251e || father.TextId != 0x1443 ||
            observer.TextId != 0x1712 || !ball.Active)
        {
            throw new InvalidOperationException(
                "Room 4:fc flag $80 did not take precedence after D7 essence " +
                "bit 6 was cleared live.");
        }

        save.SetRoomFlag(4, 0xfc, OracleSaveData.RoomFlag80, value: false);
        if (boy.TextId != 0x251d || father.TextId != 0x1442 ||
            observer.TextId != 0x1712 || !ball.Active)
        {
            throw new InvalidOperationException(
                "Clearing room 4:fc flag $80 with D7 essence bit 6 clear did " +
                "not restore room 1:49's pre-D7 state live.");
        }

        if (save.WriteWramByte(0xc6bf, 0xff))
            save.CommitInventoryChange();
        if (boy.TextId != 0x251b || father.TextId != 0 || observer.TextId != 0 ||
            ball.Active)
        {
            throw new InvalidOperationException(
                "D7 essence bit 6 did not reselect room 1:49's stone state " +
                "after the Veran flag was cleared live.");
        }

        save.SetRoomFlag(4, 0xfc, OracleSaveData.RoomFlag80);
        if (boy.TextId != 0x251e || father.TextId != 0x1443 ||
            observer.TextId != 0x1712 || !ball.Active || !ball.Idle ||
            ball.Position != new Vector2(0x75, 0x4a))
        {
            throw new InvalidOperationException(
                "Reapplying room 4:fc flag $80 did not restore and reset room " +
                "1:49's post-Veran catch interaction live.");
        }

        _player.WarpTo(new Vector2(0x18, 0x70));
        for (int update = 0; update < 29; update++)
            manager.Update(frame, _player);
        if (!ball.Idle || ball.Position != new Vector2(0x75, 0x4a) ||
            boy.CurrentAnimationFrame != 0)
        {
            throw new InvalidOperationException(
                "The room 1:49 boy threw the ball before his initial 30-update wait.");
        }

        manager.Update(frame, _player);
        if (ball.Idle || ball.SubId != 1 || ball.Position != new Vector2(0x75, 0x4a) ||
            ball.ZFixed != 0 || ball.SpeedZ != -0x1c0 ||
            boy.CurrentAnimationFrame != 1)
        {
            throw new InvalidOperationException(
                "The boy's cfd3=$02 update did not force his throw frame and launch " +
                "INTERAC_BALL $95 left from $4a/$75 at Z speed -$01c0.");
        }

        for (int update = 0; update < 29; update++)
            manager.Update(frame, _player);
        if (!ball.Idle || ball.Position != new Vector2(0x3c, 0x4a) ||
            ball.ZFixed != 0 || ball.SpeedZ != 0)
        {
            throw new InvalidOperationException(
                "The boy's ball did not land at the father's original $4a/$3c " +
                "position after the exact SPEED_200/-$01c0/$20 flight.");
        }

        for (int update = 0; update < 30; update++)
            manager.Update(frame, _player);
        if (!ball.Idle)
            throw new InvalidOperationException(
                "The father threw before his initial 60+30 update script waits.");
        manager.Update(frame, _player);
        if (ball.Idle || ball.SubId != 0 ||
            ball.Position != new Vector2(0x3c, 0x4a) ||
            ball.SpeedZ != -0x1c0 || father.CurrentAnimationFrame != 1)
        {
            throw new InvalidOperationException(
                "The father's cfd3=$01 update did not force his throw frame and " +
                "launch INTERAC_BALL $95 right on update 90.");
        }

        manager.Clear();
        RemoveChild(validationRoot);
        validationRoot.QueueFree();
        GD.Print("Validated room 1:49's exact D7/Veran flag truth table and precedence, " +
            "six imported texts, PALH_a2 stone palette, exact actor positions, " +
            "30/60/90-update synchronized throw scripts, and INTERAC_BALL $95 " +
            "SPEED_200 8.8 parabolic flights.");
    }

    private void ValidateNpcFlagVisibility()
    {
        var validationRoot = new Node { Name = "NpcFlagVisibilityValidation" };
        AddChild(validationRoot);
        OracleSaveData save = OracleSaveData.CreateStandardGame();
        var manager = new RoomEntityManager(
            validationRoot, new NpcDatabase(), new EnemyDatabase(), save);

        if (new NpcVisibilityRuleDatabase().RuleCount != 303 ||
            new NpcDialogueRuleDatabase().RuleCount != 54)
            throw new InvalidOperationException(
                "Expected 303 NPC visibility and 54 NPC dialogue state predicates.");

        manager.LoadRoom(0, _world.LoadRoom(0, 0x5a));
        List<NpcCharacter> introMonkeys = manager.Entities<NpcCharacter>().Where(npc =>
            npc.Record.Id == 0x39 && npc.Record.SubId is 0x02 or 0x03).ToList();
        if (introMonkeys.Count != 2 ||
            introMonkeys[0].TextId != 0x5700 || introMonkeys[1].TextId != 0x5701 ||
            introMonkeys[0].Record.DefaultAnimation != 0x06 ||
            introMonkeys[1].Record.DefaultAnimation != 0x07 ||
            introMonkeys.Any(monkey => !monkey.Active || string.IsNullOrEmpty(monkey.Message)))
        {
            throw new InvalidOperationException(
                "Room 0:5a did not load its two active intro monkeys with TX_5700/TX_5701 and animations $06/$07.");
        }
        foreach (NpcCharacter monkey in introMonkeys)
        {
            _player.WarpTo(monkey.Position + Vector2.Down * 16.0f);
            _player.Face(Vector2I.Up);
            if (!monkey.CanTalkTo(_player))
                throw new InvalidOperationException(
                    $"Room 0:5a monkey ${monkey.Record.Id:x2}:${monkey.Record.SubId:x2} was not talkable from below.");
        }
        ulong upperMonkeyFirstFrame = introMonkeys[0].CurrentAnimationPixelHash;
        ulong lowerMonkeyFirstFrame = introMonkeys[1].CurrentAnimationPixelHash;
        foreach (NpcCharacter monkey in introMonkeys)
            monkey.UpdateNpc(31.0 / 60.0, _player.Position);
        if (introMonkeys.Any(monkey => monkey.CurrentAnimationFrame != 0))
            throw new InvalidOperationException(
                "A room 0:5a monkey advanced before animation $06/$07's original $20-frame duration.");
        foreach (NpcCharacter monkey in introMonkeys)
            monkey.UpdateNpc(1.0 / 60.0, _player.Position);
        if (introMonkeys.Any(monkey => monkey.CurrentAnimationFrame != 1) ||
            introMonkeys[0].CurrentAnimationPixelHash != lowerMonkeyFirstFrame ||
            introMonkeys[1].CurrentAnimationPixelHash != upperMonkeyFirstFrame)
        {
            throw new InvalidOperationException(
                "Room 0:5a's animation $06/$07 monkeys did not swap their two original poses after $20 frames.");
        }
        foreach (NpcCharacter monkey in introMonkeys)
            monkey.UpdateNpc(32.0 / 60.0, _player.Position);
        if (introMonkeys.Any(monkey => monkey.CurrentAnimationFrame != 0))
            throw new InvalidOperationException(
                "Room 0:5a's two-pose monkey animation did not loop after another $20 frames.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlagIntroDone);
        if (introMonkeys.Any(monkey => monkey.Active || monkey.Visible))
            throw new InvalidOperationException(
                "GLOBALFLAG_INTRO_DONE $0a did not remove room 0:5a's intro monkeys.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlagIntroDone, value: false);
        if (introMonkeys.Any(monkey => !monkey.Active || !monkey.Visible))
            throw new InvalidOperationException(
                "Clearing GLOBALFLAG_INTRO_DONE $0a did not restore room 0:5a's intro monkeys.");

        save.SetGlobalFlag(OracleSaveData.GlobalFlagIntroDone);
        manager.LoadRoom(2, _world.LoadRoom(2, 0xea));
        List<NpcCharacter> newbornLeftFamily = manager.Entities<NpcCharacter>();
        NpcCharacter? newbornBipin = newbornLeftFamily.Find(npc =>
            npc.Record.Id == 0x28 && npc.Record.SubId == 0x00);
        NpcCharacter? newbornBlossom = newbornLeftFamily.Find(npc =>
            npc.Record.Id == 0x2b && npc.Record.SubId == 0x00);
        if (newbornLeftFamily.Count != 2 ||
            newbornBipin is not { TextId: 0x4300 } ||
            newbornBipin.Position != new Vector2(0x48, 0x48) ||
            newbornBipin.Record.DefaultAnimation != 0x04 ||
            !CanTalkTo(newbornBipin) ||
            newbornBlossom is not { TextId: 0x4400 } ||
            newbornBlossom.Position != new Vector2(0x78, 0x38) ||
            !CanTalkTo(newbornBlossom))
        {
            throw new InvalidOperationException(
                "Room 2:ea did not expand family stage $00 into talkable Bipin/Blossom actors.");
        }

        string bipinRunningLeft = newbornBipin.Record.DownAnimation;
        string bipinRunningRight = newbornBipin.Record.RightAnimation;
        if (string.IsNullOrEmpty(bipinRunningLeft) ||
            string.IsNullOrEmpty(bipinRunningRight) ||
            bipinRunningLeft == bipinRunningRight)
        {
            throw new InvalidOperationException(
                "Bipin $28:$00 did not import distinct animation $04/$05 running records.");
        }
        for (int frame = 0; frame < 32; frame++)
            manager.Update(1.0 / 60.0, _player);
        if (newbornBipin.Position != new Vector2(0x28, 0x48))
            throw new InvalidOperationException(
                "Bipin $28:$00 did not move left at SPEED_100 to X=$28 after 32 updates.");
        manager.Update(1.0 / 60.0, _player);
        if (newbornBipin.Position != new Vector2(0x27, 0x48) ||
            newbornBipin.CurrentScriptAnimationSource != bipinRunningRight)
        {
            throw new InvalidOperationException(
                "Bipin $28:$00 did not reverse and toggle animation $04->$05 after leaving X=$28.");
        }
        for (int frame = 0; frame < 49; frame++)
            manager.Update(1.0 / 60.0, _player);
        if (newbornBipin.Position != new Vector2(0x58, 0x48) ||
            newbornBipin.CurrentScriptAnimationSource != bipinRunningLeft)
        {
            throw new InvalidOperationException(
                "Bipin $28:$00 did not reverse and toggle animation $05->$04 at X=$58.");
        }

        // Link begins exactly at the legal left edge of Bipin's collision box.
        // Bipin's next leftward update creates a one-pixel overlap, which the
        // original objectPreventLinkFromPassing immediately resolves leftward.
        _player.WarpTo(new Vector2(0x4c, 0x48));
        manager.Update(1.0 / 60.0, _player);
        if (newbornBipin.Position != new Vector2(0x57, 0x48) ||
            _player.Position != new Vector2(0x4b, 0x48))
        {
            throw new InvalidOperationException(
                "Running Bipin entered Link from the side without resolving their collision.");
        }
        manager.LoadRoom(2, _world.LoadRoom(2, 0xeb));
        if (manager.Entities<NpcCharacter>().Count != 0)
            throw new InvalidOperationException(
                "Room 2:eb was not empty during family stage $00.");

        bool familySaveChanged = save.WriteWramByte(
            OracleSaveData.ChildStageAddress, 0x04);
        familySaveChanged |= save.WriteWramByte(
            OracleSaveData.ChildPersonalityAddress, 0x01);
        if (familySaveChanged)
            save.CommitInventoryChange();
        manager.LoadRoom(2, _world.LoadRoom(2, 0xea));
        List<NpcCharacter> shyStage4Left = manager.Entities<NpcCharacter>();
        if (shyStage4Left.Count != 2 ||
            shyStage4Left.Find(npc => npc.Record.Id == 0x2b) is not
                { Record.SubId: 0x04, TextId: 0x4417 } ||
            shyStage4Left.Find(npc => npc.Record.Id == 0x35) is not
                { Record.SubId: 0x01, Record.Var03: 0x02, TextId: 0x4200 })
        {
            throw new InvalidOperationException(
                "Room 2:ea did not select the shy family stage-$04 actors and dialogue.");
        }
        manager.LoadRoom(2, _world.LoadRoom(2, 0xeb));
        List<NpcCharacter> shyStage4Right = manager.Entities<NpcCharacter>();
        if (shyStage4Right.Count != 1 ||
            shyStage4Right[0].Record is not { Id: 0x28, SubId: 0x04 } ||
            shyStage4Right[0].TextId != 0x4304)
        {
            throw new InvalidOperationException(
                "Room 2:eb did not select Bipin for shy family stage $04.");
        }

        familySaveChanged = save.WriteWramByte(
            OracleSaveData.ChildStageAddress, 0x06);
        familySaveChanged |= save.WriteWramByte(
            OracleSaveData.NextChildStageAddress, 0x07);
        familySaveChanged |= save.WriteWramByte(
            OracleSaveData.ChildPersonalityAddress, 0x02);
        familySaveChanged |= save.WriteWramByte(
            OracleSaveData.ChildStatusAddress, 0x0e);
        familySaveChanged |= save.WriteWramByte(0xc6bf, 0x03);
        if (familySaveChanged)
            save.CommitInventoryChange();
        manager.RuntimeState.SetWramByte(
            OracleRuntimeState.SeedTreeRefilledBitsetAddress, 0x02);
        manager.LoadRoom(2, _world.LoadRoom(2, 0xeb));
        List<NpcCharacter> warriorStage7Right = manager.Entities<NpcCharacter>();
        if (save.ReadWramByte(OracleSaveData.ChildStageAddress) != 0x07 ||
            save.ReadWramByte(OracleSaveData.ChildPersonalityAddress) != 0x01 ||
            manager.RuntimeState.ReadWramByte(
                OracleRuntimeState.SeedTreeRefilledBitsetAddress) != 0 ||
            warriorStage7Right.Count != 2 ||
            warriorStage7Right.Find(npc => npc.Record.Id == 0x2b) is not
                { Record.SubId: 0x07, Record.Var03: 0x01, TextId: 0x4426 } ||
            warriorStage7Right.Find(npc => npc.Record.Id == 0x28) is not
                { Record.SubId: 0x07, TextId: 0x4307 })
        {
            throw new InvalidOperationException(
                "The family spawner did not advance curious stage $06 to warrior stage $07 " +
                "after two essences and seed-tree refill bit 1.");
        }
        save.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame);
        manager.LoadRoom(2, _world.LoadRoom(2, 0xea));
        if (manager.Entities<NpcCharacter>().Count != 0)
            throw new InvalidOperationException(
                "GLOBALFLAG_FINISHEDGAME $14 did not delete the Bipin/Blossom family spawner.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame, value: false);
        familySaveChanged = save.WriteWramByte(
            OracleSaveData.ChildStageAddress, 0x00);
        familySaveChanged |= save.WriteWramByte(
            OracleSaveData.NextChildStageAddress, 0x00);
        familySaveChanged |= save.WriteWramByte(
            OracleSaveData.ChildPersonalityAddress, 0x00);
        familySaveChanged |= save.WriteWramByte(
            OracleSaveData.ChildStatusAddress, 0x00);
        familySaveChanged |= save.WriteWramByte(0xc6bf, 0x00);
        if (familySaveChanged)
            save.CommitInventoryChange();

        manager.LoadRoom(0, _world.LoadRoom(0, 0x3a));
        NpcCharacter? finishedGameBoy = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x3c && npc.Record.SubId == 0x10);
        NpcCharacter? postgameBear = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x5d && npc.Record.SubId == 0x02 && npc.Record.Var03 == 0x01);
        NpcCharacter? postgameMonkey = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x39 && npc.Record.SubId == 0x07 && npc.Record.Var03 == 0x01);
        if (finishedGameBoy is not { Active: false } ||
            postgameBear is not { Active: false } ||
            postgameMonkey is not { Active: false })
            throw new InvalidOperationException(
                "Room 0:3a's finished-game NPC variants appeared before GLOBALFLAG_FINISHEDGAME $14.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame);
        if (!finishedGameBoy.Active || !postgameBear.Active || !postgameMonkey.Active)
            throw new InvalidOperationException(
                "Room 0:3a did not reveal its boy, bear, and monkey finished-game variants.");

        save.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame, value: false);
        save.SetGlobalFlag(
            OracleSaveData.GlobalFlagSawTwinrovaBeforeEndgame, value: false);
        manager.LoadRoom(0, _world.LoadRoom(0, 0x7b));
        List<NpcCharacter> graveyardBoys = manager.Entities<NpcCharacter>().Where(npc =>
            (npc.Record.Id == 0x3c && npc.Record.SubId is 0x03 or 0x04) ||
            (npc.Record.Id == 0x3f && npc.Record.SubId == 0x02)).ToList();
        if (graveyardBoys.Count != 3 || graveyardBoys.Any(npc => !npc.Active))
            throw new InvalidOperationException(
                "Room 0:7b did not begin with all three room-flag-gated children visible.");
        save.SetRoomFlag(0, 0x7b, OracleSaveData.RoomFlag40);
        if (graveyardBoys.Any(npc => npc.Active || npc.Visible))
            throw new InvalidOperationException(
                "Room 0:7b flag $40 did not hide all three completed-event children immediately.");
        save.SetRoomFlag(0, 0x7b, OracleSaveData.RoomFlag40, value: false);
        if (graveyardBoys.Any(npc => !npc.Active || !npc.Visible))
            throw new InvalidOperationException(
                "Clearing room 0:7b flag $40 did not restore its room-placed children.");
        graveyardBoys[0].SetActive(false);
        save.SetRoomFlag(0, 0x7b, OracleSaveData.RoomFlag40);
        save.SetRoomFlag(0, 0x7b, OracleSaveData.RoomFlag40, value: false);
        if (graveyardBoys[0].Active || graveyardBoys[0].Visible)
            throw new InvalidOperationException(
                "A live flag refresh revived an NPC already retired by its interaction lifecycle.");

        manager.LoadRoom(0, _world.LoadRoom(0, 0x82));
        List<NpcCharacter> forestFairies = manager.Entities<NpcCharacter>().Where(npc =>
            npc.Record.Id == 0x49 && npc.Record.SubId == 0x0a).ToList();
        if (forestFairies.Count != 2 || forestFairies.Any(npc => npc.Active))
            throw new InvalidOperationException(
                "Room 0:82's $49:$0a fairies ignored their initial compound flag gate.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlagWonFairyHidingGame);
        save.SetGlobalFlag(OracleSaveData.GlobalFlagForestUnscrambled);
        if (forestFairies.Any(npc => npc.Active))
            throw new InvalidOperationException(
                "Room 0:82's fairies appeared before specific room 0:90 flag $40 was set.");
        save.SetRoomFlag(0, 0x90, OracleSaveData.RoomFlag40);
        if (forestFairies.Any(npc => !npc.Active))
            throw new InvalidOperationException(
                "The global-and-specific-room predicate did not reveal room 0:82's fairies.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame);
        if (forestFairies.Any(npc => npc.Active))
            throw new InvalidOperationException(
                "GLOBALFLAG_FINISHEDGAME $14 did not hide the pre-ending forest fairies.");

        manager.LoadRoom(2, _world.LoadRoom(2, 0xe7));
        NpcCharacter? dog = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x54 && npc.Record.SubId == 0x00);
        if (dog is null || !dog.Active)
            throw new InvalidOperationException("Room 2:e7's Mamamu dog did not satisfy its room-item alternative.");
        save.SetRoomFlag(2, 0xe7, OracleSaveData.RoomFlagItem);
        if (dog.Active)
            throw new InvalidOperationException(
                "Mamamu's dog remained visible after every initialization alternative failed.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlagReturnedDog);
        if (!dog.Active)
            throw new InvalidOperationException(
                "GLOBALFLAG_RETURNED_DOG $3b did not satisfy Mamamu's alternative visibility branch.");

        void SetTreasure(int treasure, bool value)
        {
            int address = 0xc69a + treasure / 8;
            byte mask = (byte)(1 << (treasure & 7));
            byte current = save.ReadWramByte(address);
            byte next = value ? (byte)(current | mask) : (byte)(current & ~mask);
            if (save.WriteWramByte(address, next))
                save.CommitInventoryChange();
        }

        // Restore a coherent immediate-post-intro state before checking all
        // placed members of the Impa/Nayru/Zelda story-state family.
        save.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame, value: false);
        save.SetGlobalFlag(OracleSaveData.GlobalFlagSavedNayru, value: false);
        save.SetGlobalFlag(OracleSaveData.GlobalFlagPreBlackTowerCutsceneDone, value: false);
        save.SetGlobalFlag(OracleSaveData.GlobalFlagGotRingFromZelda, value: false);
        save.SetGlobalFlag(OracleSaveData.GlobalFlagFlameOfDespairLit, value: false);
        save.SetGlobalFlag(OracleSaveData.GlobalFlagReturnedDog, value: false);
        save.SetGlobalFlag(OracleSaveData.GlobalFlagIntroDone);
        save.SetRoomFlag(0, 0x83, OracleSaveData.RoomFlag80, value: false);
        save.SetRoomFlag(0, 0xe7, OracleSaveData.RoomFlag80, value: false);
        SetTreasure(TreasureDatabase.TreasureHarp, value: false);
        SetTreasure(TreasureDatabase.TreasureMakuSeed, value: false);
        if (save.WriteWramByte(0xc612, 0))
            save.CommitInventoryChange();
        if (save.WriteWramByte(0xc6bf, 0))
            save.CommitInventoryChange();

        manager.RuntimeState.SetWramByte(
            OracleRuntimeState.MamamuDogLocationAddress, 0x03);
        manager.LoadRoom(0, _world.LoadRoom(0, 0x48));
        NpcCharacter? roamingDog = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x54 && npc.Record.SubId == 0x01 && npc.Record.Var03 == 0x03);
        if (roamingDog is not { Active: false })
            throw new InvalidOperationException(
                "Room 0:48's roaming dog appeared before Mamamu's search began.");
        save.SetRoomFlag(0, 0xe7, OracleSaveData.RoomFlag80);
        if (!roamingDog.Active)
            throw new InvalidOperationException(
                "Room 0:e7 flag $80 did not reveal location-$03 Mamamu dog in room 0:48.");
        manager.RuntimeState.SetWramByte(
            OracleRuntimeState.MamamuDogLocationAddress, 0x01);
        if (roamingDog.Active)
            throw new InvalidOperationException(
                "Changing wMamamuDogLocation away from $03 did not hide room 0:48's dog.");

        manager.LoadRoom(0, _world.LoadRoom(0, 0x55));
        NpcCharacter? relocatedDog = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x54 && npc.Record.SubId == 0x01 && npc.Record.Var03 == 0x01);
        if (relocatedDog is not { Active: true })
            throw new InvalidOperationException(
                "wMamamuDogLocation $01 did not select the roaming dog in room 0:55.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlagReturnedDog);
        if (relocatedDog.Active)
            throw new InvalidOperationException(
                "GLOBALFLAG_RETURNED_DOG $3b did not remove the selected roaming dog.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlagReturnedDog, value: false);
        save.SetRoomFlag(0, 0xe7, OracleSaveData.RoomFlag80, value: false);
        manager.RuntimeState.SetWramByte(
            OracleRuntimeState.MamamuDogLocationAddress, 0x00);

        manager.LoadRoom(0, _world.LoadRoom(0, 0x57));
        NpcCharacter? earlyLynnaOldMan = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x41 && npc.Record.SubId == 0x01);
        if (earlyLynnaOldMan is not { Active: true } ||
            earlyLynnaOldMan.TextId != 0x2600 || !CanTalkTo(earlyLynnaOldMan))
            throw new InvalidOperationException(
                "Room 0:57 did not load its talkable state-$00 old man with TX_2600.");
        if (save.WriteWramByte(0xc6bf, 0x04))
            save.CommitInventoryChange();
        if (earlyLynnaOldMan.Active)
            throw new InvalidOperationException(
                "Room 0:57's $41:$01 old man remained after getGameProgress_1 state $00.");
        if (save.WriteWramByte(0xc6bf, 0))
            save.CommitInventoryChange();

        manager.LoadRoom(0, _world.LoadRoom(0, 0x58));
        NpcCharacter? rollingRidgeMan = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x41 && npc.Record.SubId == 0x04);
        if (rollingRidgeMan is not { Active: false })
            throw new InvalidOperationException(
                "Room 0:58's $41:$04 man appeared before getGameProgress_1 state $03.");
        if (save.WriteWramByte(0xc6bf, 0x40))
            save.CommitInventoryChange();
        if (!rollingRidgeMan.Active || rollingRidgeMan.TextId != 0x2603 ||
            !CanTalkTo(rollingRidgeMan))
            throw new InvalidOperationException(
                "Beating D7 did not reveal room 0:58's talkable TX_2603 state-$03 man.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlagSawTwinrovaBeforeEndgame);
        if (rollingRidgeMan.Active)
            throw new InvalidOperationException(
                "The Maku-seed/Twinrova phase did not retire room 0:58's state-$03 man.");
        save.SetGlobalFlag(
            OracleSaveData.GlobalFlagSawTwinrovaBeforeEndgame, value: false);
        if (!rollingRidgeMan.Active)
            throw new InvalidOperationException(
                "Clearing the later-phase flag did not restore room 0:58's D7-phase man.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame);
        if (rollingRidgeMan.Active)
            throw new InvalidOperationException(
                "Finished-game state did not retire room 0:58's state-$03 man.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame, value: false);
        if (save.WriteWramByte(0xc6bf, 0))
            save.CommitInventoryChange();

        manager.LoadRoom(0, _world.LoadRoom(0, 0x65));
        List<NpcCharacter> kidnappedZeldaActors = manager.Entities<NpcCharacter>().Where(npc =>
            (npc.Record.Id == 0x31 && npc.Record.SubId == 0x07) ||
            (npc.Record.Id == 0x4c && npc.Record.SubId == 0x04)).ToList();
        if (kidnappedZeldaActors.Count != 2 || kidnappedZeldaActors.Any(npc => npc.Active))
            throw new InvalidOperationException(
                "Room 0:65's linked kidnapped-Zelda Impa and bird appeared immediately after the intro.");

        manager.LoadRoom(0, _world.LoadRoom(0, 0x68));
        NpcCharacter? earlyLynnaMan = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x44 && npc.Record.SubId == 0x02);
        NpcCharacter? lateLynnaWoman = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x3b && npc.Record.SubId == 0x02);
        NpcCharacter? makuSeedVillager = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x3a && npc.Record.SubId == 0x05);
        NpcCharacter? seedSatchelBoy = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x3c && npc.Record.SubId == 0x02);
        if (earlyLynnaMan is not { Active: true } ||
            earlyLynnaMan.TextId != 0x1610 ||
            lateLynnaWoman is not { Active: false } ||
            makuSeedVillager is not { Active: false } ||
            seedSatchelBoy is not { Active: false })
            throw new InvalidOperationException(
                "Room 0:68 did not select its talkable TX_1610 pre-D3 man after the intro.");

        bool CanTalkTo(NpcCharacter npc)
        {
            _player.WarpTo(npc.Position + Vector2.Down * 16.0f);
            _player.Face(Vector2I.Up);
            return manager.FindTalkTarget(_player) == npc;
        }
        if (!CanTalkTo(earlyLynnaMan))
            throw new InvalidOperationException(
                "Room 0:68's $44:$02 man was visible with TX_1610 but not talkable.");

        if (save.WriteWramByte(0xc6bf, 0x04))
            save.CommitInventoryChange();
        if (!earlyLynnaMan.Active || earlyLynnaMan.TextId != 0x1611 ||
            !CanTalkTo(earlyLynnaMan))
            throw new InvalidOperationException(
                "Room 0:68's $44:$02 man did not switch live to D3 dialogue TX_1611.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlagSavedNayru);
        if (!earlyLynnaMan.Active || earlyLynnaMan.TextId != 0x1612 ||
            !CanTalkTo(earlyLynnaMan))
            throw new InvalidOperationException(
                "Room 0:68's $44:$02 man did not switch live to saved-Nayru dialogue TX_1612.");

        if (save.WriteWramByte(0xc6bf, 0x40))
            save.CommitInventoryChange();
        if (earlyLynnaMan.Active || !lateLynnaWoman.Active ||
            lateLynnaWoman.TextId != 0x1523 ||
            makuSeedVillager.Active || !seedSatchelBoy.Active ||
            seedSatchelBoy.TextId != 0x2503 ||
            !CanTalkTo(lateLynnaWoman) || !CanTalkTo(seedSatchelBoy))
            throw new InvalidOperationException(
                "Room 0:68 did not switch to talkable TX_1523/TX_2503 actors in state $03.");

        save.SetGlobalFlag(OracleSaveData.GlobalFlagSawTwinrovaBeforeEndgame);
        if (earlyLynnaMan.Active || !lateLynnaWoman.Active ||
            lateLynnaWoman.TextId != 0x1524 ||
            !makuSeedVillager.Active || makuSeedVillager.TextId != 0x1434 ||
            !seedSatchelBoy.Active || seedSatchelBoy.TextId != 0x2504 ||
            !CanTalkTo(makuSeedVillager))
            throw new InvalidOperationException(
                "Room 0:68 did not select its state-$04 dialogue and talkable villager cast.");

        save.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame);
        if (earlyLynnaMan.Active || !lateLynnaWoman.Active ||
            lateLynnaWoman.TextId != 0x1525 ||
            makuSeedVillager.Active || !seedSatchelBoy.Active ||
            seedSatchelBoy.TextId != 0x2505)
            throw new InvalidOperationException(
                "Room 0:68 did not select finished-game dialogue and retire its state-$04 villager.");

        save.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame, value: false);
        save.SetGlobalFlag(
            OracleSaveData.GlobalFlagSawTwinrovaBeforeEndgame, value: false);
        save.SetGlobalFlag(OracleSaveData.GlobalFlagSavedNayru, value: false);
        if (save.WriteWramByte(0xc6bf, 0))
            save.CommitInventoryChange();

        manager.LoadRoom(0, _world.LoadRoom(0, 0x78));
        NpcCharacter? clockSecretLady = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x3d && npc.Record.SubId == 0x04);
        if (clockSecretLady is not { Active: false } ||
            clockSecretLady.TextId != 0x4d00)
            throw new InvalidOperationException(
                "Room 0:78's TX_4d00 linked-secret old lady appeared in an unlinked game.");
        if (save.WriteWramByte(0xc6bf, 0x08))
            save.CommitInventoryChange();
        if (clockSecretLady.Active)
            throw new InvalidOperationException(
                "Room 0:78's old lady ignored the linked-game requirement after D4.");
        if (save.WriteWramByte(0xc612, 1))
            save.CommitInventoryChange();
        if (!clockSecretLady.Active || !CanTalkTo(clockSecretLady))
            throw new InvalidOperationException(
                "Linked-game plus D4 state did not reveal room 0:78's talkable TX_4d00 old lady.");

        if (save.WriteWramByte(0xc6bf, 0x02))
            save.CommitInventoryChange();
        manager.LoadRoom(3, _world.LoadRoom(3, 0xf8));
        NpcCharacter? ruulSecretLady = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x3d && npc.Record.SubId == 0x05);
        if (ruulSecretLady is not { Active: true } ||
            ruulSecretLady.TextId != 0x4d2d || !CanTalkTo(ruulSecretLady))
            throw new InvalidOperationException(
                "Linked-game plus D2 state did not reveal the paired talkable TX_4d2d old lady.");
        if (save.WriteWramByte(0xc612, 0))
            save.CommitInventoryChange();
        if (save.WriteWramByte(0xc6bf, 0))
            save.CommitInventoryChange();

        manager.LoadRoom(0, _world.LoadRoom(0, 0x25));
        List<NpcCharacter> bridgeCarpenters = manager.Entities<NpcCharacter>().Where(npc =>
            npc.Record.Id == 0x9a).ToList();
        if (bridgeCarpenters.Count != 5 || bridgeCarpenters.Any(npc => !npc.Active))
            throw new InvalidOperationException(
                "Room 0:25 did not begin with its five unlinked pre-bridge carpenters.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlagSymmetryBridgeBuilt);
        if (bridgeCarpenters.Any(npc => npc.Active))
            throw new InvalidOperationException(
                "GLOBALFLAG_SYMMETRY_BRIDGE_BUILT $25 did not hide room 0:25's carpenters.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlagSymmetryBridgeBuilt, value: false);

        manager.LoadRoom(0, _world.LoadRoom(0, 0xaa));
        NpcCharacter? dimitriTokay = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x48 && npc.Record.SubId == 0x10);
        if (dimitriTokay is not { Active: false })
            throw new InvalidOperationException(
                "Room 0:aa's Dimitri-event Tokay appeared before D3.");

        manager.LoadRoom(1, _world.LoadRoom(1, 0x46));
        List<NpcCharacter> palaceActors = manager.Entities<NpcCharacter>().Where(npc =>
            (npc.Record.Id == 0x37 && npc.Record.SubId == 0x09) ||
            (npc.Record.Id == 0x40 && npc.Record.SubId == 0x0b)).ToList();
        if (palaceActors.Count != 2 || palaceActors.Any(npc => npc.Active))
            throw new InvalidOperationException(
                "Room 1:46's essence/Mystery-Seed-gated Ralph and soldier appeared immediately after the intro.");

        manager.LoadRoom(1, _world.LoadRoom(1, 0x65));
        List<NpcCharacter> linkedFinaleActors = manager.Entities<NpcCharacter>().Where(npc =>
            (npc.Record.Id == 0x4d && npc.Record.SubId == 0x0a) ||
            (npc.Record.Id == 0x37 && npc.Record.SubId == 0x12)).ToList();
        if (linkedFinaleActors.Count != 2 || linkedFinaleActors.Any(npc => npc.Active))
            throw new InvalidOperationException(
                "Room 1:65's linked finale Ambi and Ralph appeared in a standard post-intro game.");

        manager.LoadRoom(1, _world.LoadRoom(1, 0x68));
        NpcCharacter? linkedSubrosian = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x4e && npc.Record.SubId == 0x00);
        if (linkedSubrosian is not { Active: false })
            throw new InvalidOperationException(
                "Room 1:68's late linked-game Subrosian appeared immediately after the intro.");

        manager.LoadRoom(1, _world.LoadRoom(1, 0x75));
        List<NpcCharacter> linkedEndingActors = manager.Entities<NpcCharacter>().Where(npc =>
            (npc.Record.Id == 0x37 && npc.Record.SubId == 0x0a) ||
            (npc.Record.Id == 0x31 && npc.Record.SubId is 0x04 or 0x05) ||
            (npc.Record.Id == 0x36 && npc.Record.SubId == 0x0a) ||
            (npc.Record.Id == 0xad && npc.Record.SubId == 0x04)).ToList();
        if (linkedEndingActors.Count != 5 || linkedEndingActors.Any(npc => npc.Active))
            throw new InvalidOperationException(
                "Room 1:75's pre-tower/linked Impa, Ralph, Nayru, and Zelda variants appeared immediately after the intro.");

        manager.LoadRoom(1, _world.LoadRoom(1, 0x47));
        List<NpcCharacter> heritageActors = manager.Entities<NpcCharacter>().Where(npc =>
            (npc.Record.Id == 0x4f && npc.Record.SubId == 0x01) ||
            (npc.Record.Id == 0xad && npc.Record.SubId == 0x08)).ToList();
        if (heritageActors.Count != 2 || heritageActors.Any(npc => npc.Active))
            throw new InvalidOperationException(
                "Room 1:47's late-story Impa and linked Zelda variants appeared immediately after the intro.");

        manager.LoadRoom(1, _world.LoadRoom(1, 0x58));
        List<NpcCharacter> flameActors = manager.Entities<NpcCharacter>().Where(npc =>
            (npc.Record.Id == 0x4f && npc.Record.SubId == 0x02) ||
            (npc.Record.Id == 0x36 && npc.Record.SubId == 0x0d)).ToList();
        if (flameActors.Count != 2 || flameActors.Any(npc => npc.Active))
            throw new InvalidOperationException(
                "Room 1:58's flame-of-despair Impa and Nayru variants appeared immediately after the intro.");

        manager.LoadRoom(1, _world.LoadRoom(1, 0xcb));
        NpcCharacter? rosa = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x68 && npc.Record.SubId == 0x00);
        if (rosa is not { Active: false })
            throw new InvalidOperationException(
                "Room 1:cb's linked pre-D3 Rosa appeared immediately after the intro.");

        manager.LoadRoom(2, _world.LoadRoom(2, 0xa0));
        NpcCharacter? d7Zora = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0xab && npc.Record.SubId == 0x10);
        if (d7Zora is not { Active: false })
            throw new InvalidOperationException(
                "Room 2:a0's D7-gated Zora appeared immediately after the intro.");

        manager.LoadRoom(2, _world.LoadRoom(2, 0xd7));
        NpcCharacter? linkedZora = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0xab && npc.Record.SubId == 0x12);
        if (linkedZora is not { Active: false })
            throw new InvalidOperationException(
                "Room 2:d7's linked-game Zora appeared in a standard post-intro game.");

        manager.LoadRoom(3, _world.LoadRoom(3, 0x9e));
        List<NpcCharacter> nayruHouseActors = manager.Entities<NpcCharacter>().Where(npc =>
            npc.Record.Id is 0x36 or 0x4f or 0xad).ToList();
        List<NpcCharacter> activeHouseActors = nayruHouseActors.Where(npc => npc.Active).ToList();
        if (nayruHouseActors.Count != 11 || activeHouseActors.Count != 1 ||
            activeHouseActors[0].Record.Id != 0x4f ||
            activeHouseActors[0].Record.SubId != 0x00 ||
            activeHouseActors[0].Record.Var03 != 0x00 ||
            activeHouseActors[0].Position != new Vector2(0x38, 0x38) ||
            activeHouseActors[0].TextId != 0x0120 ||
            string.IsNullOrEmpty(activeHouseActors[0].Message))
        {
            throw new InvalidOperationException(
                "Immediate-post-intro room 3:9e did not contain only talkable Impa $4f:$00 state $00 at $38,$38.");
        }

        save.SetRoomFlag(0, 0x83, OracleSaveData.RoomFlag80);
        NpcCharacter? passageImpa = nayruHouseActors.Find(npc =>
            npc.Record.Id == 0x4f && npc.Record.Var03 == 0x01);
        if (passageImpa is not { Active: true } ||
            nayruHouseActors.Count(npc => npc.Active) != 1 ||
            passageImpa.Position != new Vector2(0x28, 0x48) ||
            passageImpa.TextId != 0x0121)
        {
            throw new InvalidOperationException(
                "Opening D2's passage did not live-swap Nayru's-house Impa to state $01 and TX_0121.");
        }

        SetTreasure(TreasureDatabase.TreasureHarp, value: true);
        NpcCharacter? harpImpa = nayruHouseActors.Find(npc =>
            npc.Record.Id == 0x4f && npc.Record.Var03 == 0x02);
        if (harpImpa is not { Active: true } || nayruHouseActors.Count(npc => npc.Active) != 1 ||
            harpImpa.Position != new Vector2(0x68, 0x28) || harpImpa.TextId != 0x0122)
        {
            throw new InvalidOperationException(
                "Obtaining the harp did not live-swap Nayru's-house Impa to state $02 and TX_0122.");
        }

        if (save.WriteWramByte(0xc612, 1))
            save.CommitInventoryChange();
        NpcCharacter? linkedD3Impa = nayruHouseActors.Find(npc =>
            npc.Record.Id == 0x4f && npc.Record.Var03 == 0x0b);
        if (linkedD3Impa is not { Active: true } || nayruHouseActors.Count(npc => npc.Active) != 1)
            throw new InvalidOperationException(
                "Linked-game state did not select Nayru's-house Impa behavior $0b before D3.");
        if (save.WriteWramByte(0xc6bf, 0x04))
            save.CommitInventoryChange();
        if (nayruHouseActors.Any(npc => npc.Active))
            throw new InvalidOperationException(
                "D3 essence bit 2 did not delete linked Nayru's-house Impa state $0c.");

        if (save.WriteWramByte(0xc612, 0))
            save.CommitInventoryChange();
        if (save.WriteWramByte(0xc6bf, 0))
            save.CommitInventoryChange();
        save.SetGlobalFlag(OracleSaveData.GlobalFlagSavedNayru);
        NpcCharacter? houseNayru = nayruHouseActors.Find(npc =>
            npc.Record.Id == 0x36 && npc.Record.SubId == 0x0b);
        if (houseNayru is not { Active: true })
            throw new InvalidOperationException(
                "GLOBALFLAG_SAVED_NAYRU $11 did not reveal room 3:9e's pre-Maku-seed Nayru.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlagGotRingFromZelda);
        NpcCharacter? houseZelda = nayruHouseActors.Find(npc =>
            npc.Record.Id == 0xad && npc.Record.SubId == 0x07);
        if (houseZelda is not { Active: true })
            throw new InvalidOperationException(
                "GLOBALFLAG_GOT_RING_FROM_ZELDA $38 did not reveal room 3:9e's pre-Maku-seed Zelda.");
        SetTreasure(TreasureDatabase.TreasureMakuSeed, value: true);
        if (houseNayru.Active || houseZelda.Active)
            throw new InvalidOperationException(
                "Obtaining the Maku Seed did not remove Nayru and Zelda from room 3:9e.");

        manager.Clear();
        RemoveChild(validationRoot);
        validationRoot.QueueFree();
        GD.Print("Validated room 0:5a's TX_5700/TX_5701 intro monkeys, opposing $06/$07 " +
            "$20-frame animation loops, rooms 2:ea/2:eb's 72-record family spawner, " +
            "Bipin $28:$00's SPEED_100 X=$28/$58 patrol, $04/$05 animation reversal, " +
            "and moving objectPreventLinkFromPassing collision, " +
            "303 visibility and 54 dialogue predicates, roaming-dog " +
            "location selection, rooms 0:68/0:78's phased and linked talkable cast, " +
            "room 3:9e's post-intro Impa, var03 selection, compound and alternative gates, " +
            "live refresh, and lifecycle-safe hiding.");
    }

    private void ValidateBipinBlossomNaming()
    {
        Span<byte> emptyName = stackalloc byte[6];
        emptyName.Clear();
        bool changed = _saveData.WriteWramBytes(
            OracleSaveData.ChildNameAddress, emptyName);
        changed |= _saveData.WriteWramByte(OracleSaveData.ChildStatusAddress, 0x00);
        changed |= _saveData.WriteWramByte(OracleSaveData.ChildStageAddress, 0x00);
        changed |= _saveData.WriteWramByte(OracleSaveData.NextChildStageAddress, 0x00);
        changed |= _saveData.WriteWramByte(OracleSaveData.ChildFlagsAddress, 0x00);
        changed |= _saveData.WriteWramByte(OracleSaveData.ChildPersonalityAddress, 0x00);
        if (changed)
            _saveData.CommitInventoryChange();
        _saveData.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame, value: false);
        _entities.RuntimeState.SetWramByte(
            OracleRuntimeState.SeedTreeRefilledBitsetAddress, 0x00);

        LoadValidationRoom(2, 0xea);
        NpcCharacter? blossom = _entities.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x2b && npc.Record.SubId == 0x00);
        if (blossom is null)
            throw new InvalidOperationException(
                "Room 2:ea did not provide Blossom $2b:$00 for child-name validation.");

        _player.WarpTo(blossom.Position + Vector2.Down * 16.0f);
        _player.Face(Vector2I.Up);
        if (!_interactions.TryInteract(_player) ||
            !_dialogue.CurrentMessage.Contains("would you call", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Blossom $2b:$00 did not begin TX_4400's child-naming interaction.");
        }
        _dialogue.Close();
        _interactions.UpdateFamilyNamingForValidation(0.0);
        if (!_interactions.GameplayMenuActive ||
            _interactions.KidNameScreenForValidation is not { EnteredName.Length: 0 })
        {
            throw new InvalidOperationException(
                "Closing TX_4400 did not open MENU_KIDNAME $07 with an empty five-character field.");
        }

        _interactions.CommitKidNameForValidation(string.Empty);
        _interactions.UpdateFamilyNamingForValidation(0.0);
        if (!_dialogue.CurrentMessage.Contains("more thought", StringComparison.Ordinal) ||
            _saveData.ChildNamed)
        {
            throw new InvalidOperationException(
                "An empty child name did not show TX_440a without advancing family state.");
        }
        _dialogue.Close();
        _interactions.UpdateFamilyNamingForValidation(0.0);
        if (_interactions.FamilyNamingActive)
            throw new InvalidOperationException(
                "Blossom's empty-name response did not return to her ordinary talk loop.");

        if (!_interactions.TryInteract(_player))
            throw new InvalidOperationException(
                "Blossom could not restart child naming after an empty name.");
        _dialogue.Close();
        _interactions.UpdateFamilyNamingForValidation(0.0);
        _interactions.CommitKidNameForValidation("Pip");
        _interactions.UpdateFamilyNamingForValidation(0.0);
        if (!_dialogue.ChoiceActive ||
            !_dialogue.CurrentMessage.Contains("Pip", StringComparison.Ordinal) ||
            !_dialogue.CurrentMessage.Contains("Yes", StringComparison.Ordinal) ||
            !_dialogue.CurrentMessage.Contains("No", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "MENU_KIDNAME did not pass the candidate name into TX_4407's Yes/No confirmation.");
        }

        _dialogue.SubmitChoiceForValidation(1);
        _interactions.UpdateFamilyNamingForValidation(0.0);
        if (!_interactions.GameplayMenuActive ||
            _interactions.KidNameScreenForValidation?.EnteredName != "Pip" ||
            _saveData.ChildNamed)
        {
            throw new InvalidOperationException(
                "Choosing No in TX_4407 did not reopen MENU_KIDNAME with the candidate preserved.");
        }
        _interactions.CommitKidNameForValidation("Pip");
        _interactions.UpdateFamilyNamingForValidation(0.0);
        _dialogue.SubmitChoiceForValidation(0);
        _interactions.UpdateFamilyNamingForValidation(0.0);

        // blossom_decideInitialChildStatus sums the encoded name's low
        // nibbles: P=$0, i=$9, p=$0, so Pip selects status $01.
        if (_saveData.ChildName != "Pip" || !_saveData.ChildNamed ||
            _saveData.ReadWramByte(OracleSaveData.ChildStatusAddress) != 0x01 ||
            _saveData.ReadWramByte(OracleSaveData.ChildStageAddress) != 0x00 ||
            _saveData.ReadWramByte(OracleSaveData.NextChildStageAddress) != 0x01)
        {
            throw new InvalidOperationException(
                "Confirming Pip did not reproduce wKidName/wChildStatus/wc6e2/wNextChildStage writes.");
        }

        NpcCharacter? bipin = _entities.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x28 && npc.Record.SubId == 0x00);
        if (bipin is not { TextId: 0x4301 } || blossom.TextId != 0x4409 ||
            !bipin.Message.Contains("Pip", StringComparison.Ordinal) ||
            !blossom.Message.Contains("Pip", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Naming the child did not live-switch Bipin/Blossom to TX_4301/TX_4409 with \\Child expanded.");
        }

        for (int frame = 0; frame < 29; frame++)
            _interactions.UpdateFamilyNamingForValidation(1.0 / 60.0);
        if (_dialogue.IsOpen)
            throw new InvalidOperationException(
                "Blossom showed TX_4408 before the original 30-update delay elapsed.");
        _interactions.UpdateFamilyNamingForValidation(1.0 / 60.0);
        if (!_dialogue.CurrentMessage.Contains("It's a fine", StringComparison.Ordinal) ||
            !_dialogue.CurrentMessage.Contains("Come visit us", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Blossom did not show TX_4408 after the original 30-update delay.");
        }
        _dialogue.Close();
        _interactions.UpdateFamilyNamingForValidation(0.0);
        if (_interactions.FamilyNamingActive)
            throw new InvalidOperationException(
                "Blossom's child-naming interaction did not finish after TX_4408 closed.");

        LoadValidationRoom(2, 0xea);
        blossom = _entities.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x2b && npc.Record.SubId == 0x00);
        bipin = _entities.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x28 && npc.Record.SubId == 0x00);
        if (blossom is not { TextId: 0x4409 } || bipin is not { TextId: 0x4301 } ||
            !blossom.Message.Contains("Pip", StringComparison.Ordinal) ||
            !bipin.Message.Contains("Pip", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Reloading room 2:ea lost the named stage-$00 family dialogue state.");
        }

        changed = _saveData.WriteWramBytes(OracleSaveData.ChildNameAddress, emptyName);
        changed |= _saveData.WriteWramByte(OracleSaveData.ChildStatusAddress, 0x00);
        changed |= _saveData.WriteWramByte(OracleSaveData.ChildStageAddress, 0x00);
        changed |= _saveData.WriteWramByte(OracleSaveData.NextChildStageAddress, 0x00);
        changed |= _saveData.WriteWramByte(OracleSaveData.ChildFlagsAddress, 0x00);
        changed |= _saveData.WriteWramByte(OracleSaveData.ChildPersonalityAddress, 0x00);
        if (changed)
            _saveData.CommitInventoryChange();

        GD.Print("Validated Bipin/Blossom stage-$00 movement and MENU_KIDNAME $07: empty-name " +
            "handling, No/re-edit, Yes confirmation, original child-status/state writes, " +
            "30-update TX_4408 delay, and persistent TX_4301/TX_4409 \\Child dialogue.");
    }

    private void ValidateImpaIntroEncounter()
    {
        ImpaIntroEvent impaEvent = _roomEvents.Impa;
        NayruIntroEvent nayruIntro = _roomEvents.Nayru;
        var encounterTrace = new ValidationCutsceneTrace();
        _roomEvents.CommandTraceSink = encounterTrace;
        _saveData.SetGlobalFlag(OracleSaveData.GlobalFlagPregameIntroDone);
        _saveData.SetGlobalFlag(OracleSaveData.GlobalFlagIntroDone, value: false);
        _sound.PlaySound(OracleSoundEngine.SndCtrlStopMusic);
        _sound.ClearPlayRequestAudit();
        _saveData.SetRoomFlag(0, 0x7a, OracleSaveData.RoomFlag40, value: false);
        _saveData.SetRoomFlag(0, 0x6a, OracleSaveData.RoomFlag40, value: false);
        LoadValidationRoom(0, 0x7a);
        if (_sound.ActiveMusic != 0)
            throw new InvalidOperationException(
                "The playable intro did not suppress ordinary room music before meeting Impa.");
        _player.WarpTo(new Vector2(0x38, 0x07));
        _player.Face(Vector2I.Up);
        if (!impaEvent.HelpWaitingAtEdge || _roomEvents.Active)
            throw new InvalidOperationException("Room 0:7a did not arm INTERAC_MISCELLANEOUS_1 $6b:$00.");
        impaEvent.UpdateHelpFrame(upPressed: true);
        if (_dialogue.IsOpen)
            throw new InvalidOperationException("Impa's help text triggered before Link's Y coordinate was below $07.");

        _player.WarpTo(new Vector2(0x38, 0x06));
        impaEvent.UpdateHelpFrame(upPressed: true);
        if (!_dialogue.IsOpen || _dialogue.CurrentMessage != "HELLLLP!!!" ||
            _dialogue.Position.Y != 80 || !_player.CutsceneControlled ||
            impaEvent.Counter != 30 ||
            _saveData.HasRoomFlag(0, 0x7a, OracleSaveData.RoomFlag40))
        {
            throw new InvalidOperationException(
                "Room 0:7a did not show fixed-bottom TX_0100 and install its 30-update counter.");
        }
        _dialogue.Close();
        StepRoomEventFrames(29);
        if (impaEvent.Counter != 1 ||
            _saveData.HasRoomFlag(0, 0x7a, OracleSaveData.RoomFlag40))
        {
            throw new InvalidOperationException(
                "INTERAC_MISCELLANEOUS_1 $6b:$00 ended its post-text counter early.");
        }
        StepRoomEventFrames(1);
        if (impaEvent.Counter != 8 ||
            !_saveData.HasRoomFlag(0, 0x7a, OracleSaveData.RoomFlag40))
        {
            throw new InvalidOperationException(
                "Room 0:7a did not set room flag $40 and install eight BTN_UP updates.");
        }
        StepRoomEventFrames(1);
        if (!_transitions.ScrollActive || _activeGroup != 0 || _currentRoom.Id != 0x6a)
            throw new InvalidOperationException("The simulated Up input did not begin the 0:7a -> 0:6a scroll.");
        int impaScrollFrames = FinishActiveScrollingTransitionWithRoomEventsForValidation();
        if (impaScrollFrames != 32)
            throw new InvalidOperationException(
                $"The 0:7a -> 0:6a vertical scroll took {impaScrollFrames} updates, expected 32.");

        NpcCharacter? impa = impaEvent.Actor;
        System.Collections.Generic.IReadOnlyList<NpcCharacter> octoroks =
            impaEvent.FakeOctoroks;
        Color possessedHighlight = new(0x12 / 31.0f, 0x1a / 31.0f, 0x1f / 31.0f);
        if (impa is null || !_roomEvents.Active || impa.Position != new Vector2(0x48, 0x38) ||
            octoroks.Count != 3 ||
            octoroks[0].Position != new Vector2(0x48, 0x18) ||
            octoroks[1].Position != new Vector2(0x38, 0x38) ||
            octoroks[2].Position != new Vector2(0x58, 0x38) ||
            !impa.CurrentAnimationUsesColor(possessedHighlight))
        {
            throw new InvalidOperationException(
                "Room 0:6a did not create possessed Impa and objectData.impaOctoroks " +
                $"with their original positions and PALH_97 palette 7 (impa={impa?.Position}, " +
                $"active={_roomEvents.Active}, octoroks={octoroks.Count}, " +
                $"positions={string.Join(',', octoroks.Select(actor => actor.Position))}, " +
                $"highlight={impa?.CurrentAnimationUsesColor(possessedHighlight)}).");
        }

        Vector2 linkStart = _player.Position;
        if (linkStart != new Vector2(0x38, 0x76))
            throw new InvalidOperationException($"0:7a -> 0:6a placed Link at {linkStart}, expected $76/$38.");
        if (!_player.CutsceneControlled || impaEvent.Counter != 120 ||
            _sound.ActiveMusic != OracleSoundEngine.MusFairyFountain ||
            _sound.MusicVolume != 3 ||
            _player.Position != linkStart || _player.FacingVector != Vector2I.Up)
        {
            throw new InvalidOperationException(
                "linkCutscene1 state 0 did not install its $78 counter, upward animation, " +
                "and MUS_FAIRY_FOUNTAIN volume-3 override.");
        }
        StepRoomEventFrames(119);
        if (impaEvent.Counter != 1 || _player.Position != linkStart)
            throw new InvalidOperationException("Link's initial 120-update wait ended early in room 0:6a.");
        StepRoomEventFrames(1);
        StepRoomEventFrames(15);
        if (_player.Position != new Vector2(0x47, 0x76) ||
            _player.FacingVector != Vector2I.Right)
        {
            throw new InvalidOperationException(
                "Link did not approach center X=$48 at one pixel per SPEED_100 update.");
        }
        StepRoomEventFrames(1);
        StepRoomEventFrames(1);
        if (impaEvent.Counter != 4 || _player.Position != new Vector2(0x48, 0x76))
            throw new InvalidOperationException("Link did not install the four-update center wait at X=$48.");
        StepRoomEventFrames(3);
        if (impaEvent.Counter != 1 || _player.Position.Y != 0x76)
            throw new InvalidOperationException("Link's four-update center wait ended early.");
        StepRoomEventFrames(1);
        StepRoomEventFrames(45);
        if (impaEvent.Counter != 1 || _player.Position != new Vector2(0x48, 0x49))
            throw new InvalidOperationException("Link's $2e-update upward approach ended early.");
        StepRoomEventFrames(1);
        if (_player.Position != new Vector2(0x48, 0x48) ||
            _sound.LastPlayRequest != OracleSoundEngine.SndClink ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndClink) != 1)
        {
            throw new InvalidOperationException(
                "Link did not finish exactly 46 pixels above his entry point and play SND_CLINK.");
        }

        StepRoomEventFrames(1);
        if (impaEvent.Counter != 0 || impaEvent.EncounterCommandIndex != 1)
            throw new InvalidOperationException(
                "impaScript0 did not preserve checkmemoryeq's successful one-update yield.");
        StepRoomEventFrames(1);
        if (impaEvent.Counter != 210 || impaEvent.EncounterCommandIndex != 1)
            throw new InvalidOperationException(
                "impaScript0 did not install its 210-update wait after the cfd0 gate.");
        Vector2[] fakeStarts =
        {
            new(0x48, 0x18), new(0x38, 0x38), new(0x58, 0x38)
        };
        StepRoomEventFrames(18);
        if (octoroks[0].Position != fakeStarts[0] ||
            octoroks[1].Position != fakeStarts[1] ||
            octoroks[2].Position != fakeStarts[2])
        {
            throw new InvalidOperationException("A fake Octorok left before its original $14 signal wait.");
        }
        StepRoomEventFrames(1);
        StepRoomEventFrames(59);
        if (octoroks[1].Position != fakeStarts[1])
            throw new InvalidOperationException("Fake Octorok var03=$01 moved during its $3c flee delay.");
        StepRoomEventFrames(1);
        if (octoroks[1].Position != fakeStarts[1] ||
            _sound.LastPlayRequest != OracleSoundEngine.SndThrow ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndThrow) != 1)
        {
            throw new InvalidOperationException(
                "Fake Octorok var03=$01 did not play SND_THROW on its stationary substate update.");
        }
        StepRoomEventFrames(1);
        if (octoroks[1].Position != fakeStarts[1] + Vector2.Left * 3)
        {
            throw new InvalidOperationException(
                "Fake Octorok var03=$01 did not flee left at SPEED_300 after $14+$3c updates.");
        }

        StepRoomEventFrames(129);
        if (_dialogue.IsOpen || impaEvent.Counter != 1)
            throw new InvalidOperationException("Impa's 210-update post-signal wait ended early.");
        StepRoomEventFrames(1);
        if (!_dialogue.IsOpen || _dialogue.Position.Y != 8 ||
            !_dialogue.CurrentMessage.StartsWith("That was\nfrightening!") ||
            !_dialogue.CurrentMessage.EndsWith("with you nearby.") ||
            octoroks[0].Active || octoroks[1].Active || octoroks[2].Active ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndThrow) != 3)
        {
            throw new InvalidOperationException(
                "TX_0102, automatic TX_0101 call, textbox placement, fake-Octorok cleanup, " +
                "or the three staggered SND_THROW calls diverged.");
        }

        _dialogue.Close();
        StepRoomEventFrames(1);
        StepRoomEventFrames(29);
        if (impaEvent.Counter != 1 || impa.Position != new Vector2(0x48, 0x38))
            throw new InvalidOperationException("Impa's 30-update post-text wait ended early.");
        StepRoomEventFrames(1);
        if (impaEvent.Counter != 0 || impaEvent.EncounterCommandIndex != 5)
            throw new InvalidOperationException(
                "Impa's post-text wait did not carry through setspeed on its completion update.");
        StepRoomEventFrames(1);
        if (impa.Position != new Vector2(0x48, 0x38) || impaEvent.Counter != 32)
        {
            throw new InvalidOperationException(
                "Impa moved during the setspeed/movedown script-command updates.");
        }
        StepRoomEventFrames(30);
        if (impaEvent.Counter != 2 || impa.Position != new Vector2(0x48, 0x47))
        {
            throw new InvalidOperationException(
                "Impa did not apply SPEED_080 through the high-coordinate byte for 30 updates.");
        }
        StepRoomEventFrames(1);
        if (impaEvent.Counter != 1 || impa.Position != new Vector2(0x48, 0x47))
            throw new InvalidOperationException("movedown $20 did not retain its final half-pixel fraction.");
        StepRoomEventFrames(1);
        if (_saveData.HasRoomFlag(0, 0x6a, OracleSaveData.RoomFlag40))
            throw new InvalidOperationException("Impa set room flag $40 before counter2 reached zero.");
        StepRoomEventFrames(1);
        if (!_roomEvents.Active || impaEvent.Following ||
            !_player.CutsceneControlled || impa.Position == _player.Position ||
            !_saveData.HasRoomFlag(0, 0x6a, OracleSaveData.RoomFlag40))
        {
            throw new InvalidOperationException(
                "scriptCmd_orRoomFlags did not set room flag $40 and yield before scriptend.");
        }
        StepRoomEventFrames(1);
        Vector2 followStart = _player.Position;
        if (_roomEvents.Active || !impaEvent.Following || _player.CutsceneControlled ||
            impa.Position != followStart || impa.FacingVector != Vector2I.Up ||
            !_saveData.HasRoomFlag(0, 0x6a, OracleSaveData.RoomFlag40))
        {
            throw new InvalidOperationException(
                "Impa did not set room flag $40, restore Link, and initialize the follower at Link's position.");
        }

        Vector2I[] impaDirections =
        {
            Vector2I.Up, Vector2I.Right, Vector2I.Down, Vector2I.Left
        };
        var impaAnimationHashes = new HashSet<ulong>();
        foreach (Vector2I direction in impaDirections)
        {
            impa.SetFacingDirection(direction);
            if (impa.CurrentAnimationOpaquePixels == 0 ||
                !impaAnimationHashes.Add(impa.CurrentAnimationPixelHash))
            {
                throw new InvalidOperationException(
                    $"Impa animation ${Array.IndexOf(impaDirections, direction):x2} " +
                    "was empty or reused another directional sprite.");
            }
        }
        impa.SetFacingDirection(Vector2I.Up);

        for (int update = 1; update <= 16; update++)
        {
            _player.WarpTo(followStart + Vector2.Right * update);
            _player.Face(Vector2I.Right);
            StepRoomEventFrames(1);
        }
        if (impa.Position != followStart)
            throw new InvalidOperationException("Impa advanced before the 16-entry Link path delay elapsed.");
        _player.WarpTo(followStart + Vector2.Right * 17);
        StepRoomEventFrames(1);
        if (impa.Position != followStart + Vector2.Right || impa.FacingVector != Vector2I.Right)
        {
            throw new InvalidOperationException(
                "checkUpdateFollowingLinkObject did not replay Link's first delayed position/direction.");
        }

        for (int update = 18; update <= 82; update++)
        {
            _player.WarpTo(followStart + Vector2.Right * update);
            StepRoomEventFrames(1);
        }
        if (_player.Position.X != 0x9a ||
            impa.Position != _player.Position + Vector2.Left * 16)
        {
            throw new InvalidOperationException(
                "Impa's path was not primed at the right screen edge before scrolling.");
        }

        _transitions.BeginScroll(_player, Vector2I.Right, 0x6b);
        NpcCharacter? incomingImpa = impaEvent.Actor;
        if (incomingImpa is null || incomingImpa == impa ||
            incomingImpa.Position != impa.Position + Vector2.Left * 160 ||
            impa.Active ||
            !impaEvent.Following)
        {
            throw new InvalidOperationException(
                "Following Impa was not transferred into room 0:6b with the original " +
                "screen offset and a retired outgoing rendering copy.");
        }
        for (int frame = 0; frame < 40; frame++)
        {
            UpdateScrollingTransition(1.0 / 60.0);
            _roomEvents.Update(1.0 / 60.0);
            Vector2 scrollingLink = _transitions.ScrollLinkPositionInDestination;
            Vector2 expectedImpa = new(
                Mathf.Floor(scrollingLink.X) - 16,
                Mathf.Floor(scrollingLink.Y));
            bool outgoingImpaVisible = _entities.OutgoingEntities<NpcCharacter>().Any(npc =>
                npc.Record.Id == 0x31 && npc.Record.SubId == 0x00 && npc.Active);
            if (_transitions.ScrollActive &&
                (incomingImpa.Position != expectedImpa || outgoingImpaVisible))
            {
                throw new InvalidOperationException(
                    $"Always-update Impa fell behind on right-scroll update {frame + 1}: " +
                    $"expected {expectedImpa}, got {incomingImpa.Position}, " +
                    $"outgoing visible={outgoingImpaVisible}.");
            }
        }
        if (IsTransitioning)
            throw new InvalidOperationException("The Impa right scroll did not finish in 40 updates.");
        if (incomingImpa.Position != _player.Position + Vector2.Left * 16)
        {
            throw new InvalidOperationException(
                "resetFollowingLinkObjectPosition did not place Impa 16 pixels behind " +
                $"Link after the right scroll (Link={_player.Position}, Impa={incomingImpa.Position}).");
        }
        StepRoomEventFrames(1);
        if (incomingImpa.Position != _player.Position + Vector2.Left * 16 ||
            incomingImpa.FacingVector != Vector2I.Right)
        {
            throw new InvalidOperationException(
                "The rebuilt path did not retain Impa at the left edge facing right on its first update.");
        }

        _player.Face(Vector2I.Left);
        for (int x = 0x16; x >= 0x06; x--)
        {
            _player.WarpTo(new Vector2(x, _player.Position.Y));
            StepRoomEventFrames(1);
        }
        if (incomingImpa.Position != _player.Position + Vector2.Right * 16 ||
            incomingImpa.FacingVector != Vector2I.Left)
        {
            throw new InvalidOperationException(
                "Impa's path was not primed at the left screen edge before scrolling.");
        }

        _transitions.BeginScroll(_player, Vector2I.Left, 0x6a);
        List<NpcCharacter> returningImpas = _npcNodes.Where(npc =>
            npc.Record.Id == 0x31 && npc.Record.SubId == 0x00).ToList();
        NpcCharacter? returningFollower = impaEvent.Actor;
        if (returningImpas.Count != 2 || returningFollower is null ||
            incomingImpa.Active ||
            !returningFollower.Active || returningImpas.Count(npc => npc.Active) != 1 ||
            !impaEvent.Following)
        {
            throw new InvalidOperationException(
                "Returning to room 0:6a retained both the completed placed Impa and her follower.");
        }
        for (int frame = 0; frame < 40; frame++)
        {
            UpdateScrollingTransition(1.0 / 60.0);
            _roomEvents.Update(1.0 / 60.0);
            Vector2 scrollingLink = _transitions.ScrollLinkPositionInDestination;
            Vector2 expectedImpa = new(
                Mathf.Floor(scrollingLink.X) + 16,
                Mathf.Floor(scrollingLink.Y));
            bool outgoingImpaVisible = _entities.OutgoingEntities<NpcCharacter>().Any(npc =>
                npc.Record.Id == 0x31 && npc.Record.SubId == 0x00 && npc.Active);
            if (_transitions.ScrollActive &&
                (returningFollower.Position != expectedImpa || outgoingImpaVisible))
            {
                throw new InvalidOperationException(
                    $"Always-update Impa fell behind on left-scroll update {frame + 1}: " +
                    $"expected {expectedImpa}, got {returningFollower.Position}, " +
                    $"outgoing visible={outgoingImpaVisible}.");
            }
        }
        if (IsTransitioning)
            throw new InvalidOperationException("The Impa left scroll did not finish in 40 updates.");
        if (returningFollower.Position != _player.Position + Vector2.Right * 16)
        {
            throw new InvalidOperationException(
                "resetFollowingLinkObjectPosition did not place Impa 16 pixels behind " +
                $"Link after the left scroll (Link={_player.Position}, Impa={returningFollower.Position}).");
        }
        StepRoomEventFrames(1);
        if (returningFollower.Position != _player.Position + Vector2.Right * 16 ||
            returningFollower.FacingVector != Vector2I.Left)
        {
            throw new InvalidOperationException(
                "The rebuilt path did not retain Impa at the right edge facing left on its first update.");
        }

        _saveData.SetGlobalFlag(OracleSaveData.GlobalFlagIntroDone, value: false);
        _saveData.SetRoomFlag(0, 0x39, OracleSaveData.RoomFlag40, value: false);
        _saveData.SetRoomFlag(0, 0x39, OracleSaveData.RoomFlag80, value: false);
        _transitions.BeginScroll(_player, Vector2I.Right, 0x39);
        NpcCharacter? gatheringFollower = impaEvent.Actor;
        if (gatheringFollower is null || !impaEvent.Following ||
            nayruIntro.ActorRegistry.Count != 7 || _roomEvents.Active)
        {
            throw new InvalidOperationException(
                "Room 0:39 did not create the seven intro gathering actors while Impa was following Link.");
        }
        FinishActiveScrollingTransitionForValidation();
        StepRoomEventFrames(1);
        if (gatheringFollower.Position != _player.Position + Vector2.Left * 16 ||
            nayruIntro.ActorRegistry.Values.Any(actor => !actor.Active))
        {
            throw new InvalidOperationException(
                "The complete Nayru gathering or following Impa did not survive the incoming room scroll.");
        }

        _transitions.BeginScroll(_player, Vector2I.Right, 0x3a);
        List<NpcCharacter> outgoingGathering = _entities.OutgoingEntities<NpcCharacter>()
            .Where(actor => actor.Name.ToString().StartsWith(
                "NayruIntro_", StringComparison.Ordinal))
            .ToList();
        if (nayruIntro.ActorRegistry.Count != 0 || !impaEvent.Following ||
            outgoingGathering.Count != 7 || outgoingGathering.Any(actor => !actor.Active))
        {
            throw new InvalidOperationException(
                "Leaving room 0:39 did not retain all seven dynamic audience actors in the outgoing scroll set.");
        }
        UpdateScrollingTransition(1.0 / 60.0);
        if (outgoingGathering.Any(actor => actor.TransitionDrawOffset != Vector2.Left * 4))
            throw new InvalidOperationException(
                "Room 0:39's dynamic audience did not move with the outgoing room texture.");
        FinishActiveScrollingTransitionForValidation();
        _transitions.BeginScroll(_player, Vector2I.Left, 0x39);
        NpcCharacter? returningGatheringFollower = impaEvent.Actor;
        if (returningGatheringFollower is null || !impaEvent.Following ||
            nayruIntro.ActorRegistry.Count != 7 ||
            nayruIntro.ActorRegistry.Values.Any(actor => !actor.Active))
        {
            throw new InvalidOperationException(
                "Re-entering pre-intro room 0:39 did not recreate all seven gathering actors.");
        }
        FinishActiveScrollingTransitionForValidation();

        ValidateImpaStoneEvent(impaEvent, encounterTrace);

        LoadValidationRoom(0, 0x6a);
        NpcCharacter? completedImpa = _npcNodes.Find(npc =>
            npc.Record.Id == 0x31 && npc.Record.SubId == 0x00);
        if (completedImpa is null || completedImpa.Active || _roomEvents.Active ||
            impaEvent.Following || impaEvent.FakeOctoroks.Count != 0)
        {
            throw new InvalidOperationException(
                "Room flag $40 did not suppress Impa and her fake Octoroks on room 0:6a re-entry.");
        }

        LoadValidationRoom(0, 0x7a);
        _player.WarpTo(new Vector2(0x38, 0x06));
        _player.Face(Vector2I.Up);
        impaEvent.UpdateHelpFrame(upPressed: true);
        if (impaEvent.HelpWaitingAtEdge || _roomEvents.Active || _dialogue.IsOpen)
        {
            throw new InvalidOperationException(
                "Room 0:7a flag $40 did not suppress TX_0100 on re-entry.");
        }

        CutsceneCommandTraceEntry[] helpStarts = encounterTrace.Entries
            .Where(entry =>
                entry.Source.Script == "interaction6b_subid00" &&
                entry.Phase == CutsceneCommandTracePhase.Started)
            .ToArray();
        string[] expectedHelpOpcodes =
        {
            "disablemenu", "setdisabledobjectscontinue", "setcounter", "showtext",
            "waitpreloadedcounter", "setdisabledobjectscontinue", "native",
            "orroomflagcontinue", "scriptend"
        };
        if (helpStarts.Length != expectedHelpOpcodes.Length ||
            helpStarts.Where((entry, index) =>
                entry.Source.Label != "interaction6b_subid00" ||
                entry.Source.CommandIndex != index ||
                entry.Source.Opcode != expectedHelpOpcodes[index] ||
                entry.Source.SourceLine <= 0 ||
                (index > 0 && entry.Source.SourceLine <=
                    helpStarts[index - 1].Source.SourceLine))
            .Any())
        {
            throw new InvalidOperationException(
                "The imported interaction6b_subid00 trace lost source lines, " +
                "native-operation order, or typed opcodes.");
        }

        int HelpCompletedUpdate(int commandIndex) => encounterTrace.Entries.Single(entry =>
            entry.Source.Script == "interaction6b_subid00" &&
            entry.Source.CommandIndex == commandIndex &&
            entry.Phase == CutsceneCommandTracePhase.Completed).ScriptUpdate;
        CutsceneCommandTraceEntry[] helpWaitUpdates = encounterTrace.Entries
            .Where(entry =>
                entry.Source.Script == "interaction6b_subid00" &&
                entry.Source.CommandIndex == 4 &&
                entry.Phase == CutsceneCommandTracePhase.Updated)
            .ToArray();
        if (helpStarts[0].ScriptUpdate != helpStarts[1].ScriptUpdate ||
            helpStarts[0].ScriptUpdate != helpStarts[2].ScriptUpdate ||
            helpStarts[0].ScriptUpdate != helpStarts[3].ScriptUpdate ||
            helpStarts[4].ScriptUpdate != HelpCompletedUpdate(3) + 1 ||
            helpWaitUpdates.Length != 29 ||
            helpWaitUpdates.Where((entry, index) => entry.Counter != 29 - index).Any() ||
            helpStarts[5].ScriptUpdate != HelpCompletedUpdate(4) ||
            helpStarts[6].ScriptUpdate != helpStarts[5].ScriptUpdate ||
            helpStarts[7].ScriptUpdate != helpStarts[5].ScriptUpdate ||
            helpStarts[8].ScriptUpdate != helpStarts[5].ScriptUpdate)
        {
            throw new InvalidOperationException(
                "interaction6b_subid00 did not preserve its same-update setup, " +
                "first-post-text decrement, 30-update counter, or completion handoff.");
        }

        CutsceneCommandTraceEntry[] starts = encounterTrace.Entries
            .Where(entry =>
                entry.Source.Script == "impaScript0" &&
                entry.Phase == CutsceneCommandTracePhase.Started)
            .ToArray();
        string[] expectedOpcodes =
        {
            "checkmemoryeq", "wait", "showtextdifferentforlinked", "wait",
            "setspeed", "move", "orroomflag", "scriptend"
        };
        if (starts.Length != expectedOpcodes.Length ||
            starts.Where((entry, index) =>
                entry.Source.Script != "impaScript0" ||
                entry.Source.Label != "impaScript0" ||
                entry.Source.CommandIndex != index ||
                entry.Source.Opcode != expectedOpcodes[index] ||
                entry.Source.SourceLine <= 0 ||
                (index > 0 && entry.Source.SourceLine <= starts[index - 1].Source.SourceLine))
            .Any())
        {
            throw new InvalidOperationException(
                "The imported impaScript0 trace lost source lines, command order, or typed opcodes.");
        }

        int CompletedUpdate(int commandIndex) => encounterTrace.Entries.Single(entry =>
            entry.Source.Script == "impaScript0" &&
            entry.Source.CommandIndex == commandIndex &&
            entry.Phase == CutsceneCommandTracePhase.Completed).ScriptUpdate;
        if (starts[1].ScriptUpdate != CompletedUpdate(0) + 1 ||
            starts[2].ScriptUpdate != CompletedUpdate(1) ||
            starts[3].ScriptUpdate != CompletedUpdate(2) + 1 ||
            starts[4].ScriptUpdate != CompletedUpdate(3) ||
            starts[5].ScriptUpdate != CompletedUpdate(4) + 1 ||
            starts[6].ScriptUpdate != CompletedUpdate(5) + 1 ||
            starts[7].ScriptUpdate != CompletedUpdate(6) + 1)
        {
            throw new InvalidOperationException(
                "impaScript0 did not preserve gate, text, wait, setspeed, counter2, " +
                "room-flag yield, or scriptend cadence.");
        }
        _roomEvents.CommandTraceSink = null;

        GD.Print("Validated room 0:7a $6b:$00 edge trigger, imported native-operation " +
            "source trace, fixed-bottom TX_0100, 30-update post-text wait, silent playable-intro " +
            "room music, room flag $40, and eight-Up handoff; " +
            "room 0:6a possessed Impa " +
            "$31:$00 PALH_97, three objectData fake Octoroks, linkCutscene1 $78/$04/$2e " +
            "cadence with SND_CLINK, staggered $14+$50/$3c/$5a escapes and three SND_THROW " +
            "calls, imported TX_0102/TX_0103 selection and source trace, 210/30 waits, " +
            "SPEED_080 movedown $20, MUS_FAIRY_FOUNTAIN volume 3, room flag $40, " +
            "animations $00-$03, single-copy " +
            "always-update scroll following, transition-end 16-entry follower-path rebuild, " +
            "room 0:39's seven-actor intro gathering during follow, clean leave/re-entry " +
            "recreation, and placed-Impa suppression when the follower returns.");
    }

    private void ValidateImpaStoneEvent(
        ImpaIntroEvent impaEvent,
        ValidationCutsceneTrace commandTrace)
    {
        const int group = 0;
        const int room = 0x59;
        ImpaIntroEventDatabase.ImpaStoneEventRecord record = impaEvent.Database.StoneRecord;
        ImpaIntroEventDatabase.ImpaStoneActorRecord stone = record.Actor;
        ImpaIntroEventDatabase.ImpaStoneTimingRecord timing = record.Timing;
        _saveData.SetRoomFlag(group, room, OracleSaveData.RoomFlag40, value: false);
        _saveData.SetRoomFlag(group, room, OracleSaveData.RoomFlag80, value: false);

        _transitions.BeginScroll(_player, Vector2I.Right, room);
        NpcCharacter? follower = impaEvent.Actor;
        NpcCharacter? stoneActor = impaEvent.StoneActor;
        Color stoneMidtone = new(0x0c / 31.0f, 0x12 / 31.0f, 0x11 / 31.0f);
        if (follower is null || stoneActor is null ||
            impaEvent.CurrentStoneStage != ImpaIntroEvent.StoneStage.WaitingForApproach ||
            stoneActor.Position != new Vector2(stone.InitialX, stone.InitialY) ||
            stone.SourceGrayscaleInverted ||
            stoneActor.CurrentAnimationTextureSize != new Vector2I(24, 16) ||
            stoneActor.CurrentAnimationOpaquePixels != 278 ||
            !stoneActor.CurrentAnimationUsesColor(stoneMidtone) ||
            stoneActor.ZIndex != NpcCharacter.FixedLowPriorityZIndex ||
            follower.ZIndex <= stoneActor.ZIndex)
        {
            throw new InvalidOperationException(
                "Room 0:59 did not transfer following Impa or instantiate the centered " +
                "INTERAC_TRIFORCE_STONE $34:$00 with its non-inverted 24x16 sprite, PALH_98, " +
                "and fixed priority 3 below follower Impa's relative priority 1/2.");
        }
        FinishActiveScrollingTransitionForValidation();

        _player.WarpTo(new Vector2(stone.ApproachX - 8, stone.ApproachY - 8));
        StepRoomEventFrames(1);
        if (stoneActor.ZIndex != NpcCharacter.FixedLowPriorityZIndex ||
            follower.ZIndex <= stoneActor.ZIndex)
        {
            throw new InvalidOperationException(
                "INTERAC_TRIFORCE_STONE priority 3 did not remain below follower Impa's " +
                "priority 1/2 after the room-entity priority update.");
        }
        if (!_player.CutsceneControlled || !_roomEvents.Active ||
            impaEvent.CurrentStoneStage != ImpaIntroEvent.StoneStage.SpotJumpHold ||
            impaEvent.Counter != timing.SpotHoldFrames)
        {
            throw new InvalidOperationException(
                "Impa did not clear following and begin the $1e-update first jump below $58/$78.");
        }

        StepRoomEventFrames(timing.SpotHoldFrames);
        int firstAirUpdates = 0;
        while (impaEvent.CurrentStoneStage == ImpaIntroEvent.StoneStage.SpotJumpAir &&
            firstAirUpdates++ < 60)
        {
            StepRoomEventFrames(1);
        }
        if (firstAirUpdates != 29 ||
            impaEvent.CurrentStoneStage != ImpaIntroEvent.StoneStage.FirstLandingWait ||
            follower.ScriptDrawOffset != Vector2.Zero)
        {
            throw new InvalidOperationException(
                $"Impa's -$1c0/$20 first jump did not land after 29 gravity updates ({firstAirUpdates}).");
        }
        StepRoomEventFrames(timing.FirstLandingWait);
        if (!_dialogue.IsOpen || _dialogue.CurrentMessage !=
            DialogueBox.PlainText(record.Texts.First.Message) ||
            impaEvent.Counter != timing.FirstTextPostFrames)
        {
            throw new InvalidOperationException("Impa did not show TX_0104 after the first landing wait.");
        }
        _dialogue.Close();
        StepRoomEventFrames(1 + timing.FirstTextPostFrames);
        int approachUpdates = 0;
        while (impaEvent.CurrentStoneStage == ImpaIntroEvent.StoneStage.ApproachStone &&
            approachUpdates++ < 80)
        {
            StepRoomEventFrames(1);
        }
        if (impaEvent.CurrentStoneStage != ImpaIntroEvent.StoneStage.AtStoneWait ||
            follower.Position != new Vector2(stone.TargetX, stone.TargetY) ||
            !Mathf.IsEqualApprox(follower.AnimationRate, 1.0f))
        {
            throw new InvalidOperationException(
                "Impa did not reach $38/$38 at SPEED_300 with the original close-radius snap.");
        }

        StepRoomEventFrames(timing.StoneWaitFrames + timing.SecondHoldFrames);
        int secondAirUpdates = 0;
        while (impaEvent.CurrentStoneStage == ImpaIntroEvent.StoneStage.SecondJumpAir &&
            secondAirUpdates++ < 60)
        {
            StepRoomEventFrames(1);
        }
        if (secondAirUpdates != 25 ||
            impaEvent.CurrentStoneStage != ImpaIntroEvent.StoneStage.SecondLandingWait)
        {
            throw new InvalidOperationException(
                $"Impa's -$180/$20 second jump did not land after 25 gravity updates ({secondAirUpdates}).");
        }
        StepRoomEventFrames(timing.SecondLandingWait);
        if (!_dialogue.IsOpen || _dialogue.CurrentMessage !=
            DialogueBox.PlainText(record.Texts.Sign.Message) ||
            !_dialogue.CurrentMessage.Contains('▲'))
        {
            throw new InvalidOperationException("Impa did not show expanded TX_0105 with the Triforce glyph.");
        }
        _dialogue.Close();
        StepRoomEventFrames(1 + timing.SignTextPostFrames);

        int linkUpdates = 0;
        while (!_dialogue.IsOpen && linkUpdates++ < 240)
        {
            StepRoomEventFrames(1);
        }
        if (!_dialogue.IsOpen || _dialogue.CurrentMessage !=
            DialogueBox.PlainText(record.Texts.Request.Message) ||
            _player.Position != new Vector2(stone.LinkTargetX, stone.LinkTargetY))
        {
            throw new InvalidOperationException(
                "linkCutscene2 did not route Link through $38/$48 and its 8/60/16 waits before TX_0106.");
        }
        _dialogue.Close();
        int firstRetreatUpdates = 0;
        while (!_dialogue.IsOpen && firstRetreatUpdates++ < 120)
            StepRoomEventFrames(1);
        if (firstRetreatUpdates != 98 || !_dialogue.IsOpen || _dialogue.CurrentMessage !=
            DialogueBox.PlainText(record.Texts.Hesitation.Message) ||
            follower.Position.X != stone.TargetX - 16)
        {
            throw new InvalidOperationException(
                "Imported impaScript_moveAwayFromRock did not reach TX_0107 after " +
                "the exact 98-update first retreat path.");
        }
        _dialogue.Close();
        int secondRetreatUpdates = 0;
        while (!_dialogue.IsOpen && secondRetreatUpdates++ < 120)
            StepRoomEventFrames(1);
        if (secondRetreatUpdates != 95 || !_dialogue.IsOpen || _dialogue.CurrentMessage !=
            DialogueBox.PlainText(record.Texts.Failure.Message) ||
            follower.Position.X != stone.TargetX - 32)
        {
            throw new InvalidOperationException(
                "Imported impaScript_moveAwayFromRock did not reach TX_0108 after " +
                "the exact 95-update second retreat path.");
        }
        _dialogue.Close();
        int finishRetreatUpdates = 0;
        while (impaEvent.CurrentStoneStage != ImpaIntroEvent.StoneStage.WaitingForPush &&
            finishRetreatUpdates++ < 60)
        {
            StepRoomEventFrames(1);
        }
        if (finishRetreatUpdates != 31 ||
            impaEvent.CurrentStoneStage != ImpaIntroEvent.StoneStage.WaitingForPush ||
            _roomEvents.Active || _player.CutsceneControlled ||
            impaEvent.WaitingNpcInitialized || follower.TextId != 0 ||
            follower.Record.CanFace ||
            follower.CurrentScriptAnimationSource != impaEvent.Database.Record.RightAnimation ||
            _entities.BlocksLink(follower.Position))
        {
            throw new InvalidOperationException(
                "Impa did not retain animation $01 for the one update between installing " +
                "impaScript_waitForRockToBeMoved and running rungenericnpc TX_010b.");
        }

        CutsceneCommandTraceEntry[] prePushStarts = commandTrace.Entries
            .Where(entry =>
                entry.Source.Script == "impaScript_moveAwayFromRock" &&
                entry.Phase == CutsceneCommandTracePhase.Started)
            .ToArray();
        string[] expectedPrePushOpcodes =
        {
            "checkmemoryeq", "setanimation", "wait", "showtext", "wait",
            "setanimation", "setangle", "setspeed", "applyspeed", "wait",
            "showtext", "wait", "applyspeed", "wait", "showtext", "wait",
            "writememory", "scriptend"
        };
        if (prePushStarts.Length != expectedPrePushOpcodes.Length ||
            prePushStarts.Where((entry, index) =>
                entry.Source.Label != "impaScript_moveAwayFromRock" ||
                entry.Source.CommandIndex != index ||
                entry.Source.Opcode != expectedPrePushOpcodes[index] ||
                entry.Source.SourceLine <= 0 ||
                (index > 0 && entry.Source.SourceLine <=
                    prePushStarts[index - 1].Source.SourceLine))
            .Any())
        {
            throw new InvalidOperationException(
                "The imported impaScript_moveAwayFromRock trace lost source lines, " +
                "command order, or typed opcodes.");
        }

        int PrePushCompletedUpdate(int commandIndex) => commandTrace.Entries.Single(entry =>
            entry.Source.Script == "impaScript_moveAwayFromRock" &&
            entry.Source.CommandIndex == commandIndex &&
            entry.Phase == CutsceneCommandTracePhase.Completed).ScriptUpdate;
        int PrePushDuration(int commandIndex) =>
            PrePushCompletedUpdate(commandIndex) - prePushStarts[commandIndex].ScriptUpdate;
        int gateUpdates = commandTrace.Entries.Count(entry =>
            entry.Source.Script == "impaScript_moveAwayFromRock" &&
            entry.Source.CommandIndex == 0 &&
            entry.Phase == CutsceneCommandTracePhase.Updated);
        if (gateUpdates == 0 ||
            PrePushDuration(2) != timing.RequestLeadFrames ||
            PrePushDuration(4) != timing.RequestPostFrames ||
            PrePushDuration(8) != timing.FirstBackAwayFrames ||
            PrePushDuration(9) != timing.BetweenFirstBackAwayFrames ||
            PrePushDuration(11) != timing.HesitationPostFrames ||
            PrePushDuration(12) != timing.SecondBackAwayFrames ||
            PrePushDuration(13) != timing.BetweenSecondBackAwayFrames ||
            PrePushDuration(15) != timing.FailurePostFrames ||
            prePushStarts[1].ScriptUpdate != PrePushCompletedUpdate(0) + 1 ||
            prePushStarts[2].ScriptUpdate != PrePushCompletedUpdate(1) + 1 ||
            prePushStarts[3].ScriptUpdate != PrePushCompletedUpdate(2) ||
            prePushStarts[4].ScriptUpdate != PrePushCompletedUpdate(3) + 1 ||
            prePushStarts[5].ScriptUpdate != PrePushCompletedUpdate(4) ||
            prePushStarts[6].ScriptUpdate != PrePushCompletedUpdate(5) + 1 ||
            prePushStarts[7].ScriptUpdate != PrePushCompletedUpdate(6) + 1 ||
            prePushStarts[8].ScriptUpdate != PrePushCompletedUpdate(7) + 1 ||
            prePushStarts[9].ScriptUpdate != PrePushCompletedUpdate(8) + 1 ||
            prePushStarts[10].ScriptUpdate != PrePushCompletedUpdate(9) ||
            prePushStarts[11].ScriptUpdate != PrePushCompletedUpdate(10) + 1 ||
            prePushStarts[12].ScriptUpdate != PrePushCompletedUpdate(11) ||
            prePushStarts[13].ScriptUpdate != PrePushCompletedUpdate(12) + 1 ||
            prePushStarts[14].ScriptUpdate != PrePushCompletedUpdate(13) ||
            prePushStarts[15].ScriptUpdate != PrePushCompletedUpdate(14) + 1 ||
            prePushStarts[16].ScriptUpdate != PrePushCompletedUpdate(15) ||
            prePushStarts[17].ScriptUpdate != prePushStarts[16].ScriptUpdate)
        {
            throw new InvalidOperationException(
                "impaScript_moveAwayFromRock did not preserve the native $02/$03 gate, " +
                "yield boundaries, waits, counter2 movement, or same-update $04 completion.");
        }

        StepRoomEventFrames(1);
        if (!impaEvent.WaitingNpcInitialized ||
            !_entities.BlocksLink(follower.Position) ||
            !_collision.Collides(follower.Position) || follower.Record.CanFace ||
            follower.CurrentScriptAnimationSource != impaEvent.Database.Record.RightAnimation)
        {
            throw new InvalidOperationException(
                "genericNpcScript did not install Impa's $06/$06 collision and TX_010b " +
                "without changing her animation or enabling automatic Link-facing.");
        }

        _player.WarpTo(follower.Position);
        impaEvent.UpdateStoneFrame(pushing: false);
        if (_player.Position != follower.Position + Vector2.Left * 12)
        {
            throw new InvalidOperationException(
                "interactionAnimateAsNpc did not resolve an exact Impa/Link overlap " +
                "horizontally by the combined $06+$06 collision radii.");
        }

        _player.WarpTo(follower.Position + Vector2.Right * 16);
        _player.Face(Vector2I.Left);
        if (!TryInteract(_player) || !_dialogue.IsOpen ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(record.Texts.Talk.Message) ||
            follower.Record.CanFace ||
            follower.CurrentScriptAnimationSource != impaEvent.Database.Record.RightAnimation)
        {
            throw new InvalidOperationException(
                "Waiting Impa did not expose rungenericnpc TX_010b while holding animation $01.");
        }
        _dialogue.Close();
        _player.WarpTo(new Vector2(0x50, stone.LeaveY + 2));
        impaEvent.UpdateStoneFrame(pushing: false, downPressed: true);
        if (!_dialogue.IsOpen || _player.Position.Y != stone.LeaveY ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(record.Texts.Leave.Message) ||
            !_dialogue.CurrentMessage.EndsWith("move this!"))
        {
            throw new InvalidOperationException(
                "The room $59 boundary guard did not clamp Y=$76 and expand TX_010a -> TX_010c.");
        }
        _dialogue.Close();

        _player.WarpTo(new Vector2(stone.InitialX + 16, stone.InitialY));
        _player.Face(Vector2I.Left);
        impaEvent.UpdateStoneFrame(pushing: false);
        if (impaEvent.StonePushCounter != timing.PushHoldFrames)
            throw new InvalidOperationException("The stone push counter did not reset to $14.");

        // Dynamic actors are not room-tile walls, so Link's generic wall-push
        // detector is deliberately false here. Drive the actual room-event
        // input path to ensure the interaction observes the held direction.
        _player.UpdatePushingState(Vector2.Left);
        if (_player.IsPushing)
            throw new InvalidOperationException(
                "The Triforce stone was incorrectly treated as static room-tile collision.");
        Input.ActionPress("move_left");
        Input.ActionPress("attack");
        StepRoomEventFrames(1);
        Input.ActionRelease("attack");
        if (impaEvent.StonePushCounter != timing.PushHoldFrames || _player.IsPushing)
        {
            throw new InvalidOperationException(
                "objectCheckLinkPushingAgainstCenter counted a push update while A was held.");
        }
        StepRoomEventFrames(timing.PushHoldFrames - 1);
        if (impaEvent.CurrentStoneStage != ImpaIntroEvent.StoneStage.WaitingForPush ||
            impaEvent.StonePushCounter != 1 || !_player.IsPushing)
        {
            throw new InvalidOperationException("The Triforce stone moved before 20 centered push updates.");
        }
        StepRoomEventFrames(1);
        Input.ActionRelease("move_left");
        if (impaEvent.CurrentStoneStage != ImpaIntroEvent.StoneStage.PushStarted ||
            !_player.CutsceneControlled || !_player.IsPushing ||
            impaEvent.StoneMoveCounter != timing.StoneMoveFrames)
        {
            throw new InvalidOperationException(
                "INTERAC_TRIFORCE_STONE did not start linkCutscene6 and its $40-update movement.");
        }

        for (int update = 1; update < timing.StoneMoveFrames; update++)
        {
            StepRoomEventFrames(1);
            if (Mathf.Abs(stoneActor.Position.X - _player.Position.X) <
                    stone.CollisionRadiusX + NpcCharacter.LinkCollisionRadius &&
                Mathf.Abs(stoneActor.Position.Y - _player.Position.Y) <
                    stone.CollisionRadiusY + NpcCharacter.LinkCollisionRadius)
            {
                throw new InvalidOperationException(
                    $"objectPreventLinkFromPassing allowed Link inside the moving stone " +
                    $"on update {update} (Link={_player.Position}, stone={stoneActor.Position}).");
            }
        }
        if (_saveData.HasRoomFlag(group, room, OracleSaveData.RoomFlag40) ||
            impaEvent.StoneMoveCounter != 1)
        {
            throw new InvalidOperationException("The stone set room flag $40 before counter1 reached zero.");
        }
        StepRoomEventFrames(1);
        if (!_saveData.HasRoomFlag(group, room, OracleSaveData.RoomFlag40) ||
            _saveData.HasRoomFlag(group, room, OracleSaveData.RoomFlag80) ||
            stoneActor.Position != new Vector2(stone.LeftX, stone.InitialY) ||
            _player.Position != new Vector2(0x38, stone.InitialY) ||
            _collision.Collides(_player.Position) ||
            _currentRoom.GetMetatile(new Vector2(stone.LeftX, stone.MovedY)) !=
                stone.FinalLayoutTile ||
            !_currentRoom.IsSolid(new Vector2(stone.LeftX, stone.MovedY)))
        {
            throw new InvalidOperationException(
                "The left-pushed stone did not snap to X=$28, leave Link outside at X=$38, " +
                $"set room flag $40, and install collision $0f (flags=" +
                $"{_saveData.GetRoomFlags(group, room):x2}, stone={stoneActor.Position}, " +
                $"Link={_player.Position}, LinkCollision={_collision.Collides(_player.Position)}, " +
                $"tile={_currentRoom.GetMetatile(new Vector2(stone.LeftX, stone.MovedY)):x2}, " +
                $"solid={_currentRoom.IsSolid(new Vector2(stone.LeftX, stone.MovedY))}).");
        }

        int responseUpdates = 0;
        bool sawRightFacingReunion = false;
        bool sawUpFacingCorrection = false;
        while (!_dialogue.IsOpen && responseUpdates++ < 400)
        {
            StepRoomEventFrames(1);
            if (!sawRightFacingReunion && commandTrace.Entries.Any(entry =>
                entry.Source.Script == "impaScript_rockJustMoved" &&
                entry.Source.CommandIndex == 10 &&
                entry.Phase == CutsceneCommandTracePhase.Started))
            {
                sawRightFacingReunion = true;
                if (follower.CurrentScriptAnimationSource !=
                    impaEvent.Database.Record.RightAnimation)
                {
                    throw new InvalidOperationException(
                        "Impa did not retain animation $01 while moving right to rejoin Link.");
                }
            }
            if (!sawUpFacingCorrection && commandTrace.Entries.Any(entry =>
                entry.Source.Script == "impaScript_rockJustMoved" &&
                entry.Source.CommandIndex == 13 &&
                entry.Phase == CutsceneCommandTracePhase.Started))
            {
                sawUpFacingCorrection = true;
                if (follower.FacingVector != Vector2I.Up)
                {
                    throw new InvalidOperationException(
                        "moveup $11 did not select Impa's up-facing animation before correction movement.");
                }
            }
        }
        if (!_dialogue.IsOpen || _dialogue.CurrentMessage !=
            DialogueBox.PlainText(record.Texts.Thanks.Message) ||
            responseUpdates >= 400 || !sawRightFacingReunion ||
            !sawUpFacingCorrection)
        {
            throw new InvalidOperationException(
                "Impa's left-push 4+65+120 response, SPEED_100 move, moveup $11, " +
                "or TX_0109 timing stalled.");
        }
        _dialogue.Close();
        int finishUpdates = 0;
        bool sawUpFacingFinalMove = false;
        while (impaEvent.CurrentStoneStage != ImpaIntroEvent.StoneStage.Moved &&
            finishUpdates++ < 100)
        {
            StepRoomEventFrames(1);
            if (!sawUpFacingFinalMove && commandTrace.Entries.Any(entry =>
                entry.Source.Script == "impaScript_rockJustMoved" &&
                entry.Source.CommandIndex == 21 &&
                entry.Phase == CutsceneCommandTracePhase.Started))
            {
                sawUpFacingFinalMove = true;
                if (follower.FacingVector != Vector2I.Up)
                {
                    throw new InvalidOperationException(
                        "moveup $20 did not select Impa's up-facing animation before reunion movement.");
                }
            }
        }
        if (!sawUpFacingFinalMove || !impaEvent.Following ||
            _player.CutsceneControlled || _player.FacingVector != Vector2I.Down ||
            follower.FacingVector != Vector2I.Down || follower.Position != _player.Position)
        {
            throw new InvalidOperationException(
                "Impa did not face up for moveup $20, finish TX_0109, face down with Link, " +
                "and rebuild following.");
        }

        CutsceneCommandTraceEntry[] leftPostPushStarts = commandTrace.Entries
            .Where(entry =>
                entry.Source.Script == "impaScript_rockJustMoved" &&
                entry.Phase == CutsceneCommandTracePhase.Started)
            .ToArray();
        int[] expectedLeftPostPushCommands =
        {
            0, 1, 2, 3, 4, 5, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17,
            18, 19, 20, 21, 22
        };
        if (leftPostPushStarts.Length != expectedLeftPostPushCommands.Length ||
            leftPostPushStarts.Where((entry, pathIndex) =>
                entry.Source.CommandIndex != expectedLeftPostPushCommands[pathIndex] ||
                entry.Source.SourceLine <= 0 ||
                (pathIndex > 0 && entry.Source.SourceLine <=
                    leftPostPushStarts[pathIndex - 1].Source.SourceLine))
            .Any())
        {
            throw new InvalidOperationException(
                "The left-push impaScript_rockJustMoved trace did not follow the " +
                "imported correction branch and source order.");
        }

        CutsceneCommandTraceEntry LeftCompleted(int commandIndex) =>
            commandTrace.Entries.Single(entry =>
                entry.Source.Script == "impaScript_rockJustMoved" &&
                entry.Source.CommandIndex == commandIndex &&
                entry.Phase == CutsceneCommandTracePhase.Completed);
        int LeftStartedUpdate(int commandIndex) => leftPostPushStarts.Single(entry =>
            entry.Source.CommandIndex == commandIndex).ScriptUpdate;
        int LeftDuration(int commandIndex) =>
            LeftCompleted(commandIndex).ScriptUpdate - LeftStartedUpdate(commandIndex);
        if (LeftCompleted(1).NextCommandIndex != 2 ||
            LeftCompleted(5).NextCommandIndex != 7 ||
            LeftCompleted(12).NextCommandIndex != 13 ||
            LeftDuration(0) != timing.ReactionLeadFrames ||
            LeftDuration(4) != timing.LeftCorrectionFrames ||
            LeftDuration(7) != timing.CommonWaitFrames ||
            LeftDuration(10) != timing.ResponseRightFrames ||
            LeftDuration(11) != timing.ResponseWait1Frames ||
            LeftDuration(13) != timing.ResponseUpFrames ||
            LeftDuration(14) != timing.ResponseWait2Frames ||
            LeftDuration(17) != timing.PoseWaitFrames ||
            LeftDuration(19) != timing.ThanksPostFrames ||
            LeftDuration(21) != timing.FinalMoveFrames ||
            LeftStartedUpdate(1) != LeftCompleted(0).ScriptUpdate ||
            LeftStartedUpdate(2) != LeftCompleted(0).ScriptUpdate ||
            LeftStartedUpdate(5) != LeftCompleted(4).ScriptUpdate + 1 ||
            LeftStartedUpdate(7) != LeftStartedUpdate(5) ||
            LeftStartedUpdate(12) != LeftCompleted(11).ScriptUpdate ||
            LeftStartedUpdate(13) != LeftStartedUpdate(12) ||
            LeftStartedUpdate(15) != LeftCompleted(14).ScriptUpdate ||
            LeftStartedUpdate(16) != LeftStartedUpdate(15) ||
            LeftStartedUpdate(18) != LeftCompleted(17).ScriptUpdate ||
            LeftStartedUpdate(20) != LeftCompleted(19).ScriptUpdate ||
            LeftStartedUpdate(22) != LeftCompleted(21).ScriptUpdate + 1)
        {
            throw new InvalidOperationException(
                "The left-push post-stone script lost a branch decision, counter duration, " +
                "yield boundary, or same-update continuation.");
        }

        // Execute the imported right-push branch independently of room state;
        // the left path above covers the complete native stone/Link integration.
        // This second lane proves both jump targets and all skipped commands.
        var rightPostPushHost = new ValidationImpaPostPushHost(linkAngle: 0x08);
        var rightPostPushRunner = new CutsceneCommandRunner(rightPostPushHost);
        rightPostPushRunner.Start(impaEvent.Database.StonePostPushCommands);
        int rightGuard = 0;
        while (rightPostPushRunner.Active && rightGuard++ < 500)
        {
            rightPostPushHost.AdvanceValidationFrame();
            rightPostPushRunner.AdvanceFrame();
            if (rightPostPushHost.DialogueOpen)
                rightPostPushHost.CloseDialogue();
        }
        int[] expectedRightPostPushCommands =
        {
            0, 1, 6, 7, 8, 9, 10, 11, 12, 15, 16, 17, 18, 19, 20, 21, 22
        };
        CutsceneCommandTraceEntry[] rightPostPushStarts = rightPostPushHost.Trace.Entries
            .Where(entry => entry.Phase == CutsceneCommandTracePhase.Started)
            .ToArray();
        CutsceneCommandTraceEntry RightCompleted(int commandIndex) =>
            rightPostPushHost.Trace.Entries.Single(entry =>
                entry.Source.CommandIndex == commandIndex &&
                entry.Phase == CutsceneCommandTracePhase.Completed);
        if (rightPostPushRunner.Active || !rightPostPushHost.Ended ||
            rightPostPushHost.Signal != 0x07 ||
            rightPostPushHost.TextIds.ToArray() is not [0x0109] ||
            rightPostPushStarts.Length != expectedRightPostPushCommands.Length ||
            rightPostPushStarts.Where((entry, pathIndex) =>
                entry.Source.CommandIndex != expectedRightPostPushCommands[pathIndex]).Any() ||
            RightCompleted(1).NextCommandIndex != 6 ||
            RightCompleted(12).NextCommandIndex != 15 ||
            rightPostPushHost.Position != new Vector2(32.0f, -15.5f) ||
            rightPostPushHost.Facing != Vector2I.Up)
        {
            throw new InvalidOperationException(
                "The imported right-push branch did not skip both left corrections, " +
                "retain its 65-update wait, signal $07, or complete the reunion path.");
        }

        LoadValidationRoom(group, room);
        NpcCharacter? movedStone = impaEvent.StoneActor;
        if (movedStone is null || movedStone.Position != new Vector2(stone.LeftX, stone.MovedY) ||
            _roomEvents.Active || impaEvent.Following ||
            !_currentRoom.IsSolid(new Vector2(stone.LeftX, stone.MovedY)))
        {
            throw new InvalidOperationException(
                "PART_TRIFORCE_STONE $5a:$5a did not restore the left-side solid stone on re-entry.");
        }

        _saveData.SetRoomFlag(group, room, OracleSaveData.RoomFlag40, value: false);
        _saveData.SetRoomFlag(group, room, OracleSaveData.RoomFlag80);
        LoadValidationRoom(group, room);
        movedStone = impaEvent.StoneActor;
        if (movedStone is null || movedStone.Position != new Vector2(stone.RightX, stone.MovedY) ||
            !_currentRoom.IsSolid(new Vector2(stone.RightX, stone.MovedY)) ||
            _currentRoom.IsSolid(new Vector2(stone.LeftX, stone.MovedY)))
        {
            throw new InvalidOperationException(
                "PART_TRIFORCE_STONE $5a:$5a did not select the right-side $80 position on re-entry.");
        }

        GD.Print("Validated room 0:59 Impa/Triforce-stone event: PALH_98 interaction/part " +
            "forms, fixed priority 3 below follower Impa, two fixed-point jumps, " +
            "TX_0104-$010b, native linkCutscene2 targeting, imported " +
            "impaScript_moveAwayFromRock source trace and $02/$03/$04 handshake, " +
            "two SPEED_080 retreats, exact rungenericnpc wait animation/collision/talk loop, " +
            "A/B-safe 20-update push, 64-update SPEED_40 movement with per-update " +
            "objectPreventLinkFromPassing, linkCutscene6, direction flags $40/$80, imported " +
            "impaScript_rockJustMoved left/right branch traces, response waits, moveup $11/$20 " +
            "facing resets, $07, TX_0109 follower restore, final collision, sounds, and " +
            "completed re-entry.");
    }

    private void ValidateMakuTreeDisappearanceCutscene()
    {
        MakuTreeDisappearanceEvent makuEvent = _roomEvents.MakuTree;
        MakuTreeCutsceneDatabase makuDatabase = makuEvent.Database;
        MakuTreeCutsceneDatabase.MakuTreeCutsceneRecord makuRecord = makuDatabase.Record;
        var commandTrace = new ValidationCutsceneTrace();
        _roomEvents.CommandTraceSink = commandTrace;
        _sound.ClearPlayRequestAudit();
        LoadValidationRoom(0, 0x38);
        // The event is entered through room position $52 (the open $dc tile),
        // before its simulated right/up approach takes over.
        _player.WarpTo(new Vector2(0x28, 0x58));
        NpcCharacter? makuTree = _npcNodes.Find(npc =>
            npc.Record.Id == 0x87 && npc.Record.SubId == 0x00);
        if (makuTree is null || !_roomEvents.Active || !makuTree.Active ||
            makuTree.Position != new Vector2(0x50, 0x40))
        {
            throw new InvalidOperationException(
                "Room 0:38 did not start the $87:$01 Maku Tree entry event at $40/$50.");
        }
        bool retainedPalettes = true;
        for (int header = 0; header < MakuTreeCutsceneDatabase.PaletteCount; header++)
        for (int palette = 4;
            palette < MakuTreeCutsceneDatabase.BackgroundPalettesPerHeader;
            palette++)
        for (int shade = 0; shade < MakuTreeCutsceneDatabase.ColorsPerPalette; shade++)
        {
            retainedPalettes &= makuDatabase.BackgroundPalettes[header, palette, shade]
                .IsEqualApprox(makuDatabase.BackgroundPalettes[
                    makuRecord.InitialPaletteHeader, palette, shade]);
        }
        // Metatile $8f at room tile 3,7 selects BG palette 7. Pixel $31,$71
        // uses its first color, making it a focused check for the gate shown
        // during the unswapped-layout lead-in.
        OracleRoomData unswappedRoom = _currentRoom;
        Vector2I gatePixelPosition = new(0x31, 0x71);
        Color gatePixel = unswappedRoom.GetRenderedPixelForValidation(gatePixelPosition);
        Color expectedGate = makuDatabase.BackgroundPalettes[
            makuRecord.InitialPaletteHeader, 5, 0];
        bool GateMatchesExpected(Color color) =>
            Mathf.RoundToInt(color.R * 31) == Mathf.RoundToInt(expectedGate.R * 31) &&
            Mathf.RoundToInt(color.G * 31) == Mathf.RoundToInt(expectedGate.G * 31) &&
            Mathf.RoundToInt(color.B * 31) == Mathf.RoundToInt(expectedGate.B * 31);
        if (makuRecord.InitialPaletteHeader != 2 ||
            makuDatabase.BackgroundPalettes.GetLength(0) != 4 ||
            makuDatabase.BackgroundPalettes.GetLength(1) != 6 ||
            makuDatabase.BackgroundPalettes.GetLength(2) != 4 ||
            !retainedPalettes || makuEvent.PaletteHeader != makuRecord.InitialPaletteHeader ||
            Mathf.RoundToInt(expectedGate.R * 31) != 0x19 ||
            Mathf.RoundToInt(expectedGate.G * 31) != 0x15 ||
            Mathf.RoundToInt(expectedGate.B * 31) != 0x02 ||
            !GateMatchesExpected(gatePixel))
        {
            throw new InvalidOperationException(
                $"The unswapped Maku Tree room did not apply PALH_8f to BG palettes 2-7 " +
                $"before simulated input (header={makuEvent.PaletteHeader}, " +
                $"gate={gatePixel}, expected={expectedGate}, retained={retainedPalettes}).");
        }
        if (makuTree.CurrentAnimationTextureSize.X <= 32 ||
            makuTree.CurrentAnimationTextureSize.Y <= 32 ||
            makuTree.CurrentAnimationOffset.Y >= -16)
        {
            throw new InvalidOperationException(
                $"The Maku Tree face OAM was clipped to an ordinary NPC canvas " +
                $"(size={makuTree.CurrentAnimationTextureSize}, offset={makuTree.CurrentAnimationOffset}).");
        }

        Vector2 inputStart = _player.Position;
        StepRoomEventFrames(60);
        if (_player.Position != inputStart)
            throw new InvalidOperationException("Maku Tree simulated input did not begin with 60 idle updates.");
        StepRoomEventFrames(48);
        if (makuEvent.InputFrame != 108 || _player.FacingVector != Vector2I.Right ||
            _player.Position.X <= inputStart.X)
        {
            throw new InvalidOperationException(
                $"Maku Tree simulated input did not hold BTN_RIGHT for exactly 48 updates " +
                $"(input={makuEvent.InputFrame}, facing={_player.FacingVector}, " +
                $"start={inputStart}, current={_player.Position}).");
        }
        StepRoomEventFrames(4);
        Vector2 beforeUp = _player.Position;
        StepRoomEventFrames(14);
        if (makuEvent.InputFrame != 126 || _player.FacingVector != Vector2I.Up ||
            _player.Position.Y >= beforeUp.Y)
        {
            throw new InvalidOperationException(
                "Maku Tree simulated input did not hold BTN_UP for exactly 14 updates.");
        }
        StepRoomEventFrames(84);
        if (_dialogue.IsOpen || makuEvent.CurrentCommandIndex != 5 ||
            makuEvent.Counter != 3)
        {
            throw new InvalidOperationException(
                "The Maku Tree script preamble did not retain its collision/gate update boundaries.");
        }
        StepRoomEventFrames(3);
        if (!_dialogue.IsOpen || _dialogue.Position.Y != 80 ||
            !_dialogue.CurrentMessage.StartsWith("Pleased to meet\nyou, young hero."))
        {
            throw new InvalidOperationException(
                "TX_0564 did not open after the script preamble and original 210-update wait.");
        }

        _dialogue.Close();
        StepRoomEventFrames(61);
        if (_sound.LastPlayRequest != OracleSoundEngine.SndCtrlStopMusic ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndCtrlStopMusic) != 1)
        {
            throw new InvalidOperationException(
                "The Maku Tree did not stop its music after the 60-update post-text wait.");
        }
        StepRoomEventFrames(1);
        if (makuTree.CurrentAnimationFrame != 0 ||
            makuTree.CurrentAnimationOpaquePixels == 0)
        {
            throw new InvalidOperationException(
                "The Maku Tree animation command did not reset the frown to visible frame zero.");
        }
        StepRoomEventFrames(3);
        if (makuTree.CurrentAnimationFrame != 1)
            throw new InvalidOperationException(
                "INTERAC_MAKU_TREE animation 4 did not use its original four-update first frame.");
        StepRoomEventFrames(57);
        if (_sound.LastPlayRequest != OracleSoundEngine.SndMakuDisappear ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndMakuDisappear) != 1)
        {
            throw new InvalidOperationException(
                "The first SND_MAKUDISAPPEAR did not start with the palette-cycling disappearance.");
        }

        StepRoomEventFrames(1);
        int paletteBefore = makuEvent.PaletteHeader;
        StepRoomEventFrames(8);
        if (makuEvent.PaletteHeader == paletteBefore)
            throw new InvalidOperationException(
                "The $9a/$c4/$8f/$c5 Maku Tree palettes did not cycle within eight updates.");
        StepRoomEventFrames(202);
        if (!_dialogue.IsOpen || _dialogue.Position.Y != 80 || _dialogue.CurrentMessage != "Ahh...")
            throw new InvalidOperationException("TX_0540 did not open after 210 disappearance updates.");

        _dialogue.Close();
        StepRoomEventFrames(1);
        if (_sound.LastPlayRequest != OracleSoundEngine.SndMakuDisappear ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndMakuDisappear) != 2)
        {
            throw new InvalidOperationException(
                "TX_0540 did not replay SND_MAKUDISAPPEAR when its textbox closed.");
        }
        StepRoomEventFrames(211);
        if (!_dialogue.IsOpen || _dialogue.Position.Y != 80 ||
            !_dialogue.CurrentMessage.StartsWith("I feel so weird.\nI'm vanishing!"))
            throw new InvalidOperationException("TX_0541 did not follow the original 210-update pause.");

        _dialogue.Close();
        StepRoomEventFrames(1);
        if (_sound.LastPlayRequest != OracleSoundEngine.SndMakuDisappear ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndMakuDisappear) != 3)
        {
            throw new InvalidOperationException(
                "TX_0541 did not replay SND_MAKUDISAPPEAR when its textbox closed.");
        }
        StepRoomEventFrames(151);
        if (!makuEvent.HasState || makuEvent.Completed || IsTransitioning ||
            _saveData.MakuTreeState != 1 ||
            _saveData.HasGlobalFlag(OracleSaveData.GlobalFlagMakuTreeDisappeared))
        {
            throw new InvalidOperationException(
                "The Maku Tree script did not increment wMakuTreeState and defer its native handler by one update.");
        }
        StepRoomEventFrames(1);
        if (!makuEvent.Completed || _roomEvents.Active || !IsTransitioning ||
            _activeGroup != 0 || _currentRoom.Id != 0x38 ||
            !_saveData.HasGlobalFlag(OracleSaveData.GlobalFlagMakuTreeDisappeared) ||
            !_saveData.HasRoomFlag(0, 0x38, OracleSaveData.RoomFlagLayoutSwap) ||
            _saveData.MakuTreeState != 1 ||
            _sound.LastPlayRequest != OracleSoundEngine.SndFadeOut ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndFadeOut) != 1)
        {
            throw new InvalidOperationException(
                "The Maku Tree event did not persist GLOBALFLAG_0c, wMakuTreeState, room bit 0, " +
                "and initiate its hardcoded same-room warp after 150 updates.");
        }
        Color fadeStartGate = unswappedRoom.GetRenderedPixelForValidation(gatePixelPosition);
        if (_currentRoom.TilesetId != 0x22 || !makuTree.Active || _warpFade.Color.A != 0.0f ||
            !GateMatchesExpected(fadeStartGate))
        {
            throw new InvalidOperationException(
                $"The delayed $83 fade replaced room 0:38 or restored its corrupt base palette " +
                $"before beginning its transition to white (gate={fadeStartGate}).");
        }

        for (int frame = 0; frame < RoomTransitionController.DelayedWarpFadeFrames - 1; frame++)
            UpdateRoomWarpTransition(1.0 / 60.0);
        Color nearWhiteGate = unswappedRoom.GetRenderedPixelForValidation(gatePixelPosition);
        if (_currentRoom.TilesetId != 0x22 || _warpFade.Color.A <= 0.9f ||
            _warpFade.Color.A >= 1.0f || !GateMatchesExpected(nearWhiteGate))
        {
            throw new InvalidOperationException(
                $"The $83 delayed fade did not retain the old layout and cutscene palette " +
                $"for 124 updates (tileset={_currentRoom.TilesetId:x2}, " +
                $"alpha={_warpFade.Color.A}, gate={nearWhiteGate}).");
        }
        UpdateRoomWarpTransition(1.0 / 60.0);
        NpcCharacter? reloadedTree = _npcNodes.Find(npc =>
            npc.Record.Id == 0x87 && npc.Record.SubId == 0x00);
        Color retiredGate = unswappedRoom.GetRenderedPixelForValidation(gatePixelPosition);
        if (reloadedTree is null || reloadedTree.Active || GateMatchesExpected(retiredGate) ||
            !_rooms.IsLayoutSwapped(0, 0x38) || _currentRoom.TilesetId != 0x23 ||
            _currentRoom.GetMetatile(new Vector2(0x48, 0x28)) != 0xf9)
        {
            throw new InvalidOperationException(
                $"Room flag bit 0 did not load group 2's tree-less room 0:38 layout and suppress $87 " +
                $"(tree={reloadedTree is not null}/{reloadedTree?.Active}, " +
                $"swap={_rooms.IsLayoutSwapped(0, 0x38)}, retiredGate={retiredGate}, " +
                $"tileset={_currentRoom.TilesetId:x2}, " +
                $"tile24={_currentRoom.GetMetatile(new Vector2(0x48, 0x28)):x2}).");
        }

        for (int frame = 0; frame < WarpFadeFrames; frame++)
            UpdateRoomWarpTransition(1.0 / 60.0);
        if (IsTransitioning || _player.Position != new Vector2(0x58, 0x48))
        {
            throw new InvalidOperationException(
                $"The room $38/$45 hardcoded warp did not remain at $48/$58; got {_player.Position}.");
        }
        _player._PhysicsProcess(1.0 / 60.0);
        if (_activeGroup != 0 || _currentRoom.Id != 0x38 || IsTransitioning)
            throw new InvalidOperationException(
                "The $45 re-entry incorrectly walked Link into room 0:38's $ee/$ef warp to 5:cf.");

        LoadValidationRoom(0, 0x38);
        reloadedTree = _npcNodes.Find(npc => npc.Record.Id == 0x87 && npc.Record.SubId == 0x00);
        if (_roomEvents.Active || reloadedTree is null || reloadedTree.Active)
            throw new InvalidOperationException("The completed Maku Tree entry event retriggered on room reload.");

        CutsceneCommandTraceEntry[] starts = commandTrace.Entries
            .Where(entry => entry.Phase == CutsceneCommandTracePhase.Started)
            .ToArray();
        string[] expectedOpcodes =
        {
            "disablemenu", "setanimationcontinue", "setcollisionradii",
            "makeabuttonsensitive", "gate", "wait", "showtext", "wait",
            "playsound", "setanimationcontinue", "wait", "playsound",
            "writememory", "wait", "showtext", "playsound", "wait",
            "showtext", "playsound", "wait", "writememory", "native",
            "scriptend"
        };
        if (starts.Length != expectedOpcodes.Length ||
            starts.Where((entry, index) =>
                entry.Source.Script != "makuTree_subid01Script_body" ||
                entry.Source.Label != "makuTree_subid01Script_body" ||
                entry.Source.CommandIndex != index ||
                entry.Source.Opcode != expectedOpcodes[index] ||
                entry.Source.SourceLine <= 0 ||
                (index > 0 && entry.Source.SourceLine <= starts[index - 1].Source.SourceLine))
            .Any())
        {
            throw new InvalidOperationException(
                "The imported Maku Tree command trace lost source lines, command order, or typed opcodes.");
        }

        int CompletedUpdate(int commandIndex) => commandTrace.Entries.Single(entry =>
            entry.Source.CommandIndex == commandIndex &&
            entry.Phase == CutsceneCommandTracePhase.Completed).ScriptUpdate;
        if (starts[1].ScriptUpdate != starts[0].ScriptUpdate ||
            starts[2].ScriptUpdate != starts[0].ScriptUpdate ||
            starts[3].ScriptUpdate != CompletedUpdate(2) + 1 ||
            starts[4].ScriptUpdate != starts[3].ScriptUpdate ||
            starts[5].ScriptUpdate != CompletedUpdate(4) + 1 ||
            starts[6].ScriptUpdate != CompletedUpdate(5) ||
            starts[7].ScriptUpdate != CompletedUpdate(6) + 1 ||
            starts[8].ScriptUpdate != CompletedUpdate(7) ||
            starts[9].ScriptUpdate != CompletedUpdate(8) + 1 ||
            starts[10].ScriptUpdate != starts[9].ScriptUpdate ||
            starts[11].ScriptUpdate != CompletedUpdate(10) ||
            starts[12].ScriptUpdate != CompletedUpdate(11) + 1 ||
            starts[13].ScriptUpdate != starts[12].ScriptUpdate ||
            starts[14].ScriptUpdate != CompletedUpdate(13) ||
            starts[15].ScriptUpdate != CompletedUpdate(14) + 1 ||
            starts[16].ScriptUpdate != CompletedUpdate(15) + 1 ||
            starts[17].ScriptUpdate != CompletedUpdate(16) ||
            starts[18].ScriptUpdate != CompletedUpdate(17) + 1 ||
            starts[19].ScriptUpdate != CompletedUpdate(18) + 1 ||
            starts[20].ScriptUpdate != CompletedUpdate(19) ||
            starts[21].ScriptUpdate != starts[20].ScriptUpdate ||
            starts[22].ScriptUpdate != starts[20].ScriptUpdate)
        {
            throw new InvalidOperationException(
                "makuTree_subid01Script_body did not preserve carry-through, yield, wait, " +
                "dialogue, or final same-update command cadence.");
        }
        _roomEvents.CommandTraceSink = null;

        GD.Print("Validated room 0:38 Maku Tree $87:$01 simulated input, two-sheet unclipped face OAM, " +
            "initial six-palette PALH_8f gate/ground colors, fixed-bottom \\pos(2) dialogue, " +
            "imported typed script/source trace, exact command yields, 210/60/60/210/210/150 waits, four-header " +
            "palette cycle, STOPMUSIC/three SND_MAKUDISAPPEAR/SND_FADEOUT cue chain, " +
            "cutscene palette retained through the delayed 125-update white fade, " +
            "and one-shot $45 re-entry warp.");
    }

    private void StepRoomEventFrames(int frames)
    {
        for (int frame = 0; frame < frames; frame++)
        {
            _entities.Update(1.0 / 60.0, _player);
            _roomEvents.Update(1.0 / 60.0);
            _sound.Tick();
        }
    }

    private void ValidateNayruIntroCutscene()
    {
        NayruIntroEvent nayruIntro = _roomEvents.Nayru;
        var nayruTrace = new ValidationCutsceneTrace();
        _roomEvents.CommandTraceSink = nayruTrace;
        const int group = 0;
        const int roomId = 0x39;
        Vector2 portalPoint = new(0x28, 0x28);
        OracleRoomData sourceRoom = _rooms.World.LoadRoom(group, roomId);
        byte sourcePortalTile = sourceRoom.GetMetatile(portalPoint);
        if (sourcePortalTile != 0x3a)
            sourceRoom.ReplaceMetatile(portalPoint, sourcePortalTile, 0x3a, (long)_animationTicks);
        _saveData.SetGlobalFlag(OracleSaveData.GlobalFlagPregameIntroDone);
        _saveData.SetGlobalFlag(OracleSaveData.GlobalFlagIntroDone, value: false);
        _saveData.SetRoomFlag(group, roomId, OracleSaveData.RoomFlag40, value: false);
        _saveData.SetRoomFlag(group, roomId, OracleSaveData.RoomFlag80, value: false);
        _sound.PlayMusicIfChanged(OracleSoundEngine.MusFairyFountain);
        _sound.SetMusicVolume(3);
        LoadValidationRoom(group, 0x59);
        _transitions.BeginScroll(_player, Vector2I.Up, 0x49);
        if (_sound.ActiveMusic != OracleSoundEngine.MusFairyFountain ||
            _sound.MusicVolume != 3)
        {
            throw new InvalidOperationException(
                "INTERAC_PLAY_NAYRU_MUSIC $2f ran before the 0:59 -> 0:49 scroll completed.");
        }
        FinishActiveScrollingTransitionForValidation();
        if (_sound.ActiveMusic != OracleSoundEngine.MusNayru ||
            _sound.MusicVolume != 2)
        {
            throw new InvalidOperationException(
                "Room 0:49 did not start MUS_NAYRU with volume 2 after its incoming scroll.");
        }
        _transitions.BeginScroll(_player, Vector2I.Up, roomId);
        if (_sound.ActiveMusic != OracleSoundEngine.MusNayru ||
            _sound.MusicVolume != 2)
        {
            throw new InvalidOperationException(
                "The 0:49 -> 0:39 scroll changed Nayru's volume before her interaction resumed.");
        }
        FinishActiveScrollingTransitionForValidation();
        if (_sound.ActiveMusic != OracleSoundEngine.MusNayru ||
            _sound.MusicVolume != 2)
        {
            throw new InvalidOperationException(
                "Nayru's destination interaction changed music during the 0:39 scroll.");
        }

        if (_inventory.HasTreasure(TreasureDatabase.TreasureSword) ||
            _inventory.SwordLevel != 0 || _inventoryMenu.CanOpenForValidation ||
            _mapMenu.CanOpenNormalForValidation)
        {
            throw new InvalidOperationException(
                "The pre-intro save retained the development sword or allowed Start/Select " +
                "before GLOBALFLAG_INTRO_DONE $0a.");
        }

        NayruActorRegistry actors = nayruIntro.ActorRegistry;
        (string Name, Vector2 Position)[] expectedActors =
        {
            ("Nayru", new Vector2(0x78, 0x18)),
            ("Ralph", new Vector2(0x88, 0x30)),
            ("Bear", new Vector2(0x58, 0x38)),
            ("Monkey", new Vector2(0x78, 0x50)),
            ("Rabbit", new Vector2(0x88, 0x50)),
            ("Boy", new Vector2(0x68, 0x48)),
            ("Bird", new Vector2(0x48, 0x2c))
        };
        if (actors.Count != expectedActors.Length || nayruIntro.CurrentStage != 1 ||
            _roomEvents.Active || _player.CutsceneControlled ||
            _currentRoom.GetMetatile(portalPoint) != 0x3a)
        {
            throw new InvalidOperationException(
                "Room 0:39 did not create the pre-intro $6b:$01 audience while retaining Link control.");
        }
        var nayruDatabase = new NayruIntroEventDatabase();
        NayruIntroEventDatabase.EventRecord nayruEvent = nayruDatabase.Event;
        if (nayruEvent.NpcJumpSpeedZ != -0x200 || nayruEvent.NpcJumpGravity != 0x30 ||
            nayruEvent.DarkFadeFrames != 0x20 || nayruEvent.WhiteFadeOutFrames != 0x20 ||
            nayruEvent.PossessionFadeHoldFrames != 0x3c ||
            nayruEvent.WhiteFadeInFrames != 97 || nayruEvent.NayruAscentSpeedZ != -0x400 ||
            nayruEvent.NayruTransferZ != -0x8000 || nayruEvent.NayruLandingDelay != 0x1e ||
            nayruEvent.NayruFallSpeedZ != 0x40 || nayruEvent.NayruFallGravity != 0x20 ||
            nayruDatabase.DarkBackgroundPalettes.GetLength(0) != 6 ||
            nayruDatabase.Actor("RalphSword").Id != 0x5e ||
            nayruDatabase.Flee("Monkey").WaitJumpSpeedZ != -0x120 ||
            nayruDatabase.Flee("Rabbit").EscapeJumpSpeedZ != -0x200 ||
            !nayruDatabase.Flee("Boy").WaitForLanding ||
            nayruDatabase.Flee("Bird").EscapeGravity != 0 ||
            nayruDatabase.Effect("MusicNote").VelocityXFixed != 53 ||
            nayruDatabase.Effect("MusicNote").VelocityYFixed != -79 ||
            nayruDatabase.Vignette(0) != new NayruIntroEventDatabase.VignetteRecord(0, 0, 0x98, 937) ||
            nayruDatabase.Vignette(1) != new NayruIntroEventDatabase.VignetteRecord(1, 0, 0x5a, 600) ||
            nayruDatabase.Vignette(2) != new NayruIntroEventDatabase.VignetteRecord(2, 2, 0x0e, 645) ||
            nayruDatabase.VignetteMonkeys.Count != 10 ||
            nayruDatabase.VignetteMonkeys[8] !=
                new NayruIntroEventDatabase.VignetteMonkeyRecord(8, 0x50, 0x46, 180, 2) ||
            nayruDatabase.Actor("VignetteGuy").InitialAnimation != 3 ||
            nayruDatabase.Actor("VignetteGirl").InitialAnimation != 1 ||
            nayruDatabase.Actor("VignetteBoy").InitialAnimation != 1 ||
            nayruDatabase.Actor("Exclamation").Id != 0x9f ||
            nayruDatabase.PossessedSpritePalette.Length != 4 ||
            nayruDatabase.StoneSpritePalette.Length != 4 ||
            !nayruDatabase.StoneSpritePalette[2].IsEqualApprox(
                new Color(17.0f / 31.0f, 17.0f / 31.0f, 25.0f / 31.0f, 1.0f)) ||
            !nayruDatabase.PossessedSpritePalette[2].IsEqualApprox(
                new Color(3.0f / 31.0f, 13.0f / 31.0f, 27.0f / 31.0f, 1.0f)))
        {
            throw new InvalidOperationException(
                "The imported Ralph jump, PALH $99, audience escape, linked sword, white flash, " +
                "or Nayru portal-flight records changed.");
        }
        foreach ((string name, Vector2 position) in expectedActors)
        {
            NayruIntroEventDatabase.ActorRecord record = nayruDatabase.Actor(name);
            if (!actors.TryGetValue(name, out NpcCharacter? actor) || !actor.Active ||
                actors.NameOf(actor) != name ||
                actor.Position != position || actor.CurrentAnimationOpaquePixels == 0 ||
                actors.AnimationSource(name, record.InitialAnimation) !=
                    record.Animation(record.InitialAnimation) ||
                actor.CurrentScriptAnimationSource != record.Animation(record.InitialAnimation))
            {
                throw new InvalidOperationException(
                    $"objectData.nayruAndAnimalsInIntro actor {name} was missing, blank, " +
                    $"at {actor?.Position} instead of {position}, or not using initial " +
                    $"animation ${record.InitialAnimation:x2}.");
            }
        }
        StepRoomEventFrames(1);
        if (_sound.ActiveMusic != OracleSoundEngine.MusNayru ||
            _sound.MusicVolume != 3)
        {
            throw new InvalidOperationException(
                "INTERAC_NAYRU $36:$00 did not restore MUS_NAYRU to volume 3 on its first update.");
        }
        if (actors["Nayru"].SourceGraphicsWidth != 256)
        {
            throw new InvalidOperationException(
                "Nayru's short first sheet did not retain its full 128-pixel VRAM slot before spr_nayru_2.");
        }
        foreach ((string name, Vector2 position) in expectedActors)
        {
            if (!_entities.BlocksLink(position))
                throw new InvalidOperationException(
                    $"Dynamically generated Nayru gathering actor {name} has no Link collision.");
        }
        // The second note is created on phase 45 after effect movement runs. Give it
        // 18 movement updates so its rightward SPEED_60 path always exceeds the
        // global-frame sway, regardless of the frame phase established by earlier
        // validation scenarios. The first note remains inside its 70-update life.
        StepRoomEventFrames(63);
        List<NpcCharacter> singingNotes = _entities.Entities<NpcCharacter>()
            .Where(actor => actor.Name.ToString().StartsWith(
                "NayruIntroEffect_MusicNote", StringComparison.Ordinal))
            .ToList();
        NpcCharacter leftNote = singingNotes.SingleOrDefault(note =>
            note.Name.ToString().EndsWith("MusicNote0", StringComparison.Ordinal))!;
        NpcCharacter rightNote = singingNotes.SingleOrDefault(note =>
            note.Name.ToString().EndsWith("MusicNote1", StringComparison.Ordinal))!;
        if (nayruDatabase.Effect("MusicNote").SpriteName != "spr_common_sprites" ||
            nayruDatabase.Effect("MusicNote").TileBase != 0x44 ||
            !nayruTrace.Saw("NoteSpawn", value: 2) || singingNotes.Count != 2 ||
            singingNotes.Any(note => note.Record.SpriteName != "spr_common_sprites" ||
                note.Record.TileBase != 0x44) ||
            singingNotes.Any(note => !note.Active || note.CurrentAnimationOpaquePixels == 0) ||
            leftNote is null || rightNote is null ||
            leftNote.Position.X >= 0x78 - 6 || leftNote.Position.Y >= 0x18 - 4 ||
            rightNote.Position.X <= 0x78 + 8 || rightNote.Position.Y >= 0x18 - 4 ||
            !nayruTrace.Saw("NoteMotion", value: 0x01) ||
            !nayruTrace.Saw("NoteMotion", value: 0x02))
        {
            throw new InvalidOperationException(
                "Nayru's animation $04 did not create both visible music notes from " +
                "fixed bank-1 VRAM tile $44 in spr_common_sprites, with the original opposing " +
                "SPEED_60 paths and global-frame sway distinct from the snore Z at $40. " +
                $"count={singingNotes.Count}, names/positions=" +
                string.Join(", ", singingNotes.Select(note =>
                    $"{note.Name}:{note.Position}:tile{note.Record.TileBase}:" +
                    $"active={note.Active}:pixels={note.CurrentAnimationOpaquePixels}")) +
                $", motion={nayruTrace.OrValues("NoteMotion"):x2}.");
        }

        NpcCharacter bird = actors["Bird"];
        _player.WarpTo(bird.Position + Vector2.Left * 16, recordSafe: false);
        _player.Face(Vector2I.Right);
        if (!TryInteract(_player) || !_dialogue.IsOpen ||
            _dialogue.CurrentMessage != "No! I have to\nhear Nayru's\nsong!" ||
            nayruTrace.LastValue("AudienceMask") != 0x01 ||
            bird.CurrentScriptAnimationSource != nayruDatabase.Actor("Bird").Animation(2))
        {
            throw new InvalidOperationException(
                "The intro bird did not route TX_3214 through normal NPC interaction or select " +
                "its cplinkx+$02 left-facing talk animation.");
        }
        StepRoomEventFrames(1);
        if (bird.ScriptDrawOffset.Y >= 0)
            throw new InvalidOperationException(
                "The intro bird did not begin its repeating -$00c0/$0020 talk hop.");
        _dialogue.Close();
        StepRoomEventFrames(1);
        if (bird.ScriptDrawOffset != Vector2.Zero ||
            bird.CurrentScriptAnimationSource != nayruDatabase.Actor("Bird").Animation(2))
        {
            throw new InvalidOperationException(
                "The intro bird did not stop hopping and hold its talk pose for the post-text wait.");
        }
        StepRoomEventFrames(10);
        if (bird.CurrentScriptAnimationSource != nayruDatabase.Actor("Bird").Animation(1))
            throw new InvalidOperationException(
                "The intro bird did not restore animation $01 after its 10-update post-text wait.");

        NpcCharacter rabbit = actors["Rabbit"];
        _player.WarpTo(rabbit.Position + Vector2.Right * 16, recordSafe: false);
        if (!nayruIntro.TryInteractNpc(rabbit) ||
            !_dialogue.CurrentMessage.StartsWith("♪La la li li la♪", StringComparison.Ordinal) ||
            nayruTrace.LastValue("AudienceMask") != 0x03 ||
            rabbit.CurrentScriptAnimationSource != nayruDatabase.Actor("Rabbit").Animation(1))
        {
            throw new InvalidOperationException(
                "The rabbit did not face Link through turnToFaceLink, set audience bit $02, " +
                "or decode TX_5705's music symbols.");
        }
        _dialogue.Close();
        StepRoomEventFrames(11);

        NpcCharacter boy = actors["Boy"];
        _player.WarpTo(boy.Position + Vector2.Down * 16, recordSafe: false);
        if (!nayruIntro.TryInteractNpc(boy) ||
            boy.CurrentScriptAnimationSource != nayruDatabase.Actor("Boy").Animation(2))
        {
            throw new InvalidOperationException(
                "The intro boy did not use turnToFaceLink's down-facing animation.");
        }
        _dialogue.Close();
        StepRoomEventFrames(11);

        NpcCharacter monkey = actors["Monkey"];
        _player.WarpTo(monkey.Position + Vector2.Right * 16, recordSafe: false);
        if (!nayruIntro.TryInteractNpc(monkey) ||
            monkey.CurrentScriptAnimationSource != nayruDatabase.Actor("Monkey").Animation(1))
        {
            throw new InvalidOperationException(
                "The intro monkey did not use cplinkx's right-facing animation.");
        }
        _dialogue.Close();
        StepRoomEventFrames(21);
        if (nayruTrace.LastValue("AudienceMask") != 0x0f ||
            !nayruIntro.TryInteractNpc(actors["Bear"]) ||
            nayruTrace.LastValue("AudienceMask") != 0x1f ||
            nayruIntro.CurrentStage != 2 || !_roomEvents.Active ||
            !_player.CutsceneControlled)
        {
            throw new InvalidOperationException(
                "The $01/$02/$04/$08 audience bits did not unlock the bear's $10 lead-in.");
        }

        StepRoomEventFrames(20 + 16);
        if (actors["Bear"].Position != new Vector2(0x58, 0x30) ||
            actors["Bear"].CurrentScriptAnimationSource !=
                nayruDatabase.Actor("Bear").Animation(1))
        {
            throw new InvalidOperationException(
                "The bear's raw angle-$00 movement did not preserve its explicit right-facing animation $01.");
        }
        StepRoomEventFrames(16 + 50);
        if (!_dialogue.IsOpen || _dialogue.CurrentMessage !=
            "Sit here and\nlisten. How\ncharming..." ||
            actors["Bear"].Position != new Vector2(0x58, 0x28) ||
            !_saveData.HasRoomFlag(group, roomId, OracleSaveData.RoomFlag80))
        {
            throw new InvalidOperationException(
                "The bear did not wait 20, move upward for 32, settle for 50, and set room flag $80.");
        }
        _dialogue.Close();
        StepRoomEventFrames(1);
        if (_roomEvents.Active || _player.CutsceneControlled ||
            nayruIntro.CurrentStage != 1)
        {
            throw new InvalidOperationException("The bear lead-in did not restore Link control.");
        }

        _sound.ClearPlayRequestAudit();
        _player.WarpTo(new Vector2(0x60, 0x3d), recordSafe: false);
        StepRoomEventFrames(1);
        if (nayruIntro.CurrentStage != 6 || nayruIntro.Counter != 120 ||
            !_player.CutsceneControlled)
        {
            throw new InvalidOperationException(
                "The x>=$60/y<$3e initial Nayru cutscene boundary did not install the 120-update wait.");
        }
        StepRoomEventFrames(120);
        if (!_dialogue.IsOpen || _dialogue.CurrentMessage != "Isn't it \nenchanting?")
            throw new InvalidOperationException("TX_5706 did not follow the bear's 120-update wait.");
        _dialogue.Close();
        StepRoomEventFrames(1);
        StepRoomEventFrames(30);
        if (nayruIntro.CurrentStage != 9 || nayruIntro.Counter != 11)
            throw new InvalidOperationException("The post-TX_5706 30-update delay did not begin the fast fade.");
        StepRoomEventFrames(11);
        NayruSingingScreen? singing =
            _scene.InterfaceLayer.GetNodeOrNull<NayruSingingScreen>("NayruSingingScreen");
        if (nayruIntro.CurrentStage != 10 || singing is null || singing.ScrollX != 0 ||
            _hud.Visible ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndCloseMenu) != 1)
        {
            throw new InvalidOperationException(
                "GFXH_NAYRU_SINGING_CUTSCENE did not play SND_CLOSEMENU and replace the " +
                "room/HUD after 11 updates.");
        }
        StepRoomEventFrames(320);
        if (singing.ScrollX != 40 || nayruIntro.CurrentStage != 10)
        {
            throw new InvalidOperationException(
                "The singing still did not perform 40 one-pixel scrolls at eight-update intervals.");
        }
        StepRoomEventFrames(280);
        StepRoomEventFrames(11);
        if (nayruIntro.CurrentStage != 12 || !_hud.Visible ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndCloseMenu) != 2)
        {
            throw new InvalidOperationException(
                "The 600-update singing screen did not replay SND_CLOSEMENU and enter the room script.");
        }

        int scriptFrames = 0;
        int observedVignettes = 0;
        int ralphFallFrames = 0;
        bool sawStaticFallenRalph = false;
        bool sawVisibleLightning = false;
        bool sawVisibleSwordGift = false;
        bool sawSwordPickupPose = false;
        bool sawHudDuringVignetteSequence = false;
        bool hudHiddenDuringVignetteSequence = false;
        bool sawLinkFaceNayru = false;
        bool sawLinkFaceNayruSecond = false;
        bool sawLinkFaceRalph = false;
        bool sawLinkFaceImpaAfterReveal = false;
        bool sawNayruStopSingingForRalph = false;
        bool sawRalphAirborne = false;
        bool sawDarkPalette = false;
        bool sawAudienceAirborne = false;
        bool sawBoyShockDoubleCadence = false;
        bool sawBoyEscapeNormalCadence = false;
        bool sawVeranReactionMovement = false;
        bool sawPossessionFlash = false;
        bool sawRalphSword = false;
        bool sawNayruAscent = false;
        bool sawNayruDescent = false;
        bool sawNeutralLinkDuringVeranSpeech = false;
        bool sawSideviewMusic = false;
        bool sawRoomOfRitesMusic = false;
        bool sawVignetteRestartSilence = false;
        bool sawDisasterMusic = false;
        bool sawSadnessMusic = false;
        string swordMessage = DialogueBox.PlainText(nayruDatabase.Text(0x001c).Message);
        string nayruGreeting = DialogueBox.PlainText(nayruDatabase.Text(0x1d00).Message);
        string nayruSecondGreeting = DialogueBox.PlainText(nayruDatabase.Text(0x1d22).Message);
        string ralphIntroduction = DialogueBox.PlainText(nayruDatabase.Text(0x2a00).Message);
        string ralphReply = DialogueBox.PlainText(nayruDatabase.Text(0x2a22).Message);
        string veranAgeSpeech = DialogueBox.PlainText(nayruDatabase.Text(0x5605).Message);
        string nayruDownAnimation = nayruDatabase.Actor("Nayru").Animation(2);
        string impaRevealAnimation = nayruDatabase.Actor("AftermathImpa").Animation(4);
        string ralphFallAnimation = nayruDatabase.Actor("AftermathRalph").Animation(8);
        while (!_saveData.HasGlobalFlag(OracleSaveData.GlobalFlagIntroDone) &&
            scriptFrames < 20000)
        {
            NayruActorRegistry currentActors = nayruIntro.ActorRegistry;
            int visitedVignettes = nayruTrace.OrValues("VignetteVisited");
            sawSideviewMusic |= _sound.ActiveMusic == OracleSoundEngine.MusLadxSideview;
            sawRoomOfRitesMusic |= _sound.ActiveMusic == OracleSoundEngine.MusRoomOfRites;
            sawVignetteRestartSilence |= nayruIntro.CurrentVignetteIndex == 0 &&
                nayruIntro.VignetteElapsed is >= 1 and <= 120 && _sound.ActiveMusic == 0;
            sawDisasterMusic |= visitedVignettes != 0 &&
                !currentActors.ContainsKey("AftermathRalph") &&
                _sound.ActiveMusic == OracleSoundEngine.MusDisaster;
            sawSadnessMusic |= currentActors.ContainsKey("AftermathRalph") &&
                _sound.ActiveMusic == OracleSoundEngine.MusSadness;
            if (visitedVignettes != 0 && _roomEvents.Active)
            {
                sawHudDuringVignetteSequence |= _hud.Visible;
                hudHiddenDuringVignetteSequence |= !_hud.Visible;
            }
            if (_dialogue.IsOpen && _dialogue.CurrentMessage == nayruGreeting &&
                _player.FacingVector == Vector2I.Up)
                sawLinkFaceNayru = true;
            if (_dialogue.IsOpen && _dialogue.CurrentMessage == nayruSecondGreeting &&
                _player.FacingVector == Vector2I.Up)
                sawLinkFaceNayruSecond = true;
            if (_dialogue.IsOpen && _dialogue.CurrentMessage == ralphReply &&
                _player.FacingVector == Vector2I.Right)
                sawLinkFaceRalph = true;
            if (_dialogue.IsOpen && _dialogue.CurrentMessage == veranAgeSpeech &&
                !_player.Walking)
            {
                sawNeutralLinkDuringVeranSpeech = true;
            }
            if (_dialogue.IsOpen && _dialogue.CurrentMessage == ralphIntroduction &&
                currentActors.TryGetValue("Nayru", out NpcCharacter? listeningNayru) &&
                listeningNayru.CurrentScriptAnimationSource == nayruDownAnimation)
            {
                sawNayruStopSingingForRalph = true;
            }
            if (currentActors.TryGetValue("Impa", out NpcCharacter? revealingImpa) &&
                revealingImpa.Active &&
                revealingImpa.CurrentScriptAnimationSource == impaRevealAnimation &&
                _player.FacingVector == Vector2I.Down)
            {
                sawLinkFaceImpaAfterReveal = true;
            }
            if (currentActors.TryGetValue("Ralph", out NpcCharacter? introRalph) &&
                introRalph.ScriptDrawOffset.Y < 0)
                sawRalphAirborne = true;
            sawDarkPalette |= _currentRoom.TemporaryBackgroundPaletteBlend >= 1.0f;
            foreach (string audienceName in new[] { "Monkey", "Rabbit", "Boy", "Bird" })
            {
                if (currentActors.TryGetValue(audienceName, out NpcCharacter? audience) &&
                    audience.ScriptDrawOffset.Y < 0)
                    sawAudienceAirborne = true;
            }
            if (currentActors.TryGetValue("Boy", out NpcCharacter? shockedBoy))
            {
                if (shockedBoy.CurrentScriptAnimationSource ==
                        nayruDatabase.Actor("Boy").Animation(2) &&
                    Mathf.IsEqualApprox(shockedBoy.AnimationRate, 2.0f))
                {
                    sawBoyShockDoubleCadence = true;
                }
                if (sawBoyShockDoubleCadence && shockedBoy.ScriptDrawOffset.Y < 0 &&
                    Mathf.IsEqualApprox(shockedBoy.AnimationRate, 1.0f))
                {
                    sawBoyEscapeNormalCadence = true;
                }
            }
            if (currentActors.ContainsKey("GhostVeran") && _scene.WarpFade.Color.A >= 0.99f)
                sawPossessionFlash = true;
            if (currentActors.TryGetValue("RalphSword", out NpcCharacter? ralphSword) &&
                ralphSword.Active && ralphSword.CurrentAnimationOpaquePixels > 0)
                sawRalphSword = true;
            if (currentActors.TryGetValue("Nayru", out NpcCharacter? flyingNayru) &&
                flyingNayru.ScriptDrawOffset.Y < 0)
            {
                if (flyingNayru.Position.X == 0x78)
                    sawNayruAscent = true;
                if (flyingNayru.Position == new Vector2(0x28, 0x38))
                    sawNayruDescent = true;
            }
            foreach (NpcCharacter effect in _entities.Entities<NpcCharacter>())
            {
                if (effect.Name.ToString().StartsWith(
                        "NayruIntroEffect_Lightning", StringComparison.Ordinal) &&
                    effect.Active && effect.CurrentAnimationOpaquePixels > 0)
                {
                    sawVisibleLightning = true;
                }
            }
            ChestTreasureEffect? swordGift =
                _scene.WorldRoot.GetNodeOrNull<ChestTreasureEffect>("NayruSwordGift");
            if (_dialogue.IsOpen && _dialogue.CurrentMessage == swordMessage &&
                swordGift is not null)
            {
                sawVisibleSwordGift = true;
                sawSwordPickupPose |= _player.IsHoldingItemOneHand &&
                    swordGift.Position == _player.Position + new Vector2(-4, -14);
            }
            if (_dialogue.IsOpen)
                _dialogue.Close();
            StepRoomEventFrames(1);
            scriptFrames++;

            int newVignettes = visitedVignettes & ~observedVignettes;
            if (newVignettes != 0)
            {
                (int Group, int Room) expected = newVignettes switch
                {
                    1 => (0, 0x98),
                    2 => (0, 0x5a),
                    4 => (2, 0x0e),
                    _ => throw new InvalidOperationException(
                        $"Multiple Nayru vignettes advanced on one update (${newVignettes:x2}).")
                };
                if (_rooms.ActiveGroup != expected.Group ||
                    _rooms.CurrentRoom.Id != expected.Room)
                {
                    throw new InvalidOperationException(
                        $"Nayru vignette ${newVignettes:x2} showed " +
                        $"{_rooms.ActiveGroup:x1}:{_rooms.CurrentRoom.Id:x2} instead of " +
                        $"{expected.Group:x1}:{expected.Room:x2}.");
                }
                observedVignettes |= newVignettes;
            }

            if (nayruIntro.ActorRegistry.TryGetValue(
                    "AftermathRalph", out NpcCharacter? aftermathRalph) &&
                aftermathRalph.Active &&
                aftermathRalph.CurrentScriptAnimationSource == ralphFallAnimation)
            {
                ralphFallFrames++;
                if (ralphFallFrames > 60)
                {
                    sawStaticFallenRalph = true;
                    if (aftermathRalph.CurrentAnimationFrame != 9)
                        throw new InvalidOperationException(
                            "Ralph's animation $08 restarted its falling frames during TX_2a03.");
                }
            }
        }
        CutsceneCommandTraceEntry[] nayruStarts = nayruTrace.Entries
            .Where(entry => entry.Phase == CutsceneCommandTracePhase.Started)
            .ToArray();
        int importedTranslateCount = nayruDatabase.Commands
            .Count(command => command is CutsceneTranslateCommand or
                CutsceneParallelTranslateCommand);
        int startedTranslateCount = nayruStarts.Count(entry =>
            entry.Source.Opcode is "translate" or "paralleltranslate");
        int linkVeranFacingMask = nayruTrace.OrValues("LinkVeranFacing");
        int ralphVeranFacingMask = nayruTrace.OrValues("RalphVeranFacing");
        int ghostTrackingPhases = nayruTrace.OrValues("GhostTrackingPhase");
        sawVeranReactionMovement =
            nayruTrace.SawPosition(
                "ActorPosition", "Player", new Vector2(0x57, 0x30)) &&
            nayruTrace.SawPosition(
                "ActorPosition", "Ralph", new Vector2(0x88, 0x51));
        bool movementFacingShown =
            startedTranslateCount == importedTranslateCount &&
            nayruTrace.Saw("VignetteMovement", "VignetteGirl", 0) &&
            nayruTrace.Saw("VignetteMovement", "VignetteBoy", 1) &&
            nayruTrace.Saw("VignetteMovement", "VignetteBoy", 3) &&
            nayruTrace.Saw("VignetteMovement", "VignetteLady", 2) &&
            nayruTrace.Saw("VignetteMovement", "VignetteLady", 3);
        bool vignetteDetailShown =
            nayruTrace.Saw("VignetteGirlJump") &&
            nayruTrace.Saw("VignetteMonkeyHop") &&
            nayruTrace.Saw("VignetteMonkeyPacing") &&
            nayruTrace.Saw("VignetteMonkeyStone") &&
            nayruTrace.Saw("VignetteMonkeyFlicker") &&
            nayruTrace.Saw("VignetteBoyPalette") &&
            nayruTrace.Saw("VignetteLadyCadence") &&
            nayruTrace.Count("VignetteExclamation") == 3;
        bool completeCommandTrace = nayruStarts.Length == nayruDatabase.Commands.Count &&
            nayruStarts.Select(entry => entry.Source.CommandIndex)
                .SequenceEqual(Enumerable.Range(0, nayruDatabase.Commands.Count));
        int rumbleRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndRumble2);
        bool exactCutsceneSounds =
            _sound.PlayRequestsFor(OracleSoundEngine.SndCloseMenu) == 2 &&
            _sound.PlayRequestsFor(OracleSoundEngine.SndJump) == 4 &&
            _sound.PlayRequestsFor(OracleSoundEngine.SndBossDead) == 1 &&
            _sound.PlayRequestsFor(OracleSoundEngine.SndUnknown5) == 4 &&
            _sound.PlayRequestsFor(OracleSoundEngine.SndSwordSpin) == 8 &&
            _sound.PlayRequestsFor(OracleSoundEngine.SndTeleport) == 2 &&
            _sound.PlayRequestsFor(OracleSoundEngine.SndSwordObtained) == 2 &&
            rumbleRequests is >= 13 and <= 15 &&
            _sound.PlayRequestsFor(OracleSoundEngine.SndKillEnemy) == 1 &&
            _sound.PlayRequestsFor(OracleSoundEngine.SndSwordSlash) == 1 &&
            _sound.PlayRequestsFor(OracleSoundEngine.SndLightning) == 6 &&
            _sound.PlayRequestsFor(OracleSoundEngine.SndSlash) == 1 &&
            _sound.PlayRequestsFor(OracleSoundEngine.SndWarpStart) == 1 &&
            _sound.PlayRequestsFor(OracleSoundEngine.SndClink) == 10 &&
            _sound.PlayRequestsFor(OracleSoundEngine.SndBoomerang) == 1 &&
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetItem) == 1;
        if (!_saveData.HasGlobalFlag(OracleSaveData.GlobalFlagIntroDone) ||
            !_saveData.HasRoomFlag(group, roomId, OracleSaveData.RoomFlag40) ||
            _currentRoom.GetMetatile(portalPoint) != 0xd7 ||
            nayruIntro.CurrentStage != 0 || nayruIntro.ActorRegistry.Count != 0 ||
            _roomEvents.Active || _player.CutsceneControlled || !_hud.Visible ||
            _rooms.ActiveGroup != group || _rooms.CurrentRoom.Id != roomId ||
            observedVignettes != 0x07 || nayruTrace.Count("LightningSpawn") != 6 ||
            !sawVisibleLightning || !nayruTrace.Saw("CollapsedImpaRendered") ||
            !nayruTrace.SawPosition(
                "ActorPosition", "Nayru", new Vector2(0x78, 0x20)) ||
            !nayruTrace.Saw("SwordGift") || !sawVisibleSwordGift ||
            !sawSwordPickupPose || _player.IsHoldingItemOneHand ||
            !sawHudDuringVignetteSequence || hudHiddenDuringVignetteSequence ||
            !sawStaticFallenRalph || !sawLinkFaceNayru || !sawLinkFaceNayruSecond ||
            !sawLinkFaceRalph || !sawLinkFaceImpaAfterReveal ||
            !sawNeutralLinkDuringVeranSpeech ||
            !sawNayruStopSingingForRalph ||
            !sawRalphAirborne || nayruTrace.Count("RalphJump") != 2 ||
            !sawDarkPalette || !nayruTrace.Saw("DarkPalette") ||
            !sawAudienceAirborne || !nayruTrace.Saw("AudienceAirborne") ||
            !sawBoyShockDoubleCadence || !sawBoyEscapeNormalCadence ||
            !nayruTrace.Saw("BoyEscapeStarted") || !nayruTrace.Saw("BoyEscaped") ||
            ghostTrackingPhases != 0x3c ||
            linkVeranFacingMask != 0x0f || ralphVeranFacingMask != 0x0d ||
            !sawVeranReactionMovement || !sawPossessionFlash ||
            !sawRalphSword || !nayruTrace.Saw("RalphSwordVisible") ||
            !nayruTrace.SawPosition(
                "ActorPosition", "Nayru", new Vector2(0x78, 0x18)) ||
            !nayruTrace.Saw("GhostHiddenAfterPossession") ||
            !nayruTrace.Saw("PostChargeFacing") ||
            !nayruTrace.Saw("PossessionSway") ||
            !nayruTrace.Saw("PossessionBlink") ||
            !nayruTrace.Saw("PossessionMovementSync") ||
            !nayruTrace.Saw("GhostEmergence") ||
            !nayruTrace.Saw("RalphSwordSpacing") ||
            !nayruTrace.Saw("AftermathLinkWalk") ||
            !movementFacingShown || !vignetteDetailShown ||
            (nayruTrace.OrValues("AftermathRalphFacing") & 0x07) != 0x07 ||
            !sawNayruAscent || !sawNayruDescent ||
            !nayruTrace.Saw("PortalFlight") || !completeCommandTrace ||
            !sawSideviewMusic || !sawRoomOfRitesMusic ||
            !sawVignetteRestartSilence || !sawDisasterMusic || !sawSadnessMusic ||
            !exactCutsceneSounds ||
            _sound.ActiveMusic != OracleSoundEngine.MusOverworld ||
            _sound.MusicVolume != 3 ||
            !_inventory.HasTreasure(TreasureDatabase.TreasureSword) ||
            _inventory.SwordLevel != 1 || !_inventoryMenu.CanOpenForValidation ||
            !_mapMenu.CanOpenNormalForValidation ||
            scriptFrames >= 20000)
        {
            throw new InvalidOperationException(
                $"The Nayru possession/portal/vignette/aftermath sequence did not complete " +
                $"(frames={scriptFrames}, stage={nayruIntro.CurrentStage}, " +
                $"faces={sawLinkFaceNayru}/{sawLinkFaceNayruSecond}/" +
                $"{sawLinkFaceRalph}/{sawLinkFaceImpaAfterReveal}, " +
                $"neutralLink={sawNeutralLinkDuringVeranSpeech}, " +
                $"boy={sawBoyShockDoubleCadence}/{sawBoyEscapeNormalCadence}/" +
                $"{nayruTrace.Saw("BoyEscapeStarted")}/{nayruTrace.Saw("BoyEscaped")}" +
                $", track={ghostTrackingPhases:x2}:" +
                $"{linkVeranFacingMask:x2}/{ralphVeranFacingMask:x2}, " +
                $"ghostHidden={nayruTrace.Saw("GhostHiddenAfterPossession")}, " +
                $"listen/down={sawNayruStopSingingForRalph}/" +
                $"{nayruTrace.Saw("PostChargeFacing")}, " +
                $"possession={nayruTrace.Saw("PossessionSway")}/" +
                $"{nayruTrace.Saw("PossessionBlink")}/" +
                $"{nayruTrace.Saw("PossessionMovementSync")}/" +
                $"{nayruTrace.Saw("GhostEmergence")}, " +
                $"swordSpace={nayruTrace.Saw("RalphSwordSpacing")}, " +
                $"moveFacing={movementFacingShown}, vignette={vignetteDetailShown}, " +
                $"hud={sawHudDuringVignetteSequence}/{hudHiddenDuringVignetteSequence}, " +
                $"ralphTracking={nayruTrace.OrValues("AftermathRalphFacing"):x2}, " +
                $"linkWalk={nayruTrace.Saw("AftermathLinkWalk")}, " +
                $"reaction={sawVeranReactionMovement}, flash={sawPossessionFlash}, " +
                $"sword={sawRalphSword}/{nayruTrace.Saw("RalphSwordVisible")}/" +
                $"{sawSwordPickupPose}/{_player.IsHoldingItemOneHand}, " +
                $"flight={sawNayruAscent}/{sawNayruDescent}/" +
                $"{nayruTrace.Saw("PortalFlight")}, trace={nayruStarts.Length}/" +
                $"{nayruDatabase.Commands.Count}, " +
                $"sfx={exactCutsceneSounds}:jump" +
                $"{_sound.PlayRequestsFor(OracleSoundEngine.SndJump)}/" +
                $"spin{_sound.PlayRequestsFor(OracleSoundEngine.SndSwordSpin)}/" +
                $"clink{_sound.PlayRequestsFor(OracleSoundEngine.SndClink)}/" +
                $"lightning{_sound.PlayRequestsFor(OracleSoundEngine.SndLightning)}/" +
                $"rumble{rumbleRequests}, " +
                $"music={sawSideviewMusic}/{sawRoomOfRitesMusic}/" +
                $"{sawVignetteRestartSilence}/{sawDisasterMusic}/{sawSadnessMusic}/" +
                $"{_sound.ActiveMusic:x2}:{_sound.MusicVolume}).");
        }
        _entities.Update(1.0 / 60.0, _player);
        TimePortal? portal = _entities.Entities<TimePortal>().SingleOrDefault();
        if (portal is null || !portal.Active)
            throw new InvalidOperationException("Lightning tile $22=$d7 did not activate portal $e1:$01.");

        _currentRoom.ReplaceMetatile(portalPoint, 0xd7, 0x3a, (long)_animationTicks);
        LoadValidationRoom(group, roomId);
        _entities.Update(1.0 / 60.0, _player);
        portal = _entities.Entities<TimePortal>().SingleOrDefault();
        if (_currentRoom.GetMetatile(portalPoint) != 0xd7 || portal is null || !portal.Active ||
            nayruIntro.CurrentStage != 0)
        {
            throw new InvalidOperationException(
                "Room flag $40 did not restore the opened portal without retriggering the intro.");
        }

        // Leave the cached room in its imported state for the independent portal validation.
        _currentRoom.ReplaceMetatile(portalPoint, 0xd7, 0x3a, (long)_animationTicks);
        _saveData.SetRoomFlag(group, roomId, OracleSaveData.RoomFlag40, value: false);
        _saveData.SetRoomFlag(group, roomId, OracleSaveData.RoomFlag80, value: false);
        _roomEvents.CommandTraceSink = null;
        GD.Print("Validated room 0:39's pre-GLOBALFLAG_INTRO_DONE $6b:$01 audience, " +
            "$01/$02/$04/$08/$10 talk mask, cplinkx/turnToFaceLink talk facings, bird " +
            "$02/$03 hop and exact pose resets, bear room flag $80 movement, $60/$3e trigger, " +
            "solid dynamic actors and outgoing scrolling, visible singing notes, 120/30/600 " +
            "timing, imported singing OAM and 40-pixel scroll, opposing SPEED_60 notes, all " +
            "Link/Nayru/Ralph/Impa dialogue, neutral Link holds after translated movement, " +
            "and cfd5/cfd6 ghost-facing handoffs with Link's " +
            "8-update and Ralph's 16-update cadence, Nayru's held $02, cached collision target, " +
            "clockwise diagonal rounding and cfd2 left turn, opcode-driven movement " +
            "facings with preserved raw-angle backwalk poses, aftermath Ralph tracking, two Ralph jumps, PALH $99 " +
            "darkening, the boy's $0e-$10 double animation cadence and normal-speed jumping escape, " +
            "boy-inclusive audience escapes, Link/Ralph reaction movement, " +
            "Nayru's stopped singing/down-facing backstep, possession white flash, exact " +
            "palette blink/sway and 150/220-start offset, hand-raised ghost emergence, spaced " +
            "linked Ralph sword, animated aftermath Link walking, Nayru's ascent/landing, fainted Impa, " +
            "portal/vignette lightning, exact $98/$5a/2:$0e room swaps and 937/600/645-update " +
            "actor scripts with jumps, pacing, stone palettes, flicker, and exclamation marks, one-shot Ralph fall, " +
            "visible sword handoff, Fairy Fountain/Nayru/sideview/Room of Rites/" +
            "vignette-stop/Disaster/Sadness/room-music cue chain, " +
            "all actor/part/treasure SFX calls and repeated global-frame cues, " +
            "$22=$d7/flag $40, aftermath, and persistent completion.");
    }

    private void ValidateRalphPortalDepartureEvent()
    {
        RalphPortalEvent ralphEvent = _roomEvents.Ralph;
        var commandTrace = new ValidationCutsceneTrace();
        _roomEvents.CommandTraceSink = commandTrace;
        _sound.ClearPlayRequestAudit();
        // @initSubid0d deletes the object on a direct room load because
        // wScreenTransitionDirection is not DIR_RIGHT ($01).
        LoadValidationRoom(0, 0x39);
        NpcCharacter? directRalph = _npcNodes.Find(npc =>
            npc.Record.Id == 0x37 && npc.Record.SubId == 0x0d);
        if (directRalph is null || directRalph.Active || _roomEvents.Active)
        {
            throw new InvalidOperationException(
                "INTERAC_RALPH $37:$0d ignored its DIR_RIGHT room-entry guard.");
        }

        LoadValidationRoom(0, 0x38);
        _transitions.BeginScroll(_player, Vector2I.Right, 0x39);
        NpcCharacter? ralph = _npcNodes.Find(npc =>
            npc.Record.Id == 0x37 && npc.Record.SubId == 0x0d);
        if (ralph is null || !ralph.Active || !_roomEvents.Active ||
            !ralphEvent.WaitingForScroll || !_entities.ScreenTransitionActive ||
            ralph.Position != new Vector2(0x18, 0x28))
        {
            throw new InvalidOperationException(
                "Room 0:39 did not retain Ralph at $28/$18 while entering from the left.");
        }

        int ralphScrollFrames = FinishActiveScrollingTransitionWithRoomEventsForValidation();
        if (ralphScrollFrames != 40)
            throw new InvalidOperationException(
                $"The 0:38 -> 0:39 horizontal scroll took {ralphScrollFrames} updates, expected 40.");
        if (!_player.CutsceneControlled || ralphEvent.Counter != 40)
            throw new InvalidOperationException(
                "Ralph's destination event fast-forwarded instead of installing its full " +
                "40-update wait after scrolling.");
        StepRoomEventFrames(39);
        if (_dialogue.IsOpen || ralphEvent.Counter != 1)
            throw new InvalidOperationException("Ralph's introductory wait ended early.");
        StepRoomEventFrames(1);
        if (!_dialogue.IsOpen || _dialogue.CurrentMessage !=
            "The Maku Tree?\nThis is more\nof Veran's work!\nLink! You made\nit! Veran just\nleapt through\nthis Time\nPortal! If we go\nback in time, we\nshould be able\nto save Nayru\nand the Maku\nTree! I'm\ncoming, Nayru!")
        {
            throw new InvalidOperationException(
                "TX_2a1e did not open after Ralph's original 40-update wait.");
        }

        _dialogue.Close();
        StepRoomEventFrames(1);
        StepRoomEventFrames(29);
        if (ralphEvent.Counter != 1 || ralph.Position != new Vector2(0x18, 0x28))
            throw new InvalidOperationException("Ralph's post-text 30-update wait ended early.");
        StepRoomEventFrames(1);
        if (ralph.CurrentAnimationFrame != 0)
            throw new InvalidOperationException(
                "Ralph did not select animation $01 after the post-text wait.");
        StepRoomEventFrames(2);
        if (ralph.Position != new Vector2(0x18, 0x28))
            throw new InvalidOperationException(
                "Ralph moved during the setspeed/setangle script-command updates.");
        StepRoomEventFrames(1);
        if (ralphEvent.Counter != 17 || ralph.Position != new Vector2(0x18, 0x28))
            throw new InvalidOperationException(
                "Ralph did not install applyspeed counter $11 on its own script update.");

        StepRoomEventFrames(12);
        if (ralph.Position != new Vector2(0x24, 0x28) ||
            ralph.CurrentAnimationFrame != 0 || ralphEvent.Counter != 5)
        {
            throw new InvalidOperationException(
                "Ralph's SPEED_100 movement or animation $01 first-frame duration diverged.");
        }
        StepRoomEventFrames(1);
        if (ralph.Position != new Vector2(0x25, 0x28) ||
            ralph.CurrentAnimationFrame != 1 || ralphEvent.Counter != 4)
        {
            throw new InvalidOperationException(
                "Ralph's animation $01 did not change after its original 16 updates.");
        }
        StepRoomEventFrames(2);
        if (ralph.Position != new Vector2(0x27, 0x28) || ralphEvent.Counter != 2)
            throw new InvalidOperationException("Ralph's SPEED_100 movement skipped an update.");
        StepRoomEventFrames(1);
        if (ralph.Position != new Vector2(0x28, 0x28) ||
            ralphEvent.Counter != 1)
        {
            throw new InvalidOperationException(
                "applyspeed $11 did not move Ralph exactly 16 pixels to the portal.");
        }
        StepRoomEventFrames(1);
        if (ralphEvent.Counter != 0 || ralphEvent.Flickering ||
            ralph.Position != new Vector2(0x28, 0x28))
        {
            throw new InvalidOperationException(
                "Ralph's counter2 path did not pause for one update after reaching zero.");
        }
        StepRoomEventFrames(1);
        if (ralph.CurrentAnimationFrame != 0 || ralphEvent.Flickering)
            throw new InvalidOperationException("Ralph did not select portal animation $09.");
        StepRoomEventFrames(2);
        if (ralphEvent.Counter != 45 || ralphEvent.Flickering ||
            _sound.LastPlayRequest != OracleSoundEngine.SndMysterySeed ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndMysterySeed) != 1)
        {
            throw new InvalidOperationException(
                "Ralph's var3f=$2d and SND_MYSTERY_SEED commands lost their script updates.");
        }
        StepRoomEventFrames(1);
        bool firstFlickerVisibility = (_entities.FrameCounter & 1) != 0;
        if (!ralphEvent.Flickering || ralphEvent.Counter != 44 ||
            ralph.CurrentAnimationFrame != 0 || ralph.Visible != firstFlickerVisibility ||
            ralphEvent.Completed)
        {
            throw new InvalidOperationException(
                "Ralph did not select animation $09 and begin the $2d-frame parity flicker.");
        }
        StepRoomEventFrames(1);
        if (ralph.Visible == firstFlickerVisibility || ralphEvent.Counter != 43)
            throw new InvalidOperationException(
                "Ralph's objectFlickerVisibility b=$01 did not alternate every update.");
        StepRoomEventFrames(42);
        if (!_roomEvents.Active || ralphEvent.Counter != 1 ||
            ralphEvent.Completed || !ralph.Active)
        {
            throw new InvalidOperationException(
                "Ralph's $2d-frame portal flicker completed one update early.");
        }
        StepRoomEventFrames(1);
        if (_roomEvents.Active || !ralphEvent.Completed || ralph.Active ||
            _player.CutsceneControlled ||
            _sound.ActiveMusic != OracleSoundEngine.MusOverworld ||
            !_saveData.HasGlobalFlag(OracleSaveData.GlobalFlagRalphEnteredPortal))
        {
            throw new InvalidOperationException(
                "Ralph's departure did not set GLOBALFLAG_RALPH_ENTERED_PORTAL $40 and restore input.");
        }

        LoadValidationRoom(0, 0x39);
        NpcCharacter? completedRalph = _npcNodes.Find(npc =>
            npc.Record.Id == 0x37 && npc.Record.SubId == 0x0d);
        if (completedRalph is null || completedRalph.Active || _roomEvents.Active)
            throw new InvalidOperationException("Ralph's one-shot portal event retriggered after flag $40.");

        CutsceneCommandTraceEntry[] starts = commandTrace.Entries
            .Where(entry => entry.Phase == CutsceneCommandTracePhase.Started)
            .ToArray();
        string[] expectedOpcodes =
        {
            "disableinput", "wait", "showtext", "wait", "setanimation",
            "setspeed", "setangle", "applyspeed", "setanimation",
            "writeobjectbyte", "playsound", "flicker", "setglobalflag",
            "native", "enableinput", "scriptend"
        };
        if (starts.Length != expectedOpcodes.Length ||
            starts.Where((entry, index) =>
                entry.Source.Script != "ralphSubid0dScript" ||
                entry.Source.CommandIndex != index ||
                entry.Source.Opcode != expectedOpcodes[index] ||
                entry.Source.SourceLine <= 0).Any())
        {
            throw new InvalidOperationException(
                "The importer-generated Ralph command trace lost script labels, " +
                "source lines, command order, or typed opcodes.");
        }
        int flickerCompletionUpdate = commandTrace.Entries.Single(entry =>
            entry.Source.CommandIndex == 11 &&
            entry.Phase == CutsceneCommandTracePhase.Completed).ScriptUpdate;
        if (starts[12].ScriptUpdate != flickerCompletionUpdate ||
            starts[13].ScriptUpdate != flickerCompletionUpdate ||
            starts[14].ScriptUpdate != flickerCompletionUpdate ||
            starts[15].ScriptUpdate != flickerCompletionUpdate)
        {
            throw new InvalidOperationException(
                "Ralph's completion flag, native music restore, enableinput, and " +
                "scriptend did not continue on the final flicker update.");
        }
        _roomEvents.CommandTraceSink = null;

        GD.Print("Validated room 0:39 Ralph $37:$0d DIR_RIGHT guard, TX_2a1e, " +
            "40/30 waits, per-command script cadence, animation $01, 16-pixel SPEED_100 " +
            "movement, animation $09, SND_MYSTERY_SEED, $2d-frame flicker, " +
            "same-update completion chain, imported source trace, MUS_OVERWORLD restore, " +
            "and persistent GLOBALFLAG $40.");
    }

    private void ValidateStartupTransition()
    {
        if (_currentRoom.Id != 0x11)
            throw new InvalidOperationException("The transition validation expects startup room 11.");

        // Room 11's top staircase is metatile $d0 at column 4. This position
        // crosses the same collision samples and room-exit code as player input.
        Vector2 exitPosition = new(4 * OracleRoomData.MetatileSize + 8, -2);
        for (float y = _player.Position.Y; y >= exitPosition.Y; y -= 2)
        {
            if (Collides(new Vector2(exitPosition.X, y)))
                throw new InvalidOperationException(
                    $"Room 11's path to the top staircase is blocked at y={y}.");
        }

        _player.WarpTo(exitPosition);
        CheckRoomExit(_player);
        if (_currentRoom.Id != 0x01)
            throw new InvalidOperationException(
                $"Expected room 01 after the startup transition, got {_currentRoom.Id:x2}.");
        ValidateLinkScrollsForOneTransitionFrame();
        FinishActiveScrollingTransitionForValidation();
        if (_currentRoom.GetPackedPosition(_player.Position) != 0x74)
            throw new InvalidOperationException(
                $"Expected Link to finish the 11 -> 01 transition near $74, got " +
                $"${_currentRoom.GetPackedPosition(_player.Position):x2}.");
        GD.Print("Validated original-style transition 11 -> 01 through staircase collision $18.");
    }

    private void ValidateSymmetryTransition()
    {
        if (_currentRoom.Id != 0x22)
            throw new InvalidOperationException("The Symmetry transition validation expects room 22.");

        int oldTileset = _currentRoom.TilesetId;
        Vector2 exitPosition = new(3 * OracleRoomData.MetatileSize + 8, -2);
        if (Collides(exitPosition))
            throw new InvalidOperationException("Room 22's north staircase is blocked.");

        _player.WarpTo(exitPosition);
        CheckRoomExit(_player);
        if (_currentRoom.Id != 0x12 || _currentRoom.TilesetId == oldTileset)
            throw new InvalidOperationException(
                $"Expected room 12 / a new tileset, got {_currentRoom.Id:x2} / {_currentRoom.TilesetId:x2}.");
        ValidateLinkScrollsForOneTransitionFrame();
        FinishActiveScrollingTransitionForValidation();
        GD.Print($"Validated cross-tileset transition 22 ({oldTileset:x2}) -> " +
            $"12 ({_currentRoom.TilesetId:x2}).");
    }

    private void ValidateLinkScrollsForOneTransitionFrame()
    {
        if (!IsTransitioning)
            return;

        double animationTickBefore = _animationTicks;
        UpdateAnimatedTiles(1.0 / 60.0);
        if (!Mathf.IsEqualApprox((float)_animationTicks, (float)animationTickBefore))
            throw new InvalidOperationException("Animated tiles advanced during a room transition.");

        Vector2 position = _player.Position;
        UpdateScrollingTransition(1.0 / 60.0);
        Vector2 moved = _player.Position - position;
        Vector2 scrollDirection = -(Vector2)_scrollTransitionDirection;
        if (moved.Dot(scrollDirection) <= 0.0f)
            throw new InvalidOperationException("Link did not scroll with the screen transition.");
    }

    private void FinishActiveScrollingTransitionForValidation()
    {
        for (int i = 0; i < 80 && IsTransitioning; i++)
            UpdateScrollingTransition(1.0 / 60.0);
        if (IsTransitioning)
            throw new InvalidOperationException("Scrolling transition did not finish within 80 frames.");
    }

    private int FinishActiveScrollingTransitionWithRoomEventsForValidation()
    {
        int frames = 0;
        for (; frames < 80 && IsTransitioning; frames++)
        {
            UpdateScrollingTransition(1.0 / 60.0);
            _entities.Update(1.0 / 60.0, _player);
            _roomEvents.Update(1.0 / 60.0);
        }
        if (IsTransitioning)
            throw new InvalidOperationException("Scrolling transition did not finish within 80 frames.");
        return frames;
    }
}
