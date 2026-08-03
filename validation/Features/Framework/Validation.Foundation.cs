using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateGameplaySceneGraph()
    {
        Vector2 roomViewportSize = new(
            OracleRoomData.ViewportWidth, OracleRoomData.ViewportHeight);
        Vector2 screenSize = new(
            OracleRoomData.ViewportWidth, OracleRoomData.ScreenHeight);
        FailIf(
            _sound.GetParent() != this || _sound.Owner != this ||
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
            _pushBlocks.GetParent() != _scene.WorldRoot,
            "The main/gameplay PackedScene ownership boundary or world-node parentage regressed.");

        FailIf(
            _scene.Position != Vector2.Zero || _scene.WorldRoot.Position != Vector2.Zero ||
            _scene.InterfaceLayer.Layer != 10 ||
            _roomView.ZIndex != 0 || _player.ZIndex != 10 ||
            !_roomCamera.Enabled || _roomCamera.PositionSmoothingEnabled ||
            _hud.Position != Vector2.Zero || _hud.ZIndex != 20 ||
            _scene.RoomLoadReveal.Position !=
                new Vector2(0, OracleRoomData.GameplayScreenTop) ||
            _scene.RoomLoadReveal.Size != roomViewportSize ||
            _scene.RoomLoadReveal.ZIndex != 14 ||
            _scene.RoomLoadReveal.MouseFilter != Control.MouseFilterEnum.Ignore ||
            _scene.RoomLoadReveal.Visible ||
            _warpFade.Position != new Vector2(0, OracleRoomData.GameplayScreenTop) ||
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
            _debugFlagScreen.Position !=
                new Vector2(0, OracleRoomData.GameplayScreenTop) ||
            _roomDebug.Position != new Vector2(2, OracleRoomData.GameplayScreenTop) ||
            _roomDebug.ZIndex != 100 ||
            _roomDebug.MouseFilter != Control.MouseFilterEnum.Ignore ||
            _roomDebug.GetThemeFontSize("font_size") != 8 ||
            _roomDebug.GetThemeConstant("outline_size") != 1,
            $"{GameSceneGraph.ScenePath} no longer preserves the fixed camera, UI, or draw-order values.");

        Vector2 gameplayPosition =
            _transitions.WorldToGameplayScreen(_player.Position);
        FailIf(
            !_transitions.WorldToScreen(_player.Position).IsEqualApprox(
            gameplayPosition + new Vector2(0, OracleRoomData.GameplayScreenTop)),
            "WorldToScreen did not preserve gameplay coordinates while adding the top-HUD offset.");

        GD.Print("Validated main/gameplay PackedScene ownership, unique typed bindings, " +
            "world-node containment, top-HUD camera offset, and fixed UI presentation values.");
    }

    private void ValidateMenuLifecycleFoundation()
    {
        bool processEnabled = _player.IsProcessing();
        bool physicsEnabled = _player.IsPhysicsProcessing();
        bool debugVisible = _roomDebug.Visible;
        var fade = new ColorRect { Color = new Color(1, 1, 1, 0) };
        var pause = new GameplayPauseController(_player, _roomDebug);
        var lifecycle = new OracleMenuLifecycle(fade, pause);
        ValidationMenuClient inventory = new ValidationMenuClient("MENU_INVENTORY_VALIDATION");
        ValidationMenuClient map = new ValidationMenuClient("MENU_MAP_VALIDATION");

        FailIf(
            !lifecycle.TryBeginOpening(inventory) ||
            lifecycle.TryBeginOpening(map) ||
            !pause.IsLeased || !pause.IsOwnedBy(inventory) ||
            _player.IsProcessing() || _player.IsPhysicsProcessing() || _roomDebug.Visible,
            "The shared menu lifecycle did not acquire one exclusive gameplay pause lease.");

        lifecycle.Update(inventory, 0.5 / 60.0);
        FailIf(
            lifecycle.FadeUpdate != 0 || !Mathf.IsZeroApprox(fade.Color.A),
            "The fixed-update menu fade advanced on a fractional update.");
        lifecycle.Update(inventory, 0.5 / 60.0);
        FailIf(
            lifecycle.FadeUpdate != 1 ||
            !Mathf.IsEqualApprox(fade.Color.A, 1.0f / OracleMenuLifecycle.FastFadeUpdates),
            "The fixed-update menu fade did not consume two half-updates as one update.");

        for (int update = 1; update < OracleMenuLifecycle.FastFadeUpdates - 1; update++)
            lifecycle.Update(inventory, 1.0 / 60.0);
        FailIf(
            inventory.OpenAtWhiteCalls != 0 || lifecycle.FadeUpdate != 10 ||
            lifecycle.CurrentPhase != Phase.OpeningFadeOut,
            "The common menu lifecycle swapped screens before fast fade update 11.");
        lifecycle.Update(inventory, 1.0 / 60.0);
        FailIf(
            inventory.OpenAtWhiteCalls != 1 || !Mathf.IsEqualApprox(fade.Color.A, 1.0f) ||
            lifecycle.CurrentPhase != Phase.OpeningFadeIn,
            "The common menu lifecycle did not swap screens at full white on update 11.");
        for (int update = 0; update < OracleMenuLifecycle.FastFadeUpdates; update++)
            lifecycle.Update(inventory, 1.0 / 60.0);
        FailIf(
            !lifecycle.IsOpenFor(inventory) || inventory.OpenAtWhiteCalls != 1 ||
            !Mathf.IsZeroApprox(fade.Color.A) || !pause.IsLeased,
            "The common menu lifecycle did not finish its 11-update fade-in while retaining ownership.");

        lifecycle.BeginClosing(inventory);
        for (int update = 0; update < OracleMenuLifecycle.FastFadeUpdates; update++)
            lifecycle.Update(inventory, 1.0 / 60.0);
        FailIf(
            inventory.CloseAtWhiteCalls != 1 ||
            lifecycle.CurrentPhase != Phase.ClosingFadeIn ||
            !pause.IsLeased,
            "The common menu lifecycle did not remove its screen at closing full white.");
        for (int update = 0; update < OracleMenuLifecycle.FastFadeUpdates; update++)
            lifecycle.Update(inventory, 1.0 / 60.0);
        FailIf(
            lifecycle.IsActive || pause.IsLeased || inventory.ClosedCalls != 1 ||
            _player.IsProcessing() != processEnabled ||
            _player.IsPhysicsProcessing() != physicsEnabled ||
            _roomDebug.Visible != debugVisible,
            "The shared menu lifecycle did not release ownership and restore its captured gameplay state.");

        FailIf(!lifecycle.TryBeginOpening(map), "The shared menu lifecycle could not be reopened.");
        lifecycle.Update(map, (OracleMenuLifecycle.FastFadeUpdates * 2.0 + 1.0) / 60.0);
        FailIf(
            !lifecycle.IsOpenFor(map) || map.OpenAtWhiteCalls != 1 ||
            !Mathf.IsZeroApprox(fade.Color.A),
            "A long rendered frame did not execute both fixed 11-update menu fades exactly once.");
        lifecycle.CloseImmediately(map);

        ValidationMenuClient gameOver =
            new ValidationMenuClient("MENU_GAMEOVER_VALIDATION");
        FailIf(
            !lifecycle.TryBeginOpeningFromWhite(gameOver) ||
            gameOver.OpenAtWhiteCalls != 1 ||
            lifecycle.CurrentPhase != Phase.OpeningFadeIn ||
            !Mathf.IsEqualApprox(fade.Color.A, 1.0f) ||
            !pause.IsOwnedBy(gameOver),
            "The forced game-over menu did not open at white and begin " +
            "with only the 11-update fade-in half.");
        for (int update = 0;
            update < OracleMenuLifecycle.FastFadeUpdates - 1;
            update++)
        {
            lifecycle.Update(gameOver, 1.0 / 60.0);
        }
        FailIf(
            lifecycle.IsOpenFor(gameOver) ||
            gameOver.OpenAtWhiteCalls != 1 ||
            Mathf.IsZeroApprox(fade.Color.A),
            "The forced game-over screen finished its white fade-in early.");
        lifecycle.Update(gameOver, 1.0 / 60.0);
        FailIf(
            !lifecycle.IsOpenFor(gameOver) ||
            !Mathf.IsZeroApprox(fade.Color.A),
            "The forced game-over screen did not finish its fade-in on update 11.");
        lifecycle.CloseImmediately(gameOver);

        _player.SetProcess(false);
        _player.SetPhysicsProcess(true);
        _roomDebug.Visible = false;
        PauseLease preservedLease = pause.TryAcquire(map) ??
            throw new InvalidOperationException("A released gameplay pause lease could not be reacquired.");
        preservedLease.Dispose();
        FailIf(
            _player.IsProcessing() || !_player.IsPhysicsProcessing() || _roomDebug.Visible,
            "A gameplay pause lease blindly enabled state instead of restoring its captured values.");
        _player.SetProcess(processEnabled);
        _player.SetPhysicsProcess(physicsEnabled);
        _roomDebug.Visible = debugVisible;
        fade.Free();

        GD.Print("Validated one shared Oracle menu load state, exclusive pause ownership, " +
            "fractional fixed-update accumulation, exact 11-update white swap boundaries, " +
            "forced game-over fade-in from white, and captured-state restoration.");
    }

    private static void ValidateOracleObjectMath()
    {
        OracleObjectMovement movement = OracleObjectMovement.Shared;
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

        (Vector2 Origin, Vector2 Target, int Angle)[] sourceAngleCases =
        [
            (new(100, 100), new(101, 100), 0x08),
            (new(100, 100), new(100, 99), 0x00),
            (new(100, 100), new(99, 100), 0x18),
            (new(100, 100), new(100, 101), 0x10),
            (new(100, 100), new(116, 84), 0x05),
            (new(100, 100), new(116, 116), 0x0b),
            (new(100, 100), new(84, 116), 0x15),
            (new(100, 100), new(84, 84), 0x1b),
            // For a maximum delta of 64, the source's four integer
            // thresholds are exactly 16, 32, 48, and 64.
            (new(100, 100), new(164, 84), 0x08),
            (new(100, 100), new(164, 83), 0x07),
            (new(100, 100), new(164, 68), 0x07),
            (new(100, 100), new(164, 67), 0x06),
            (new(100, 100), new(164, 52), 0x06),
            (new(100, 100), new(164, 51), 0x05),
            (new(100, 100), new(164, 36), 0x05),
            // A maximum of nine reaches band 4 after the fourth comparison;
            // eleven misses all four and takes that same fallback band.
            (new(100, 100), new(109, 91), 0x04),
            (new(100, 100), new(111, 89), 0x04),
            // A maximum below eight makes the integer threshold zero. All
            // four comparisons miss, selecting the fifth table entry.
            (new(100, 100), new(107, 94), 0x04),
            // The source adds eight before unsigned subtraction, moving
            // the coordinate wrap boundary from $00 to $f8.
            (new(247, 50), new(248, 50), 0x18),
            (new(248, 50), new(249, 50), 0x08),
            (Vector2.Zero, Vector2.Zero, 0x18)
        ];
        foreach ((Vector2 origin, Vector2 target, int expected) in sourceAngleCases)
        {
            FailIf(
                movement.RelativeAngle(origin, target) != expected,
                $"objectGetRelativeAngle ({origin.Y},{origin.X})->" +
                $"({target.Y},{target.X}) did not produce ${expected:x2}.");
        }

        Vector2 precise = new(0xff80 / 256.0f, 0x01f0 / 256.0f);
        Vector2 pixels = default;
        for (int update = 0; update < 300; update++)
            pixels = movement.ApplySpeed(ref precise, 0x05, 0x03);

        FailIf(
            OracleObjectMath.ToPixelPosition(new Vector2(1.75f, -0.25f)) !=
                new Vector2(1, -1) ||
            OracleObjectMath.NormalizeSourceScreenPosition(
                new Vector2(0xff, 0xf8)) != new Vector2(-1, -8) ||
            OracleObjectMath.SourceOamWrapOffset(
                new Vector2(0xff, 0xf8)) != new Vector2(-256, -256) ||
            OracleObjectMath.SourceOamWrapOffset(
                new Vector2(0xa8, -1)) != Vector2.Zero ||
            movement.Direction(0x00) != Vector2.Up ||
            movement.Direction(0x08) != Vector2.Right ||
            precise != new Vector2(0x136c / 256.0f, 0xe378 / 256.0f) ||
            pixels != new Vector2(0x13, 0xe3) ||
            OracleObjectMath.CardinalVector(0x0f) != Vector2.Right ||
            OracleObjectMath.StrictCardinalVector(0x18) != Vector2.Left ||
            airborneLanded || airborneZ != -0x100 || airborneSpeedZ != -0xe0 ||
            !landed || landingZ != 0 || landingSpeedZ != 0x20 ||
            !rejectedNonCardinal ||
            !OracleObjectMath.IsInsideOriginalScreenBoundary(new Vector2(-7, -7)) ||
            OracleObjectMath.IsInsideOriginalScreenBoundary(new Vector2(168, 0)) ||
            OracleObjectMath.IsInsideOriginalScreenBoundary(new Vector2(0, 136)),
            "Shared original-object coordinate, angle, Z integration, or " +
            "screen-boundary math regressed.");
        GD.Print("Validated imported object motion, integer relative-angle thresholds, " +
            "the $f8 byte-wrap boundary, 300-update signed 8.8 carry/wrap, high-byte " +
            "rendering, Z integration, cardinal decoding, and screen boundaries.");
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
        OracleRandomResult firstNext = firstParse.Next();

        var secondParse = new OracleRandom();
        secondParse.BeginRoomParse();
        for (int index = 0; index < 17; index++)
            secondParse.NextPlacementValue();
        secondParse.BeginRoomParse();
        byte[] secondPlacements = new byte[8];
        for (int index = 0; index < secondPlacements.Length; index++)
            secondPlacements[index] = secondParse.NextPlacementValue();
        OracleRandomResult secondNext = secondParse.Next();

        FailIf(
            !rejectedUnparsedPlacement ||
            !firstPlacements.SequenceEqual(new byte[]
                { 0x7d, 0xe4, 0xf0, 0x49, 0x98, 0xd7, 0x5c, 0xfe }) ||
            firstNext != new OracleRandomResult(0xc6, 0x1a, 0x04) ||
            !secondPlacements.SequenceEqual(new byte[]
                { 0x07, 0x5d, 0x70, 0xde, 0xa8, 0x08, 0x6f, 0xb3 }) ||
            secondNext != new OracleRandomResult(0x59, 0xd0, 0x9b),
            "Room parsing did not rebuild the enemy-placement permutation, reset its " +
            "index, or consume the original 256 shared RNG values.");

        OracleRoomData emptyRoom = _world.LoadRoom(0, 0x00);
        var validationRoot = new Node { Name = "OracleRandomValidation" };
        AddChild(validationRoot);

        var directRandom = new OracleRandom();
        using var directFixture = RoomEntityValidationFixture.ForRoot(
            validationRoot, new() { Random = directRandom });
        RoomEntityManager directManager = directFixture.Manager;
        directManager.LoadRoom(0, emptyRoom);
        FailIf(
            directRandom.Next() != new OracleRandomResult(0xc6, 0x1a, 0x04),
            "A direct room load did not generate exactly one enemy-placement buffer.");
        directManager.Clear();

        var preloadRandom = new OracleRandom();
        using var preloadFixture = RoomEntityValidationFixture.ForRoot(
            validationRoot, new() { Random = preloadRandom });
        RoomEntityManager preloadManager = preloadFixture.Manager;
        preloadManager.LoadRoom(0, emptyRoom);
        preloadManager.BeginScreenTransition(0, emptyRoom, Vector2.Zero);
        FailIf(
            preloadRandom.Next() != new OracleRandomResult(0x59, 0xd0, 0x9b),
            "A scrolling destination preload did not generate the next placement buffer.");
        preloadManager.Clear();

        var cutsceneRandom = new OracleRandom();
        using var cutsceneFixture = RoomEntityValidationFixture.ForRoot(
            validationRoot, new() { Random = cutsceneRandom });
        RoomEntityManager cutsceneManager = cutsceneFixture.Manager;
        cutsceneManager.LoadCutsceneRoom(0, emptyRoom, includeTimePortals: false);
        FailIf(
            cutsceneRandom.Next() != new OracleRandomResult(0x5e, 0x27, 0xa5),
            "A cutscene-only room load unexpectedly parsed ordinary room objects.");
        cutsceneManager.Clear();
        RemoveChild(validationRoot);
        validationRoot.Free();

        GD.Print("Validated shared getRandomNumber state, per-parse 256-call placement-buffer " +
            "generation, placement-index reset, direct loads, destination preloads, and " +
            "cutscene-only load exclusion.");
    }

    private static void ValidateCutsceneCommandSchema()
    {
        IReadOnlyList<CutsceneCommandSchemaEntry> entries =
            CutsceneCommandSchema.Entries;
        FailIf(
            entries.Count != 52 ||
            entries.Select(entry => entry.CommandType)
                .Distinct()
                .Count() != entries.Count ||
            entries.Count(entry =>
                entry.CommandType == typeof(CutsceneShowLoadedTextCommand)) != 1 ||
            entries.Count(entry =>
                entry.CommandType == typeof(CutsceneCheckTextCommand)) != 1,
            "The cutscene command schema no longer declares exactly one entry " +
            "for each of the 52 typed command records.");

        var source = new CutsceneCommandSource(
            "validation/command-schema.tsv",
            "validationCommandSchema",
            CommandIndex: 0,
            SourceLine: 41,
            Opcode: "paralleltranslate");
        CutsceneActorId[] actors = CutsceneCommandSchema.Actors(
            new CutsceneParallelTranslateCommand(
                source,
                "Nayru",
                Vector2.Zero,
                Frames: 1,
                "Ralph",
                Vector2.Zero,
                Frames2: 1)).ToArray();
        FailIf(
            actors.Length != 2 ||
            actors[0] != new CutsceneActorId("Nayru") ||
            actors[1] != new CutsceneActorId("Ralph"),
            "Schema-owned multi-actor enumeration lost its declared order.");

        CutsceneActorId[] optionalActors = CutsceneCommandSchema.Actors(
            new CutsceneNativeBlockingCommand(
                source with { Opcode = "nativeblock" },
                "validationNative",
                Actor: null,
                Frames: 1,
                Payload: string.Empty)).ToArray();
        FailIf(
            optionalActors.Length != 0,
            "Schema-owned optional actor enumeration emitted an empty actor.");

        try
        {
            CutsceneCommandSchema.ForOpcode(
                "move",
                source with { Opcode = "move" }).ValidateNormalizedFields(
                    "validation/invalid-command.tsv",
                    physicalLine: 12,
                    actor: "Nayru",
                    arg0: "08",
                    arg1: string.Empty,
                    payload: "walk");
            throw new InvalidOperationException(
                "The cutscene command schema accepted an invalid normalized operand.");
        }
        catch (InvalidOperationException exception) when (
            exception.Message.Contains(
                "validation/invalid-command.tsv:12",
                StringComparison.Ordinal) &&
            exception.Message.Contains("arg1", StringComparison.Ordinal) &&
            exception.Message.Contains("hex", StringComparison.Ordinal))
        {
        }

        try
        {
            CutsceneCommandSchema.ValidateResult(
                new CutsceneEndCommand(
                    source with { Opcode = "scriptend" }),
                CommandResult.Continue);
            throw new InvalidOperationException(
                "The cutscene command schema accepted an undeclared runner result.");
        }
        catch (InvalidOperationException exception) when (
            exception.Message.Contains(
                "undeclared result 'continue'",
                StringComparison.Ordinal) &&
            exception.Message.Contains("scriptend", StringComparison.Ordinal))
        {
        }

        GD.Print(
            "Validated 52-entry cutscene command schema coverage, normalized " +
            "field shapes, actor enumeration, and runner result contracts.");
    }

    private static void ValidateCutsceneDefaultDeny()
    {
        var source = new CutsceneCommandSource(
            "validation/source-aware.tsv",
            "validationDefaultDeny",
            CommandIndex: 0,
            SourceLine: 37,
            Opcode: "setmusic");
        var runner = new CutsceneCommandRunner(
            new ValidationCutsceneDefaultDenyHost());
        runner.Start([new CutsceneSetMusicCommand(source, Music: 0x12)]);

        try
        {
            runner.AdvanceFrame();
        }
        catch (InvalidOperationException exception) when (
            exception.Message.Contains(source.Script, StringComparison.Ordinal) &&
            exception.Message.Contains(source.Label, StringComparison.Ordinal) &&
            exception.Message.Contains("[0]", StringComparison.Ordinal) &&
            exception.Message.Contains("line 37", StringComparison.Ordinal) &&
            exception.Message.Contains(
                "requiring [music]",
                StringComparison.Ordinal))
        {
            GD.Print(
                "Validated schema-capability-aware default-deny cutscene host diagnostics.");
            return;
        }

        throw new InvalidOperationException(
            "An unsupported cutscene host capability did not fail with its " +
            "script, label, command index, and source line.");
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
        var singleTileChanges = new SingleTileChangeDatabase();
        var warps = new WarpDatabase();
        static Vector2 Point(int position) => new(
            (position & 0x0f) * OracleRoomData.MetatileSize + 8,
            (position >> 4) * OracleRoomData.MetatileSize + 8);

        Vector2 doorPoint = Point(0x23);
        OracleRoomData room = rooms.CurrentRoom;

        FailIf(
            changes.RuleCount != 44 || changes.RoomCount != 35 ||
            singleTileChanges.RecordCount != 56 ||
            room.GetPackedPosition(doorPoint) != 0x23 ||
            room.GetOriginalMetatile(doorPoint) != 0xa7 ||
            room.GetMetatile(doorPoint) != 0xa7 || !room.IsSolid(doorPoint) ||
            warps.TryGetTileWarp(0, 0x3a, 0x23, room.GetMetatile(doorPoint), out _),
            "Room 0:3a did not begin with Nayru's house door closed at $23/$a7.");

        save.SetGlobalFlag(OracleSaveData.GlobalFlagIntroDone);
        room = rooms.Load(0, 0x3a);
        FailIf(
            room.GetMetatile(doorPoint) != 0xee || room.IsSolid(doorPoint) ||
            !warps.TryGetTileWarp(0, 0x3a, 0x23, 0xee, out Warp warp) ||
            warp.DestinationGroup != 3 || warp.DestinationRoom != 0x9e,
            "GLOBALFLAG_INTRO_DONE $0a did not open room 0:3a's $23/$ee door to 3:9e.");

        save.SetGlobalFlag(OracleSaveData.GlobalFlagIntroDone, value: false);
        room = rooms.Load(0, 0x3a);
        FailIf(
            room.GetMetatile(doorPoint) != 0xa7 || !room.IsSolid(doorPoint),
            "Reloading room 0:3a without GLOBALFLAG_INTRO_DONE did not restore door $23/$a7.");

        // applySingleTileChanges treats $f0/$f1 as unlinked/linked predicates
        // and $f2 as the executed GLOBALFLAG_FINISHEDGAME check.
        var predicateSave = OracleSaveData.CreateStandardGame();
        var predicateRooms = new RoomSession(
            0, 0x48, () => 0, () => { }, predicateSave);
        FailIf(
            predicateRooms.CurrentRoom.GetMetatile(Point(0x28)) != 0x64,
            "Single-tile predicate $f0 did not apply room 0:48's unlinked-only write.");
        predicateSave.WriteWramByte(0xc612, 1);
        room = predicateRooms.Load(0, 0x48);
        FailIf(
            room.GetMetatile(Point(0x28)) != room.GetOriginalMetatile(Point(0x28)) ||
            predicateRooms.Load(3, 0xd6).GetMetatile(Point(0x55)) != 0xe9,
            "Single-tile predicates $f0/$f1 did not switch with wIsLinkedGame.");
        predicateSave.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame);
        FailIf(
            predicateRooms.Load(0, 0x47).GetMetatile(Point(0x36)) != 0xf2,
            "Single-tile predicate $f2 did not follow GLOBALFLAG_FINISHEDGAME.");

        // Current-room set/clear conditions and arbitrary direct writes.
        room = rooms.Load(0, 0x73);
        byte forestOriginal = room.GetOriginalMetatile(Point(0x73));
        save.SetRoomFlag(0, 0x73, OracleSaveData.RoomFlag80);
        room = rooms.Load(0, 0x73);
        FailIf(
            room.GetMetatile(Point(0x73)) != 0x3a ||
            room.GetMetatile(Point(0x74)) != 0x10 ||
            room.GetMetatile(Point(0x77)) != 0x3a,
            "Room 0:73 flag $80 did not apply its five imported rubble writes.");
        save.SetRoomFlag(0, 0x73, OracleSaveData.RoomFlag80, value: false);
        room = rooms.Load(0, 0x73);
        FailIf(
            room.GetMetatile(Point(0x73)) != forestOriginal,
            "Room 0:73 did not restore its original rubble layout.");

        room = rooms.Load(0, 0xac);
        byte treeOriginal = room.GetOriginalMetatile(Point(0x33));
        FailIf(
            room.GetMetatile(Point(0x33)) != 0xaf || room.GetMetatile(Point(0x44)) != 0xaf,
            "Room 0:ac's clear flag-$80 branch did not remove the scent tree.");
        save.SetRoomFlag(0, 0xac, OracleSaveData.RoomFlag80);
        room = rooms.Load(0, 0xac);
        FailIf(
            room.GetMetatile(Point(0x33)) != treeOriginal,
            "Room 0:ac flag $80 did not retain the original planted-tree layout.");

        // Explicit other-room flags.
        room = rooms.Load(0, 0x0b);
        byte caveOriginal = room.GetOriginalMetatile(Point(0x43));
        FailIf(
            room.GetMetatile(Point(0x43)) != caveOriginal,
            "Room 0:0b opened before room 0:0a flag $40 was set.");
        save.SetRoomFlag(0, 0x0a, OracleSaveData.RoomFlag40);
        room = rooms.Load(0, 0x0b);
        FailIf(
            room.GetMetatile(Point(0x43)) != 0xdd,
            "Room 0:0a flag $40 did not open room 0:0b's imported cave entrance.");

        // Essence conditions.
        room = rooms.Load(5, 0xb9);
        byte elderOriginal = room.GetOriginalMetatile(Point(0x41));
        FailIf(
            room.GetMetatile(Point(0x41)) != elderOriginal,
            "Room 5:b9 changed before essence bit 3 was set.");
        save.WriteWramByte(0xc6bf, (byte)(1 << 3));
        room = rooms.Load(5, 0xb9);
        FailIf(
            room.GetMetatile(Point(0x41)) != 0xa1 ||
            room.GetMetatile(Point(0x44)) != 0xef ||
            room.GetMetatile(Point(0x55)) != 0xa2,
            "Essence bit 3 did not apply room 5:b9's two imported boulder rows.");

        // Draw rectangles preserve the original height/width ordering.
        room = rooms.Load(2, 0x90);
        save.SetRoomFlag(2, 0x90, 0x02);
        room = rooms.Load(2, 0x90);
        FailIf(
            room.GetMetatile(Point(0x42)) != 0xdd ||
            room.GetMetatile(Point(0x47)) != 0xef ||
            room.GetMetatile(Point(0x52)) != 0xb9 ||
            room.GetMetatile(Point(0x57)) != 0xbe,
            "Room 2:90 flag $02 did not draw its imported 2x6 Jabu entrance rectangle.");

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
            FailIf(
                room.GetMetatile(point) != sourceRoom.GetOriginalMetatile(point),
                "GLOBALFLAG_D3_CRYSTALS did not copy room 4:60's original layout into 4:52.");
        }

        room = rooms.Load(4, 0x60);
        FailIf(
            room.GetMetatile(Point(0x57)) != 0xf1,
            "Room 4:60 did not create its closed chest for clear room item flag $20.");
        save.SetRoomFlag(4, 0x60, OracleSaveData.RoomFlagItem);
        room = rooms.Load(4, 0x60);
        FailIf(
            room.GetMetatile(Point(0x57)) != 0xf0,
            "Room 4:60 item flag $20 did not select the opened chest under the D3 global flag.");

        GD.Print("Validated 56 single-tile changes and 44 imported rules for 35 " +
            "room-specific tile changers: " +
            "global/current/specific-room/essence/WRAM conditions, set/fill/draw/replace/copy " +
            "operations, and room 0:3a's closed-to-open Nayru-house warp.");
    }

    private static void ValidateRoomEventTimeline()
    {
        var timeline = new RoomEventTimelineQueue<ValidationTimelineStep>();
        timeline.Enqueue(new ValidationTimelineStep(2));
        timeline.Enqueue(new ValidationTimelineStep(0));
        var observedCounters = new List<int>();
        bool Update(ValidationTimelineStep step)
        {
            observedCounters.Add(step.Counter);
            return --step.Counter == 0;
        }

        FailIf(
            !timeline.AdvanceFrame(Update) ||
            !timeline.AdvanceFrame(Update) ||
            !timeline.AdvanceFrame(Update) ||
            timeline.AdvanceFrame(Update) ||
            !observedCounters.SequenceEqual(new[] { 2, 1, 1 }),
            "Room-event timeline duration clamping or one-step update cadence regressed.");

        timeline.Enqueue(new ValidationTimelineStep(3));
        timeline.AdvanceFrame(Update);
        timeline.Clear();
        FailIf(timeline.AdvanceFrame(Update), "Room-event timeline clear retained active work.");

        static CutsceneCommandSource CommandSource(
            string script,
            int commandIndex,
            string opcode) =>
            new(script, $"{script}Label", commandIndex, 100 + commandIndex, opcode);

        ValidationImpaPostPushHost callHost = new ValidationImpaPostPushHost(linkAngle: 0x18);
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
        FailIf(
            callHost.Signal != 0x06 || callRunner.Instruction != 3,
            "Cutscene callscript did not yield at its target boundary.");
        callHost.AdvanceValidationFrame();
        callRunner.AdvanceFrame();
        FailIf(
            callHost.Signal != 0x07 || callRunner.Instruction != 1,
            "Cutscene retscript did not yield at its return boundary.");
        callHost.AdvanceValidationFrame();
        callRunner.AdvanceFrame();
        int[] callOrder = callHost.Trace.Entries
            .Where(entry => entry.Phase == CutsceneCommandTracePhase.Started)
            .Select(entry => entry.Source.CommandIndex)
            .ToArray();
        FailIf(
            callRunner.Active || !callHost.Ended || callHost.Signal != 0x08 ||
            !callOrder.SequenceEqual(new[] { 0, 3, 4, 1, 2 }) ||
            !callHost.Trace.Entries.Any(entry =>
                entry.Source.CommandIndex == 0 &&
                entry.Phase == CutsceneCommandTracePhase.Completed &&
                entry.NextCommandIndex == 3) ||
            !callHost.Trace.Entries.Any(entry =>
                entry.Source.CommandIndex == 4 &&
                entry.Phase == CutsceneCommandTracePhase.Completed &&
                entry.NextCommandIndex == 1),
            "Cutscene branch/call stack execution or trace targets regressed.");

        ValidationImpaPostPushHost laneHost = new ValidationImpaPostPushHost(linkAngle: 0x18);
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
        FailIf(
            scheduler.Active || scheduler.Count != 2 ||
            !laneStartOrder.SequenceEqual(
                new[] { "laneA:0", "laneB:0", "laneA:1", "laneB:1", "laneB:2", "laneA:2" }),
            "Parallel cutscene lanes lost independent counters or stable insertion order.");
        scheduler.Clear();
        FailIf(
            scheduler.Active || scheduler.Count != 0,
            "Parallel cutscene lane clear retained active work.");

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

        FailIf(
            !sequence.Active ||
            !sequence.AdvanceFrame() ||
            !sequence.AdvanceFrame() ||
            !sequence.AdvanceFrame(),
            "Finite room-event sequence did not retain queued or gated work.");

        gateOpen = true;
        FailIf(
            !sequence.AdvanceFrame() ||
            !sequence.AdvanceFrame() ||
            !sequence.Active ||
            !sequence.AdvanceFrame() ||
            sequence.Active ||
            sequence.AdvanceFrame() ||
            !observedSequence.SequenceEqual(
                new[] { "wait:1", "wait:0", "elapsed", "gate", "action" }),
            "Finite room-event wait, gate, action, or command cadence regressed.");

        GD.Print("Validated shared room-event timeline duration clamping, command boundaries, " +
            "finite wait/gate/action sequences, typed actor diagnostics, call stacks, " +
            "parallel lane ordering, independent counters, and lifecycle clearing.");
    }

    private void ValidateSoundEngine()
    {
        var data = new OracleSoundData();
        OracleSaveData roomMusicSave = OracleSaveData.CreateStandardGame();
        roomMusicSave.SetGlobalFlag(0x15);
        roomMusicSave.SetRoomFlag(1, 0x97, OracleSaveData.RoomFlag40, value: false);
        int pendingRalphMusic = data.RoomMusic(1, 0x97, roomMusicSave);
        roomMusicSave.SetRoomFlag(1, 0x97, OracleSaveData.RoomFlag40);
        int completedRalphMusic = data.RoomMusic(1, 0x97, roomMusicSave);
        ChannelStart[] title = data.ChannelsFor(
            OracleSoundEngine.MusTitlescreen).ToArray();
        ChannelStart[] getItem = data.ChannelsFor(0x4c).ToArray();
        ChannelStart[] openMenu = data.ChannelsFor(
            OracleSoundEngine.SndOpenMenu).ToArray();
        ChannelStart[] makuDisappear = data.ChannelsFor(
            OracleSoundEngine.SndMakuDisappear).ToArray();
        ChannelStart[] damageLink = data.ChannelsFor(
            OracleSoundEngine.SndDamageLink).ToArray();
        ChannelStart[] linkFall = data.ChannelsFor(
            OracleSoundEngine.SndLinkFall).ToArray();
        FailIf(
            title.Length != 4 ||
            !title.Select(channel => channel.Channel).SequenceEqual(new[] { 0, 1, 4, 6 }) ||
            title.Any(channel => channel.Priority != 1 || channel.Bank != 0x3a) ||
            getItem.Length != 4 ||
            !getItem.Select(channel => channel.Channel).SequenceEqual(new[] { 2, 3, 5, 7 }) ||
            getItem.Any(channel => channel.Priority != 8 || channel.Bank != 0x3b) ||
            openMenu.Length != 2 ||
            !openMenu.Select(channel => channel.Channel).SequenceEqual(new[] { 2, 3 }) ||
            openMenu.Any(channel => channel.Priority != 1 || channel.Bank != 0x3a) ||
            makuDisappear.Length != 2 ||
            !makuDisappear.Select(channel => channel.Channel).SequenceEqual(new[] { 2, 7 }) ||
            makuDisappear.Any(channel => channel.Priority != 1 || channel.Bank != 0x39) ||
            damageLink.Length != 1 || damageLink[0].Channel != 5 ||
            damageLink[0].Priority != 1 || damageLink[0].Bank != 0x3d ||
            linkFall.Length != 1 || linkFall[0].Channel != 5 ||
            linkFall[0].Priority != 1 || linkFall[0].Bank != 0x3a ||
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
            pendingRalphMusic != OracleSoundEngine.MusRalph ||
            completedRalphMusic != data.RoomMusic(1, 0x97) ||
            !data.TryGetNoise(0x24, out NoiseRecord noise) ||
            noise.Envelope != 0x01 || noise.Frequency != 0x47 ||
            data.WaveSample(0x0e, 0) >= data.WaveSample(0x0e, 16),
            "Imported sound pointers, frequencies, conditional room assignments, " +
            "waveforms, or noise table diverged.");

        float pulseFrequency = OracleSoundEngine.ToneFrequencyForValidation(0, 0x05ce);
        float waveFrequency = OracleSoundEngine.ToneFrequencyForValidation(4, 0x05ce);
        FailIf(
            !Mathf.IsEqualApprox(pulseFrequency, 233.2242f) ||
            !Mathf.IsEqualApprox(waveFrequency, 116.6121f) ||
            !Mathf.IsEqualApprox(pulseFrequency, waveFrequency * 2) ||
            !Mathf.IsEqualApprox(OracleSoundEngine.NoiseClockForValidation(0x14), 32768.0f) ||
            !Mathf.IsEqualApprox(OracleSoundEngine.NoiseClockForValidation(0x75), 409.6f) ||
            OracleSoundEngine.NoiseClockForValidation(0xe1) != 0 ||
            OracleSoundEngine.CgbHighPassFactorForValidation is < 0.904 or > 0.905,
            "GBC pulse/wave/noise clocks or CGB high-pass coefficient diverged.");

        var sound = new OracleSoundEngine(data, enableOutput: false);
        ValidationSoundRequestAudit soundAudit =
            sound.AttachPlayRequestAudit();
        sound.PlaySound(OracleSoundEngine.MusTitlescreen);
        sound.Tick();
        ChannelState square1 = sound.Channel(0);
        ChannelState square2 = sound.Channel(1);
        ChannelState wave = sound.Channel(4);
        FailIf(
            sound.ActiveMusic != OracleSoundEngine.MusTitlescreen ||
            !square1.Active || square1.DutyOrWaveform != 2 || square1.Volume != 8 ||
            square1.CurrentFrequencyRegister != 0x0642 || square1.WaitFrames != 0x17 ||
            !square2.Active || square2.CurrentFrequencyRegister != 0x06e7 ||
            square2.WaitFrames != 0x17 || !wave.Active || wave.Gate ||
            wave.WaitFrames != 0x23 || sound.Channel(6).Active,
            "MUS_TITLESCREEN did not execute its original first square/wave/noise commands.");


        // Channel 0's first $18 note, second $14 note, and following $10
        // rest consume 45 sound updates including their command updates.
        for (int update = 0; update < 44; update++)
            sound.Tick();
        FailIf(
            !square1.Active || !square1.Gate || square1.WaitFrames != 0x0f ||
            square1.OutputVolume != 2 || square1.EnvelopePeriod != 1 ||
            square1.EnvelopeDirection != -1,
            "MUS_TITLESCREEN channel 0 rest did not install its period-1 square release.");

        sound.PlaySound(OracleSoundEngine.SndOpenMenu);
        sound.Tick();
        ChannelState openMenuHigh = sound.Channel(2);
        ChannelState openMenuLow = sound.Channel(3);
        FailIf(
            !openMenuHigh.Active || !openMenuHigh.Gate || openMenuHigh.Priority != 1 ||
            openMenuHigh.DutyOrWaveform != 1 || openMenuHigh.Volume != 15 ||
            openMenuHigh.OutputVolume != 1 || openMenuHigh.Envelope != 3 ||
            openMenuHigh.PitchSlide != 0x23 ||
            openMenuHigh.CurrentFrequencyRegister != 0x0416 || openMenuHigh.WaitFrames != 0x15 ||
            !openMenuLow.Active || !openMenuLow.Gate || openMenuLow.Priority != 1 ||
            openMenuLow.DutyOrWaveform != 2 || openMenuLow.Volume != 15 ||
            openMenuLow.OutputVolume != 1 || openMenuLow.Envelope != 3 ||
            openMenuLow.PitchSlide != 0x2c ||
            openMenuLow.CurrentFrequencyRegister != 0x002d || openMenuLow.WaitFrames != 0x15,
            "SND_OPENMENU did not start its paired C3/C2 square-channel sweep.");

        sound.PlaySound(OracleSoundEngine.SndDamageLink);
        sound.Tick();
        ChannelState linkVoice = sound.Channel(5);
        FailIf(
            !linkVoice.Active || !linkVoice.Gate || linkVoice.Priority != 1 ||
            linkVoice.DutyOrWaveform != 0x2d || linkVoice.PitchShift != -3 ||
            linkVoice.CurrentFrequencyRegister != 0x050f || linkVoice.WaitFrames != 0,
            "SND_DAMAGE_LINK did not start its shifted F2 wave-channel cry: " +
            $"active={linkVoice.Active}, gate={linkVoice.Gate}, priority={linkVoice.Priority}, " +
            $"waveform=${linkVoice.DutyOrWaveform:x2}, shift={linkVoice.PitchShift}, " +
            $"frequency=${linkVoice.CurrentFrequencyRegister:x4}, wait={linkVoice.WaitFrames}.");

        sound.PlaySound(OracleSoundEngine.SndLinkFall);
        sound.Tick();
        FailIf(
            !linkVoice.Active || !linkVoice.Gate || linkVoice.Priority != 1 ||
            linkVoice.DutyOrWaveform != 0x03 || linkVoice.PitchShift != 0 ||
            linkVoice.CurrentFrequencyRegister != 0x07c1 || linkVoice.WaitFrames != 1,
            "SND_LINK_FALL did not start its two-update C6 wave-channel descent: " +
            $"active={linkVoice.Active}, gate={linkVoice.Gate}, priority={linkVoice.Priority}, " +
            $"waveform=${linkVoice.DutyOrWaveform:x2}, shift={linkVoice.PitchShift}, " +
            $"frequency=${linkVoice.CurrentFrequencyRegister:x4}, wait={linkVoice.WaitFrames}.");

        sound.PlaySound(OracleSoundEngine.SndMenuMove);
        sound.Tick();
        ChannelState sfxSquare = sound.Channel(2);
        FailIf(
            !sfxSquare.Active || sfxSquare.Priority != 1 ||
            sfxSquare.DutyOrWaveform != 3 || !sfxSquare.RawFrequencyMode ||
            sfxSquare.RawEnvelope != 0xd9 ||
            sfxSquare.CurrentFrequencyRegister != 0x07a0 ||
            sfxSquare.WaitFrames != 2,
            "SND_MENU_MOVE did not execute its raw-frequency $07a0/$03 command.");

        sound.PlaySound(OracleSoundEngine.SndSwordSlash);
        sound.Tick();
        ChannelState rawNoise = sound.Channel(7);
        FailIf(
            !rawNoise.Active || !rawNoise.Gate || rawNoise.Priority != 1 ||
            rawNoise.RawEnvelope != 0x20 || rawNoise.OutputVolume != 2 ||
            rawNoise.EnvelopePeriod != 0 || rawNoise.NoiseRegister != 0x47 ||
            rawNoise.NoiseTriggerPending || rawNoise.WaitFrames != 0,
            "SND_SWORDSLASH did not retrigger CH4 from its raw NR42/NR43 pair.");

        sound.PlaySound(OracleSoundEngine.SndMakuDisappear);
        sound.Tick();
        ChannelState makuPulse = sound.Channel(2);
        ChannelState makuNoise = sound.Channel(7);
        FailIf(
            !makuPulse.Active || makuPulse.Priority != 1 ||
            makuPulse.DutyOrWaveform != 2 || makuPulse.Volume != 3 ||
            makuPulse.OutputVolume != 3 ||
            makuPulse.CurrentFrequencyRegister != 0x002d ||
            makuPulse.WaitFrames != 0x1b ||
            !makuNoise.Active || !makuNoise.Gate || makuNoise.Priority != 1 ||
            makuNoise.RawEnvelope != 0xf0 || makuNoise.OutputVolume != 15 ||
            makuNoise.EnvelopePeriod != 0 || makuNoise.NoiseRegister != 0x75 ||
            makuNoise.NoiseTriggerPending || makuNoise.WaitFrames != 0x1b,
            "SND_MAKUDISAPPEAR did not start its low C2 pulse and raw $f0/$75 CH4 block.");

        sound.PlaySound(0x4c);
        sound.Tick();
        int protectedOffset = sound.Channel(2).Offset;
        FailIf(
            sound.Channel(2).Priority != 8 || sound.Channel(3).Priority != 8 ||
            sound.Channel(5).Priority != 8 || sound.Channel(7).Priority != 8,
            "SND_GETITEM did not claim all four SFX channels at priority 8.");
        sound.PlaySound(OracleSoundEngine.SndMenuMove);
        FailIf(
            sound.Channel(2).Priority != 8 || sound.Channel(2).Offset != protectedOffset,
            "Low-priority SND_MENU_MOVE replaced SND_GETITEM's square channel.");

        sound.PlaySound(OracleSoundEngine.SndCtrlStopSfx);
        FailIf(
            new[] { 2, 3, 5, 7 }.Any(channel => sound.Channel(channel).Active),
            "SNDCTRL_STOPSFX did not release all SFX channels.");
        sound.PlaySound(OracleSoundEngine.SndCtrlStopMusic);
        sound.Tick();
        FailIf(
            sound.ActiveMusic != 0 || new[] { 0, 1, 4, 6 }.Any(channel => sound.Channel(channel).Active),
            "SNDCTRL_STOPMUSIC did not run sound $de's stop channels.");

        sound.PlaySound(OracleSoundEngine.MusTitlescreen);
        int overworldRequests = sound.PlayRequestsFor(OracleSoundEngine.MusOverworld);
        sound.PlaySound(OracleSoundEngine.SndCtrlMediumFadeOut);
        sound.PlayMusicIfChanged(OracleSoundEngine.MusOverworld);
        FailIf(
            sound.ActiveMusic != OracleSoundEngine.MusOverworld ||
            sound.PlayRequestsFor(OracleSoundEngine.MusOverworld) != overworldRequests + 1,
            "Ordinary room music did not immediately cancel SNDCTRL_MEDIUM_FADEOUT.");
        for (int update = 0; update < 127; update++)
            sound.Tick();
        FailIf(
            sound.ActiveMusic != OracleSoundEngine.MusOverworld ||
            sound.PlayRequestsFor(OracleSoundEngine.MusOverworld) != overworldRequests + 1,
            "A cancelled SNDCTRL_MEDIUM_FADEOUT later stopped the replacement room music.");
        FailIf(
            soundAudit.Requests.Count < 3 ||
            soundAudit.Requests[^2] != OracleSoundEngine.SndCtrlMediumFadeOut ||
            soundAudit.Requests[^1] != OracleSoundEngine.MusOverworld,
            "Validation-owned sound observation lost request order.");

        var outputSound = new OracleSoundEngine(
            data, enableOutput: true, allowHeadlessOutput: true);
        AddChild(outputSound);
        try
        {
            FailIf(
                !outputSound.OutputResourcesActiveForValidation,
                "The output sound engine did not create its stream playback.");
            RemoveChild(outputSound);
            FailIf(
                outputSound.OutputResourcesActiveForValidation,
                "The output sound engine retained its player or stream playback after _ExitTree.");
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
            "title square releases, menu square and Link damage/fall wave SFX, " +
            "raw square/noise SFX including SND_MAKUDISAPPEAR, " +
            "channel priority, stop controls, and output teardown.");
    }

    private static void ValidateGraphicsCache()
    {
        ValidationGraphicsCacheAudit cacheAudit =
            ValidationGraphicsCacheAudit.Attach();
        string[] pngPaths = EnumeratePngPaths("res://assets/oracle")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        FailIf(
            pngPaths.Length < 200,
            $"Expected the complete generated PNG set, found only {pngPaths.Length} files.");

        foreach (string path in pngPaths)
        {
            Image resourceImage = OracleGraphicsCache.LoadImage(path);
            using Image rawImage = OracleGraphicsCache.LoadRawPngForValidation(path);
            FailIf(
                resourceImage.GetWidth() != rawImage.GetWidth() ||
                resourceImage.GetHeight() != rawImage.GetHeight() ||
                resourceImage.GetFormat() != rawImage.GetFormat() ||
                !resourceImage.GetData().AsSpan().SequenceEqual(rawImage.GetData()),
                $"ResourceLoader changed imported PNG pixels for {path}.");
        }

        const string sourcePath = "res://assets/oracle/gfx/spr_impa.png";
        int loadsBefore = cacheAudit.Count(
            OracleGraphicsCacheOperation.SourceLoad);
        int sourceObservationStart = cacheAudit.Observations.Count;
        Image source = OracleGraphicsCache.LoadImage(sourcePath);
        int loadsAfterFirst = cacheAudit.Count(
            OracleGraphicsCacheOperation.SourceLoad);
        Image sameSource = OracleGraphicsCache.LoadImage(sourcePath);
        FailIf(
            !ReferenceEquals(source, sameSource) ||
            cacheAudit.Count(OracleGraphicsCacheOperation.SourceLoad) !=
                loadsAfterFirst ||
            loadsAfterFirst - loadsBefore is < 0 or > 1,
            "Repeated graphics access did not return one cached CPU source image.");
        OracleGraphicsCacheObservation[] sourceObservations =
            cacheAudit.Observations
                .Skip(sourceObservationStart)
                .Where(observation => observation.Key == sourcePath)
                .ToArray();
        FailIf(
            sourceObservations.Length != 2 ||
            sourceObservations[^1].Operation !=
                OracleGraphicsCacheOperation.SourceHit,
            "Validation-owned graphics observation lost source cache-operation detail.");

        const string extraPath = "res://assets/oracle/gfx/spr_common_sprites.png";
        Image composite = OracleGraphicsCache.AppendGraphics(source, extraPath);
        Image sameComposite = OracleGraphicsCache.AppendGraphics(source, extraPath);
        int expectedExtraX = Mathf.CeilToInt(source.GetWidth() / 128.0f) * 128;
        Image extra = OracleGraphicsCache.LoadImage(extraPath);
        FailIf(
            !ReferenceEquals(composite, sameComposite) ||
            composite.GetWidth() != expectedExtraX + extra.GetWidth() ||
            composite.GetHeight() != Math.Max(source.GetHeight(), extra.GetHeight()),
            "Chained graphics did not preserve `$20-tile slot alignment or cache identity.");

        const string encodedOam = "8,0,0,0;8,8,2,32";
        string encodedAnimation = $"2@{encodedOam}|4@{encodedOam}~1";
        AnimationDefinition animation =
            OracleGraphicsCache.GetAnimationDefinition(encodedAnimation);
        AnimationDefinition sameAnimation =
            OracleGraphicsCache.GetAnimationDefinition(encodedAnimation);
        FailIf(
            !ReferenceEquals(animation, sameAnimation) ||
            animation.LoopStart != 1 || animation.Frames.Length != 2 ||
            animation.Frames[0].Duration != 2 || animation.Frames[1].Duration != 4 ||
            animation.Frames.Any(frame => frame.EncodedOam != encodedOam),
            "Encoded animation definitions were not parsed and cached immutably.");

        int buildsBefore = cacheAudit.Count(
            OracleGraphicsCacheOperation.OamFrameBuild);
        Texture2D cached = NpcCharacter.BuildOamTexture(source, encodedOam, 0, 1);
        int buildsAfterFirst = cacheAudit.Count(
            OracleGraphicsCacheOperation.OamFrameBuild);
        int hitsAfterFirst = cacheAudit.Count(
            OracleGraphicsCacheOperation.OamFrameHit);
        Texture2D sameCached = NpcCharacter.BuildOamTexture(source, encodedOam, 0, 1);
        FailIf(
            !ReferenceEquals(cached, sameCached) ||
            cacheAudit.Count(OracleGraphicsCacheOperation.OamFrameBuild) !=
                buildsAfterFirst ||
            cacheAudit.Count(OracleGraphicsCacheOperation.OamFrameHit) !=
                hitsAfterFirst + 1 ||
            buildsAfterFirst - buildsBefore is < 0 or > 1,
            "An identical OAM frame was rebuilt instead of reused.");

        using Texture2D uncached = NpcCharacter.BuildOamTextureUncachedForValidation(
            source, encodedOam, 0, 1);
        using Image cachedImage = cached.GetImage();
        using Image uncachedImage = uncached.GetImage();
        FailIf(
            cachedImage.GetWidth() != uncachedImage.GetWidth() ||
            cachedImage.GetHeight() != uncachedImage.GetHeight() ||
            !cachedImage.GetData().AsSpan().SequenceEqual(uncachedImage.GetData()),
            "Cached fixed OAM composition differs from the original compositor.");

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
            FailIf(
                positionedOffset != uncachedPositionedOffset ||
                positionedImage.GetWidth() != uncachedPositionedImage.GetWidth() ||
                positionedImage.GetHeight() != uncachedPositionedImage.GetHeight() ||
                !positionedImage.GetData().AsSpan().SequenceEqual(
                    uncachedPositionedImage.GetData()),
                "Cached positioned OAM composition differs from the original compositor.");
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
        Texture2D sourceOffsetVariant = NpcCharacter.BuildOamTexture(
            source, encodedOam, 0, 1, null, true, sourceOffset: 0x20);
        FailIf(
            ReferenceEquals(cached, positioned) ||
            ReferenceEquals(cached, paletteVariant) ||
            ReferenceEquals(cached, inversionVariant) ||
            ReferenceEquals(cached, sourceOffsetVariant),
            "OAM cache keys collapsed composition, palette, grayscale, " +
            "or object-header source-offset variants.");

        NpcRecord npcRecord = new NpcDatabase().GetRoomNpcs(0, 0x66).First();
        var firstNpc = new NpcCharacter();
        var secondNpc = new NpcCharacter();
        try
        {
            firstNpc.Initialize(npcRecord);
            int buildsAfterFirstNpc = cacheAudit.Count(
                OracleGraphicsCacheOperation.OamFrameBuild);
            secondNpc.Initialize(npcRecord);
            FailIf(
                cacheAudit.Count(OracleGraphicsCacheOperation.OamFrameBuild) !=
                    buildsAfterFirstNpc,
                "A second NPC instance rebuilt shared facing OAM frames.");

            firstNpc.SetScriptAnimation(npcRecord.DownAnimation);
            int buildsAfterFirstScriptSelection = cacheAudit.Count(
                OracleGraphicsCacheOperation.OamFrameBuild);
            firstNpc.SetScriptAnimation(npcRecord.DownAnimation);
            FailIf(
                cacheAudit.Count(OracleGraphicsCacheOperation.OamFrameBuild) !=
                    buildsAfterFirstScriptSelection,
                "Re-selecting a scripted NPC animation rebuilt its OAM textures.");
        }
        finally
        {
            firstNpc.Free();
            secondNpc.Free();
        }

        OracleGraphicsCache.SetObserver(null);
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
}
