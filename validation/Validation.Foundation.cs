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
            _hud.Position != Vector2.Zero || _hud.ZIndex != 20 ||
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
            _roomDebug.GetThemeConstant("outline_size") != 1)
        {
            throw new InvalidOperationException(
                $"{GameSceneGraph.ScenePath} no longer preserves the fixed camera, UI, or draw-order values.");
        }

        Vector2 gameplayPosition =
            _transitions.WorldToGameplayScreen(_player.Position);
        if (!_transitions.WorldToScreen(_player.Position).IsEqualApprox(
            gameplayPosition + new Vector2(0, OracleRoomData.GameplayScreenTop)))
        {
            throw new InvalidOperationException(
                "WorldToScreen did not preserve gameplay coordinates while adding the top-HUD offset.");
        }

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
        var singleTileChanges = new SingleTileChangeDatabase();
        var warps = new WarpDatabase();
        static Vector2 Point(int position) => new(
            (position & 0x0f) * OracleRoomData.MetatileSize + 8,
            (position >> 4) * OracleRoomData.MetatileSize + 8);

        Vector2 doorPoint = Point(0x23);
        OracleRoomData room = rooms.CurrentRoom;

        if (changes.RuleCount != 44 || changes.RoomCount != 35 ||
            singleTileChanges.RecordCount != 56 ||
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

        // applySingleTileChanges treats $f0/$f1 as unlinked/linked predicates
        // and $f2 as the executed GLOBALFLAG_FINISHEDGAME check.
        var predicateSave = OracleSaveData.CreateStandardGame();
        var predicateRooms = new RoomSession(
            0, 0x48, () => 0, () => { }, predicateSave);
        if (predicateRooms.CurrentRoom.GetMetatile(Point(0x28)) != 0x64)
            throw new InvalidOperationException(
                "Single-tile predicate $f0 did not apply room 0:48's unlinked-only write.");
        predicateSave.WriteWramByte(0xc612, 1);
        room = predicateRooms.Load(0, 0x48);
        if (room.GetMetatile(Point(0x28)) != room.GetOriginalMetatile(Point(0x28)) ||
            predicateRooms.Load(3, 0xd6).GetMetatile(Point(0x55)) != 0xe9)
        {
            throw new InvalidOperationException(
                "Single-tile predicates $f0/$f1 did not switch with wIsLinkedGame.");
        }
        predicateSave.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame);
        if (predicateRooms.Load(0, 0x47).GetMetatile(Point(0x36)) != 0xf2)
            throw new InvalidOperationException(
                "Single-tile predicate $f2 did not follow GLOBALFLAG_FINISHEDGAME.");

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

        GD.Print("Validated 56 single-tile changes and 44 imported rules for 35 " +
            "room-specific tile changers: " +
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
        OracleSoundData.ChannelStart[] openMenu = data.ChannelsFor(
            OracleSoundEngine.SndOpenMenu).ToArray();
        OracleSoundData.ChannelStart[] makuDisappear = data.ChannelsFor(
            OracleSoundEngine.SndMakuDisappear).ToArray();
        OracleSoundData.ChannelStart[] damageLink = data.ChannelsFor(
            OracleSoundEngine.SndDamageLink).ToArray();
        OracleSoundData.ChannelStart[] linkFall = data.ChannelsFor(
            OracleSoundEngine.SndLinkFall).ToArray();
        if (title.Length != 4 ||
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

        sound.PlaySound(OracleSoundEngine.SndOpenMenu);
        sound.Tick();
        OracleSoundEngine.ChannelState openMenuHigh = sound.Channel(2);
        OracleSoundEngine.ChannelState openMenuLow = sound.Channel(3);
        if (!openMenuHigh.Active || !openMenuHigh.Gate || openMenuHigh.Priority != 1 ||
            openMenuHigh.DutyOrWaveform != 1 || openMenuHigh.Volume != 15 ||
            openMenuHigh.OutputVolume != 1 || openMenuHigh.Envelope != 3 ||
            openMenuHigh.PitchSlide != 0x23 ||
            openMenuHigh.CurrentFrequencyRegister != 0x0416 || openMenuHigh.WaitFrames != 0x15 ||
            !openMenuLow.Active || !openMenuLow.Gate || openMenuLow.Priority != 1 ||
            openMenuLow.DutyOrWaveform != 2 || openMenuLow.Volume != 15 ||
            openMenuLow.OutputVolume != 1 || openMenuLow.Envelope != 3 ||
            openMenuLow.PitchSlide != 0x2c ||
            openMenuLow.CurrentFrequencyRegister != 0x002d || openMenuLow.WaitFrames != 0x15)
        {
            throw new InvalidOperationException(
                "SND_OPENMENU did not start its paired C3/C2 square-channel sweep.");
        }

        sound.PlaySound(OracleSoundEngine.SndDamageLink);
        sound.Tick();
        OracleSoundEngine.ChannelState linkVoice = sound.Channel(5);
        if (!linkVoice.Active || !linkVoice.Gate || linkVoice.Priority != 1 ||
            linkVoice.DutyOrWaveform != 0x2d || linkVoice.PitchShift != -3 ||
            linkVoice.CurrentFrequencyRegister != 0x050f || linkVoice.WaitFrames != 0)
        {
            throw new InvalidOperationException(
                "SND_DAMAGE_LINK did not start its shifted F2 wave-channel cry: " +
                $"active={linkVoice.Active}, gate={linkVoice.Gate}, priority={linkVoice.Priority}, " +
                $"waveform=${linkVoice.DutyOrWaveform:x2}, shift={linkVoice.PitchShift}, " +
                $"frequency=${linkVoice.CurrentFrequencyRegister:x4}, wait={linkVoice.WaitFrames}.");
        }

        sound.PlaySound(OracleSoundEngine.SndLinkFall);
        sound.Tick();
        if (!linkVoice.Active || !linkVoice.Gate || linkVoice.Priority != 1 ||
            linkVoice.DutyOrWaveform != 0x03 || linkVoice.PitchShift != 0 ||
            linkVoice.CurrentFrequencyRegister != 0x07c1 || linkVoice.WaitFrames != 1)
        {
            throw new InvalidOperationException(
                "SND_LINK_FALL did not start its two-update C6 wave-channel descent: " +
                $"active={linkVoice.Active}, gate={linkVoice.Gate}, priority={linkVoice.Priority}, " +
                $"waveform=${linkVoice.DutyOrWaveform:x2}, shift={linkVoice.PitchShift}, " +
                $"frequency=${linkVoice.CurrentFrequencyRegister:x4}, wait={linkVoice.WaitFrames}.");
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

        sound.PlaySound(OracleSoundEngine.MusTitlescreen);
        int overworldRequests = sound.PlayRequestsFor(OracleSoundEngine.MusOverworld);
        sound.PlaySound(OracleSoundEngine.SndCtrlMediumFadeOut);
        sound.PlayMusicIfChanged(OracleSoundEngine.MusOverworld);
        if (sound.ActiveMusic != OracleSoundEngine.MusOverworld ||
            sound.PlayRequestsFor(OracleSoundEngine.MusOverworld) != overworldRequests + 1)
        {
            throw new InvalidOperationException(
                "Ordinary room music did not immediately cancel SNDCTRL_MEDIUM_FADEOUT.");
        }
        for (int update = 0; update < 127; update++)
            sound.Tick();
        if (sound.ActiveMusic != OracleSoundEngine.MusOverworld ||
            sound.PlayRequestsFor(OracleSoundEngine.MusOverworld) != overworldRequests + 1)
        {
            throw new InvalidOperationException(
                "A cancelled SNDCTRL_MEDIUM_FADEOUT later stopped the replacement room music.");
        }

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
            "title square releases, menu square and Link damage/fall wave SFX, " +
            "raw square/noise SFX including SND_MAKUDISAPPEAR, " +
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
}
