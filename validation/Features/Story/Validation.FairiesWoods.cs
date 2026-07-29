using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private const double FairiesFrame = 1.0 / 60.0;

    private void ValidateFairiesWoodsSequence()
    {
        FairiesWoodsEvent fairies = _roomEvents.FairiesWoods;
        ValidateFairiesWoodsImportedData(fairies.Database);
        ValidateFairiesWoodsRelativeAngles();

        const int essenceByteAddress = 0xc6a2;
        FairiesWoodsEventRecord record = fairies.Database.Event;
        OracleRuntimeState runtime = _entities.RuntimeState;
        byte originalEssenceByte = _saveData.ReadWramByte(essenceByteAddress);
        bool originalCompletion = _saveData.HasGlobalFlag(record.CompletionFlag);
        bool originalUnscrambled = _saveData.HasGlobalFlag(record.UnscrambledFlag);
        byte[] originalTransient = new byte[0x10];
        for (int index = 0; index < originalTransient.Length; index++)
        {
            originalTransient[index] = runtime.ReadWramByte(
                record.ActiveAddress + index);
        }

        try
        {
            _dialogue.Close();
            _saveData.SetGlobalFlag(record.CompletionFlag, value: false);
            _saveData.SetGlobalFlag(record.UnscrambledFlag, value: false);
            _saveData.WriteWramByte(
                essenceByteAddress,
                (byte)(originalEssenceByte | 0x01));
            ClearFairiesWoodsTransient(record, runtime);

            ValidateFairiesWoodsScrambler(record);
            ValidateFairiesWoodsIntro(fairies, record, runtime);
            ValidateFairiesWoodsLiveScroll(fairies, record, runtime);
            ValidateFairiesWoodsExitChoice(fairies, record, runtime);
            ValidateFairiesWoodsRoom93Reset(record, runtime);

            runtime.SetWramByte(record.ActiveAddress, 1);
            runtime.SetWramByte(record.FoundAddress, 0);
            runtime.SetWramByte(record.SignalAddress, 0);
            ValidateFairiesWoodsHiddenFairy(
                fairies, record, runtime, room: 0x81, packedPosition: 0x25,
                fairyIndex: 3, lastFairy: false);
            ValidateDiscoveredFairies(
                fairies, record, expectedCount: 1, textId: 0x1108);
            ValidateFairiesWoodsHiddenFairy(
                fairies, record, runtime, room: 0x80, packedPosition: 0x54,
                fairyIndex: 4, lastFairy: false);
            ValidateDiscoveredFairies(
                fairies, record, expectedCount: 2, textId: 0x1109);
            ValidateFairiesWoodsHiddenFairy(
                fairies, record, runtime, room: 0x91, packedPosition: 0x32,
                fairyIndex: 5, lastFairy: true);
            ValidateFairiesWoodsCompletion(fairies, record);
        }
        finally
        {
            _dialogue.Close();
            LoadValidationRoom(0, 0x11);
            _saveData.WriteWramByte(essenceByteAddress, originalEssenceByte);
            _saveData.SetGlobalFlag(record.CompletionFlag, originalCompletion);
            _saveData.SetGlobalFlag(record.UnscrambledFlag, originalUnscrambled);
            for (int index = 0; index < originalTransient.Length; index++)
            {
                runtime.SetWramByte(
                    record.ActiveAddress + index,
                    originalTransient[index]);
            }
        }
    }

    private static void ValidateFairiesWoodsImportedData(
        FairiesWoodsDatabase database)
    {
        FairiesWoodsEventRecord record = database.Event;
        FailIf(
            record is not
            {
                Group: 0,
                StartRoom: 0x82,
                ExitRoom: 0x92,
                ResetRoom: 0x93,
                EssenceTreasure: TreasureDatabase.TreasureEssence,
                ActiveAddress: 0xcfd0,
                FoundAddress: 0xcfd1,
                SignalAddress: 0xcfd2,
                HiddenDelay: 12,
                NormalFadeOut: 32,
                NormalFadeIn: 33,
                FastFadeIn: 11,
                CompletionHold: 12,
                DelayedFadeIn: 257,
                NormalFadeSpeed: 1,
                FastFadeSpeed: 3,
                DelayedFadeRefill: 8
            },
            "Fairies' Woods event constants diverged from interaction $6c.");

        (int Y, int X, int Angle, int Counter, int TargetY, int TargetX,
            int Direction, int Palette)[] expectedMovements =
        [
            (0x38, 0x68, 0x00, 4, 0x48, 0x38, 0, 1),
            (0x28, 0x38, 0x04, 4, 0x48, 0x68, 1, 2),
            (0x58, 0x50, 0x14, 4, 0x38, 0x50, 1, 3),
            (0x28, 0x58, 0x18, 3, 0x48, 0x50, 0, 1),
            (0x58, 0x48, 0x14, 3, 0x48, 0x50, 1, 2),
            (0x38, 0x28, 0x04, 3, 0x48, 0x50, 1, 3),
            (0x48, 0x38, 0x10, 5, 0x00, 0x48, 0, 1),
            (0x48, 0x68, 0x00, 5, 0x38, 0x88, 1, 2),
            (0x38, 0x50, 0x08, 5, 0x58, 0x00, 1, 3),
            (0x48, 0x50, 0x10, 5, 0x00, 0xa0, 0, 1),
            (0x48, 0x50, 0x04, 6, 0x90, 0x60, 1, 2),
            (0x48, 0x50, 0x08, 4, 0x00, 0x00, 1, 3),
            (0x48, 0xa0, 0x10, 5, 0x28, 0x58, 0, 1),
            (0x60, 0xa8, 0x00, 5, 0x58, 0x48, 1, 2),
            (0x00, 0x78, 0x0c, 5, 0x38, 0x28, 1, 3),
            (0x90, 0x50, 0x1c, 4, 0x60, 0x40, 1, 1),
            (0x80, 0x50, 0x10, 4, 0x90, 0x80, 1, 1),
            (0x48, 0x58, 0x18, 4, 0x88, 0x10, 1, 1),
            (0x38, 0x78, 0x08, 4, 0x28, 0xa8, 0, 2),
            (0x58, 0x78, 0x08, 4, 0x88, 0xa0, 0, 3),
            (0x30, 0xa8, 0x18, 4, 0x20, 0x68, 1, 1),
            (0x80, 0x50, 0x18, 4, 0x90, 0x38, 1, 1)
        ];
        FailIf(
            database.Movements.Count != expectedMovements.Length,
            "Forest fairy movement row count changed.");
        for (int index = 0; index < expectedMovements.Length; index++)
        {
            FairiesWoodsMovementRecord actual = database.Movements[index];
            var expected = expectedMovements[index];
            FailIf(
                actual.Index != index ||
                (actual.InitialY, actual.InitialX, actual.Angle, actual.Counter,
                 actual.TargetY, actual.TargetX, actual.Direction, actual.Palette) !=
                expected,
                $"Forest fairy movement preset ${index:x2} diverged.");
        }

        (int Room, int Packed, int Fairy)[] expectedSpots =
            [(0x81, 0x25, 3), (0x80, 0x54, 4), (0x91, 0x32, 5)];
        foreach (var expected in expectedSpots)
        {
            FailIf(
                !database.TryHiddenSpot(
                    expected.Room, out FairiesWoodsHiddenSpotRecord actual) ||
                (actual.Room, actual.PackedPosition, actual.FairyIndex) != expected,
                $"Fairy hiding spot in room $0:${expected.Room:x2} diverged.");
        }

        (int Room, int Preset)[] expectedHidingRooms =
            [(0x81, 0x0c), (0x80, 0x0d), (0x91, 0x0e)];
        FailIf(
            database.HidingRooms.Count != expectedHidingRooms.Length,
            "Fairy hiding vignette count changed.");
        for (int index = 0; index < expectedHidingRooms.Length; index++)
        {
            FairiesWoodsHidingRoomRecord actual = database.HidingRooms[index];
            FailIf(
                actual.Index != index ||
                (actual.Room, actual.Preset) != expectedHidingRooms[index],
                $"Fairy hiding vignette ${index:x2} diverged.");
        }

        (int Y, int X, int Palette, string Animation)[] expectedDiscovered =
        [
            (0x48, 0x38, 1, record.Animation0),
            (0x48, 0x68, 2, record.Animation1),
            (0x28, 0x50, 3, record.Animation1)
        ];
        FailIf(
            database.DiscoveredFairies.Count != expectedDiscovered.Length,
            "Discovered-fairy row count changed.");
        for (int index = 0; index < expectedDiscovered.Length; index++)
        {
            FairiesWoodsDiscoveredRecord actual =
                database.DiscoveredFairies[index];
            var expected = expectedDiscovered[index];
            FailIf(
                actual.Index != index ||
                (actual.Y, actual.X, actual.Palette, actual.Animation) !=
                expected,
                $"Discovered fairy ${index:x2} diverged.");
        }

        ValidateCommandSourceRange(
            database.IntroCommands, 5670, 5690, expectedCount: 17);
        ValidateCommandSourceRange(
            database.RevealCommands, 5695, 5700, expectedCount: 6);
        ValidateCommandSourceRange(
            database.ExitCommands, 5705, 5719, expectedCount: 9);
    }

    private static void ValidateCommandSourceRange(
        IReadOnlyList<CutsceneCommand> commands,
        int firstLine,
        int lastLine,
        int expectedCount)
    {
        FailIf(commands.Count != expectedCount, "Fairy command stream row count changed.");
        int previous = firstLine - 1;
        foreach (CutsceneCommand command in commands)
        {
            int sourceLine = command.Source.SourceLine;
            FailIf(
                sourceLine < firstLine ||
                sourceLine > lastLine ||
                sourceLine <= previous,
                $"Fairy command source provenance is not ordered near {command.Source}.");
            previous = sourceLine;
        }
    }

    private static void ValidateFairiesWoodsRelativeAngles()
    {
        const int y = 0x50;
        const int x = 0x50;
        (int TargetY, int TargetX, int Angle)[] cases =
        [
            (0x30, x, 0x00),
            (0x30, 0x70, 0x05),
            (y, 0x70, 0x08),
            (0x70, 0x70, 0x0b),
            (0x70, x, 0x10),
            (0x70, 0x30, 0x15),
            (y, 0x30, 0x18),
            (0x30, 0x30, 0x1b)
        ];
        foreach (var test in cases)
        {
            int actual = OracleObjectMovement.Shared.RelativeAngle(
                (byte)y,
                (byte)x,
                (byte)test.TargetY,
                (byte)test.TargetX);
            FailIf(
                actual != test.Angle,
                $"objectGetRelativeAngleWithTempVars expected ${test.Angle:x2}, " +
                $"got ${actual:x2} toward ({test.TargetY:x2},{test.TargetX:x2}).");
        }
    }

    private void ValidateFairiesWoodsScrambler(FairiesWoodsEventRecord record)
    {
        var scrambler = new FairiesWoodsScramblerDatabase();
        int[,] expected =
        {
            { 0x70, 0x00, 0x71, 0x90, 0x00 },
            { 0x71, 0x00, 0x82, 0x91, 0x80 },
            { 0x72, 0x00, 0x00, 0x92, 0x82 },
            { 0x80, 0x72, 0x82, 0x80, 0x00 },
            { 0x81, 0x80, 0x82, 0x82, 0x71 },
            { 0x82, 0x70, 0x71, 0x82, 0x71 },
            { 0x90, 0x81, 0x92, 0x00, 0x00 },
            { 0x91, 0x72, 0x91, 0x00, 0x92 },
            { 0x92, 0x82, 0x00, 0x00, 0x92 }
        };
        Vector2I[] directions =
            [Vector2I.Up, Vector2I.Right, Vector2I.Down, Vector2I.Left];
        for (int row = 0; row < expected.GetLength(0); row++)
        for (int direction = 0; direction < directions.Length; direction++)
        {
            int destination = expected[row, direction + 1];
            bool resolved = scrambler.TryResolve(
                expected[row, 0], directions[direction], out int actual);
            FailIf(
                resolved != (destination != 0) ||
                (resolved && actual != destination),
                $"Forest scrambler room ${expected[row, 0]:x2} direction " +
                $"{direction} diverged.");
        }

        LoadValidationRoom(0, record.StartRoom);
        FailIf(
            !_transitions.TryGetScreenTransitionDestinationForValidation(
                Vector2I.Up, out int scrambled) ||
            scrambled != 0x70,
            "Room $0:$82 did not use the pre-completion scrambled north exit.");
        _saveData.SetGlobalFlag(record.UnscrambledFlag);
        FailIf(
            !_transitions.TryGetScreenTransitionDestinationForValidation(
                Vector2I.Up, out int ordinary) ||
            ordinary != 0x72,
            "The completed forest did not restore room $0:$82's ordinary north exit.");
        _saveData.SetGlobalFlag(record.UnscrambledFlag, value: false);
    }

    private void ValidateFairiesWoodsIntro(
        FairiesWoodsEvent fairies,
        FairiesWoodsEventRecord record,
        OracleRuntimeState runtime)
    {
        LoadValidationRoom(record.Group, record.StartRoom);
        FailIf(
            fairies.Stage != FairiesWoodsStage.StartPending ||
            fairies.FoundFairies != 0 ||
            fairies.SignalValue != 0,
            "Room $0:$82 did not arm the untouched fairy hide-and-seek intro.");

        AdvanceFairiesWoodsFrame();
        FailIf(
            fairies.Stage != FairiesWoodsStage.IntroScript ||
            fairies.Flights.Count != 1 ||
            fairies.Flights[0].PresetIndex != 0 ||
            !fairies.BlocksGameplay ||
            !_player.CutsceneControlled ||
            runtime.ReadWramByte(record.ActiveAddress) != 1,
            "The fairy intro did not create preset $00 and lock Link on " +
            "its first update.");

        HashSet<int> viewedRooms = [];
        bool[] reachedHidingSpots = [false, false, false];
        bool[] puffedAtHidingSpots = [false, false, false];
        int firstVignetteSnapshot = 0;
        for (int frame = 0;
             frame < 5000 && fairies.Stage != FairiesWoodsStage.SearchRoom;
             frame++)
        {
            int hidingIndex = HidingRoomIndex(
                fairies.Database.HidingRooms, _currentRoom.Id);
            int arrivedIndex = -1;
            if (hidingIndex >= 0)
            {
                FailIf(
                    _player.Visible,
                    $"Link remained visible in fairy vignette room " +
                    $"$0:${_currentRoom.Id:x2}.");
                if (fairies.Stage == FairiesWoodsStage.HidingFlight &&
                    fairies.Flights.Count == 1 &&
                    fairies.Flights[0].PresetIndex == 0x0c)
                {
                    ValidateFirstHidingFlightSnapshot(
                        fairies.Flights[0], firstVignetteSnapshot++);
                }
                if (fairies.Stage == FairiesWoodsStage.HidingFlight &&
                    fairies.SignalValue != 0 &&
                    fairies.Flights.Count == 1)
                {
                    ForestFairyFlight flight = fairies.Flights[0];
                    FairiesWoodsMovementRecord movement =
                        fairies.Database.Movements[flight.PresetIndex];
                    FailIf(
                        flight.Actor.Record.SubId != 0 ||
                        (byte)((ushort)flight.YFixed >> 8) != movement.TargetY ||
                        (byte)((ushort)flight.XFixed >> 8) != movement.TargetX ||
                        flight.Actor.Position !=
                            new Vector2(movement.TargetX, movement.TargetY),
                        $"Forest fairy $49:$00 preset " +
                        $"${flight.PresetIndex:x2} did not snap to the " +
                        "exact hiding tile before signaling.");
                    reachedHidingSpots[hidingIndex] = true;
                    arrivedIndex = hidingIndex;
                }
            }
            if (_dialogue.IsOpen)
                _dialogue.Close();
            AdvanceFairiesWoodsFrame();
            viewedRooms.Add(_currentRoom.Id);
            if (arrivedIndex >= 0)
            {
                FairiesWoodsMovementRecord movement =
                    fairies.Database.Movements[
                        fairies.Database.HidingRooms[arrivedIndex].Preset];
                PuzzlePuffEffect? puff =
                    _entities.Entities<PuzzlePuffEffect>().Find(candidate =>
                        !candidate.Finished &&
                        candidate.Position ==
                            new Vector2(movement.TargetX, movement.TargetY));
                FailIf(
                    fairies.Stage != FairiesWoodsStage.HidingRoomFadeOut ||
                    fairies.Flights.Count != 0 ||
                    puff is not { ElapsedUpdates: 1 },
                    $"Forest fairy vignette {arrivedIndex} did not replace " +
                    "the tile-centered fairy with INTERAC_PUFF $05 in the " +
                    "following interaction pass.");
                puffedAtHidingSpots[arrivedIndex] = true;
            }
        }
        FailIf(
            fairies.Stage != FairiesWoodsStage.SearchRoom ||
            _activeGroup != record.Group ||
            _currentRoom.Id != record.StartRoom ||
            !viewedRooms.IsSupersetOf([0x81, 0x80, 0x91]) ||
            !_dialogue.IsOpen ||
            !_player.Visible ||
            _player.CutsceneControlled ||
            fairies.BlocksGameplay ||
            !reachedHidingSpots[0] ||
            !reachedHidingSpots[1] ||
            !reachedHidingSpots[2] ||
            !puffedAtHidingSpots[0] ||
            !puffedAtHidingSpots[1] ||
            !puffedAtHidingSpots[2] ||
            firstVignetteSnapshot != 108 ||
            fairies.FoundFairies != 0 ||
            runtime.ReadWramByte(record.ActiveAddress) != 1,
            "The three-room CUTSCENE_FAIRIES_HIDE sequence did not return " +
            "to room $0:$82 with TX_1104 and active search state.");
        _dialogue.Close();
    }

    private static void ValidateFirstHidingFlightSnapshot(
        ForestFairyFlight flight,
        int snapshot)
    {
        int yFixed;
        int xFixed;
        int angle;
        int counter1;
        int counter2;
        int sparkle;
        switch (snapshot)
        {
            case 0:
                (yFixed, xFixed, angle, counter1, counter2, sparkle) =
                    (0x4800, 0xa000, 0x10, 5, 5, 0x5a);
                break;
            case 15:
                (yFixed, xFixed, angle, counter1, counter2, sparkle) =
                    (0x64b4, 0x9926, 0x13, 5, 5, 0x4b);
                break;
            case 31:
                (yFixed, xFixed, angle, counter1, counter2, sparkle) =
                    (0x797c, 0x81a5, 0x16, 4, 5, 0x3b);
                break;
            case 47:
                (yFixed, xFixed, angle, counter1, counter2, sparkle) =
                    (0x7c8b, 0x626a, 0x19, 3, 5, 0x2b);
                break;
            case 63:
                (yFixed, xFixed, angle, counter1, counter2, sparkle) =
                    (0x6cc2, 0x474c, 0x1c, 2, 5, 0x1b);
                break;
            case 79:
                (yFixed, xFixed, angle, counter1, counter2, sparkle) =
                    (0x5000, 0x3a98, 0x1f, 1, 5, 0x0b);
                break;
            case 95:
                (yFixed, xFixed, angle, counter1, counter2, sparkle) =
                    (0x31ac, 0x4224, 0x03, 1, 3, 0x55);
                break;
            case 99:
                (yFixed, xFixed, angle, counter1, counter2, sparkle) =
                    (0x2c52, 0x480b, 0x05, 3, 3, 0x51);
                break;
            case 103:
                (yFixed, xFixed, angle, counter1, counter2, sparkle) =
                    (0x2894, 0x4f0f, 0x06, 2, 3, 0x4d);
                break;
            case 104:
                (yFixed, xFixed, angle, counter1, counter2, sparkle) =
                    (0x27d1, 0x50e8, 0x06, 1, 3, 0x4c);
                break;
            case 105:
                (yFixed, xFixed, angle, counter1, counter2, sparkle) =
                    (0x276e, 0x52de, 0x07, 3, 3, 0x4b);
                break;
            case 106:
                (yFixed, xFixed, angle, counter1, counter2, sparkle) =
                    (0x270b, 0x54d4, 0x07, 2, 3, 0x4a);
                break;
            case 107:
                (yFixed, xFixed, angle, counter1, counter2, sparkle) =
                    (0x280b, 0x58d4, 0x07, 2, 3, 0x4a);
                break;
            default:
                return;
        }

        FailIf(
            (ushort)flight.YFixed != yFixed ||
            (ushort)flight.XFixed != xFixed ||
            flight.Angle != angle ||
            flight.Counter1 != counter1 ||
            flight.Counter2 != counter2 ||
            flight.SparkleCounter != sparkle,
            $"Forest fairy preset $0c diverged from the executed-ROM " +
            $"fixed-point checkpoint at snapshot {snapshot}: " +
            $"y=${(ushort)flight.YFixed:x4}, " +
            $"x=${(ushort)flight.XFixed:x4}, " +
            $"angle=${flight.Angle:x2}, c1=${flight.Counter1:x2}, " +
            $"c2=${flight.Counter2:x2}, " +
            $"sparkle=${flight.SparkleCounter:x2}.");
    }

    private void ValidateFairiesWoodsLiveScroll(
        FairiesWoodsEvent fairies,
        FairiesWoodsEventRecord record,
        OracleRuntimeState runtime)
    {
        ScrollFairiesWoods(Vector2I.Up, expectedRoom: 0x70);
        ScrollFairiesWoods(Vector2I.Down, expectedRoom: 0x90);
        ScrollFairiesWoods(Vector2I.Up, expectedRoom: 0x81);
        FailIf(
            fairies.Stage != FairiesWoodsStage.HiddenWatch ||
            fairies.HiddenCounter != record.HiddenDelay ||
            runtime.ReadWramByte(record.ActiveAddress) != 1,
            "Real scrambled screen transitions did not preserve the active " +
            "game and arm room $0:$81's hidden fairy.");
    }

    private void ScrollFairiesWoods(Vector2I direction, int expectedRoom)
    {
        FailIf(
            !_transitions.TryGetScreenTransitionDestinationForValidation(
                direction, out int target) ||
            target != expectedRoom,
            $"The live fairy route expected room ${expectedRoom:x2}, " +
            $"got ${target:x2}.");
        _transitions.BeginScroll(_player, direction, target);
        for (int frame = 0; frame < 60 && _transitions.ScrollActive; frame++)
        {
            _transitions.UpdateScroll(FairiesFrame);
            _entities.Update(FairiesFrame, _player);
            _roomEvents.Update(FairiesFrame);
        }
        FailIf(
            _transitions.ScrollActive ||
            _currentRoom.Id != expectedRoom,
            $"The live fairy route did not finish in room ${expectedRoom:x2}.");
    }

    private static int HidingRoomIndex(
        IReadOnlyList<FairiesWoodsHidingRoomRecord> rooms,
        int room)
    {
        for (int index = 0; index < rooms.Count; index++)
        {
            if (rooms[index].Room == room)
                return index;
        }
        return -1;
    }

    private void ValidateFairiesWoodsExitChoice(
        FairiesWoodsEvent fairies,
        FairiesWoodsEventRecord record,
        OracleRuntimeState runtime)
    {
        LoadValidationRoom(record.Group, record.ExitRoom);
        _player.WarpTo(new Vector2(record.ExitX, record.ExitY));
        AdvanceUntilFairyDialogue(fairies, 60, "initial forest-exit prompt");
        _dialogue.SubmitChoiceForValidation(1);
        AdvanceFairiesWoodsFrame();
        FailIf(
            !fairies.HasState ||
            runtime.ReadWramByte(record.ActiveAddress) != 1 ||
            !_player.CutsceneControlled,
            "Answering No at TX_110c did not force Link back into the forest.");
        for (int frame = 0; frame < 20; frame++)
            AdvanceFairiesWoodsFrame();
        FailIf(
            runtime.ReadWramByte(record.ActiveAddress) != 1,
            "The No branch incorrectly cleared fairy search state.");

        _player.WarpTo(new Vector2(record.ExitX, record.ExitY));
        AdvanceUntilFairyDialogue(fairies, 60, "repeated forest-exit prompt");
        _dialogue.SubmitChoiceForValidation(0);
        AdvanceFairiesWoodsFrame();
        for (int offset = 0; offset < 0x10; offset++)
        {
            FailIf(
                runtime.ReadWramByte(record.ActiveAddress + offset) != 0,
                $"The Yes exit branch did not clear $cf{0xd0 + offset:x2}.");
        }
        FailIf(
            fairies.HasState || _player.CutsceneControlled,
            "The accepted forest-exit prompt did not retire its controller.");
    }

    private void ValidateFairiesWoodsRoom93Reset(
        FairiesWoodsEventRecord record,
        OracleRuntimeState runtime)
    {
        for (int offset = 0; offset < 0x10; offset++)
            runtime.SetWramByte(record.ActiveAddress + offset, (byte)(offset + 1));
        LoadValidationRoom(record.Group, record.ResetRoom);
        for (int offset = 0; offset < 0x10; offset++)
        {
            FailIf(
                runtime.ReadWramByte(record.ActiveAddress + offset) != 0,
                "Room $0:$93 did not run its pre-completion fairy-state reset.");
        }
    }

    private void ValidateFairiesWoodsHiddenFairy(
        FairiesWoodsEvent fairies,
        FairiesWoodsEventRecord record,
        OracleRuntimeState runtime,
        int room,
        int packedPosition,
        int fairyIndex,
        bool lastFairy)
    {
        LoadValidationRoom(record.Group, room);
        FailIf(
            fairies.Stage != FairiesWoodsStage.HiddenWatch ||
            fairies.HiddenCounter != record.HiddenDelay,
            $"Room $0:${room:x2} did not arm hidden fairy ${fairyIndex:x2}.");

        Vector2 point = new(
            (packedPosition & 0x0f) * OracleRoomData.MetatileSize + 8,
            (packedPosition >> 4) * OracleRoomData.MetatileSize + 8);
        byte originalTile = _currentRoom.GetMetatile(point);
        byte changedTile = (byte)(originalTile ^ 0x01);
        FailIf(
            !_currentRoom.ReplaceMetatile(
            point, originalTile, changedTile, (long)_animationTicks),
            $"Could not expose fairy spot ${packedPosition:x2} in room $0:${room:x2}.");
        for (int frame = 0; frame < record.HiddenDelay - 1; frame++)
            AdvanceFairiesWoodsFrame();
        FailIf(
            fairies.Stage != FairiesWoodsStage.HiddenWatch ||
            fairies.HiddenCounter != 1,
            $"Hidden fairy ${fairyIndex:x2} did not preserve the 12-update delay.");
        AdvanceFairiesWoodsFrame();
        FailIf(
            fairies.Stage != FairiesWoodsStage.HiddenSpawn ||
            !fairies.ScreenTransitionsDisabled,
            $"Hidden fairy ${fairyIndex:x2} did not lock on update 12.");
        FailIf(
            !_currentRoom.ReplaceMetatile(
            point, changedTile, originalTile, (long)_animationTicks),
            $"Could not restore fairy spot ${packedPosition:x2}.");
        AdvanceFairiesWoodsFrame();
        FailIf(
            fairies.Stage != FairiesWoodsStage.RevealScript ||
            fairies.Flights.Count != 1 ||
            fairies.Flights[0].PresetIndex != fairyIndex,
            $"Fairy spot ${packedPosition:x2} did not spawn preset ${fairyIndex:x2}.");

        FairiesWoodsStage expectedStage = lastFairy
            ? FairiesWoodsStage.CompletionShowInitial
            : FairiesWoodsStage.Inactive;
        for (int frame = 0;
             frame < 2000 && fairies.Stage != expectedStage;
             frame++)
        {
            if (_dialogue.IsOpen)
                _dialogue.Close();
            AdvanceFairiesWoodsFrame();
        }
        int expectedFound = (1 << (fairyIndex - 2)) - 1;
        FailIf(
            fairies.Stage != expectedStage ||
            fairies.FoundFairies != expectedFound ||
            runtime.ReadWramByte(record.ActiveAddress) != 1,
            $"Fairy ${fairyIndex:x2} reveal did not commit found mask " +
            $"${expectedFound:x2} (stage={fairies.Stage}, " +
            $"found=${fairies.FoundFairies:x2}, " +
            $"active=${runtime.ReadWramByte(record.ActiveAddress):x2}, " +
            $"signal=${fairies.SignalValue:x2}).");
        FailIf(
            !lastFairy && (fairies.BlocksGameplay ||
            fairies.ScreenTransitionsDisabled ||
            _player.CutsceneControlled),
            $"Fairy ${fairyIndex:x2} reveal did not release gameplay.");
    }

    private void ValidateDiscoveredFairies(
        FairiesWoodsEvent fairies,
        FairiesWoodsEventRecord record,
        int expectedCount,
        int textId)
    {
        LoadValidationRoom(record.Group, record.StartRoom);
        List<NpcCharacter> discovered =
            _entities.Entities<NpcCharacter>().FindAll(npc =>
                npc.Name.ToString().StartsWith(
                    "FairiesWoods_01_", StringComparison.Ordinal));
        FailIf(
            fairies.Stage != FairiesWoodsStage.SearchRoom ||
            discovered.Count != expectedCount,
            $"Room $0:$82 did not restore {expectedCount} discovered fairies.");
        FailIf(
            !_roomEvents.FairiesWoods.TryInteractNpc(discovered[0]) ||
            !_dialogue.IsOpen ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(
                fairies.Database.Text(textId).Message),
            $"The {expectedCount}-fairy reunion did not show TX_{textId:x4}.");
        _dialogue.Close();
        ValidateDiscoveredFairyScrollRetention(
            fairies, record, expectedCount);
    }

    private void ValidateDiscoveredFairyScrollRetention(
        FairiesWoodsEvent fairies,
        FairiesWoodsEventRecord record,
        int expectedCount)
    {
        FailIf(
            !_transitions.TryGetScreenTransitionDestinationForValidation(
                Vector2I.Up, out int target) ||
            target != 0x70,
            "Room $0:$82 did not retain its imported upward forest route.");

        _transitions.BeginScroll(_player, Vector2I.Up, target);
        for (int frame = 0; frame < 60 && _transitions.ScrollActive; frame++)
        {
            List<NpcCharacter> outgoing =
                _entities.OutgoingEntities<NpcCharacter>().FindAll(npc =>
                    npc.Name.ToString().StartsWith(
                        "FairiesWoods_01_", StringComparison.Ordinal));
            FailIf(
                outgoing.Count != expectedCount ||
                outgoing.Exists(npc => !npc.Visible),
                $"Room $0:$82 lost one of its {expectedCount} discovered " +
                "fairies while the source screen was still scrolling.");
            _transitions.UpdateScroll(FairiesFrame);
            _entities.Update(FairiesFrame, _player);
            _roomEvents.Update(FairiesFrame);
        }
        FailIf(
            _transitions.ScrollActive || _currentRoom.Id != 0x70 ||
            _entities.OutgoingEntities<NpcCharacter>().Count != 0,
            "The discovered-fairy retention test did not finish its " +
            "room $0:$82 -> $0:$70 scroll cleanly.");

        ScrollFairiesWoods(Vector2I.Right, expectedRoom: 0x71);
        ScrollFairiesWoods(Vector2I.Right, expectedRoom: record.StartRoom);
        List<NpcCharacter> restored =
            _entities.Entities<NpcCharacter>().FindAll(npc =>
                npc.Name.ToString().StartsWith(
                    "FairiesWoods_01_", StringComparison.Ordinal));
        FailIf(
            fairies.Stage != FairiesWoodsStage.SearchRoom ||
            restored.Count != expectedCount ||
            restored.Exists(npc => !npc.Visible),
            $"Returning to room $0:$82 did not recreate all " +
            $"{expectedCount} discovered fairies.");
    }

    private void ValidateFairiesWoodsCompletion(
        FairiesWoodsEvent fairies,
        FairiesWoodsEventRecord record)
    {
        FailIf(!_transitions.IsTransitioning, "The third fairy did not start the room-$82 warp.");
        for (int frame = 0; frame < 120 && _transitions.IsTransitioning; frame++)
            UpdateRoomWarpTransition(FairiesFrame);
        FailIf(
            _transitions.IsTransitioning ||
            _activeGroup != record.Group ||
            _currentRoom.Id != record.StartRoom ||
            fairies.Stage != FairiesWoodsStage.CompletionWaitInitial ||
            !_dialogue.IsOpen ||
            _saveData.HasGlobalFlag(record.CompletionFlag) ||
            _saveData.HasGlobalFlag(record.UnscrambledFlag),
            "The third fairy warp did not open TX_110a before committing flags.");

        _dialogue.Close();
        AdvanceFairiesWoodsFrame();
        FailIf(
            fairies.Stage != FairiesWoodsStage.CompletionFastFade1,
            "The first 11-update fairy fade did not start.");
        AdvanceFairiesWoodsFrame();
        AssertFairyFadeAlpha(29.0f / 31.0f, "fast-fade first palette step");
        AdvanceFairiesWoodsFrames(record.FastFadeIn - 2);
        FailIf(
            fairies.Stage != FairiesWoodsStage.CompletionFastFade1,
            "The first fairy fade stopped before update 11.");
        AssertFairyFadeAlpha(2.0f / 31.0f, "fast-fade tenth palette step");
        AdvanceFairiesWoodsFrame();
        FailIf(
            fairies.Stage != FairiesWoodsStage.CompletionHold1,
            "The first fairy fade was not exactly 11 updates.");
        AssertFairyFadeAlpha(0.0f, "fast-fade completion");
        AdvanceFairiesWoodsFrames(record.CompletionHold - 1);
        FailIf(
            fairies.Stage != FairiesWoodsStage.CompletionHold1,
            "The first fairy hold ended before update 12.");
        AdvanceFairiesWoodsFrame();
        FailIf(
            fairies.Stage != FairiesWoodsStage.CompletionFastFade2,
            "The second fairy fade did not start on hold 12.");
        AdvanceFairiesWoodsFrames(record.FastFadeIn);
        FailIf(
            fairies.Stage != FairiesWoodsStage.CompletionHold2,
            "The second fairy fade was not exactly 11 updates.");
        AdvanceFairiesWoodsFrames(record.CompletionHold - 1);
        FailIf(
            fairies.Stage != FairiesWoodsStage.CompletionHold2,
            "The second fairy hold ended before update 12.");
        AdvanceFairiesWoodsFrame();
        FailIf(
            fairies.Stage != FairiesWoodsStage.CompletionSlowFade,
            "The 257-update delayed fade did not start.");
        AssertFairyFadeAlpha(1.0f, "delayed-fade initialization");
        AdvanceFairiesWoodsFrames(248);
        FailIf(
            fairies.Stage != FairiesWoodsStage.CompletionSlowFade,
            "The delayed fairy fade stopped before update 257.");
        AssertFairyFadeAlpha(1.0f / 31.0f, "delayed-fade update 248");
        AdvanceFairiesWoodsFrame();
        AssertFairyFadeAlpha(0.0f, "delayed-fade visible completion");
        AdvanceFairiesWoodsFrames(record.DelayedFadeIn - 250);
        FailIf(
            fairies.Stage != FairiesWoodsStage.CompletionSlowFade,
            "The delayed fairy fade retired during its final counter delay.");
        AdvanceFairiesWoodsFrame();
        FailIf(
            fairies.Stage != FairiesWoodsStage.CompletionFinalize ||
            !_dialogue.IsOpen ||
            _saveData.HasGlobalFlag(record.CompletionFlag) ||
            _saveData.HasGlobalFlag(record.UnscrambledFlag),
            "TX_110b or the final flag boundary diverged after the delayed fade.");
        AdvanceFairiesWoodsFrame();
        FailIf(
            fairies.Stage != FairiesWoodsStage.Inactive ||
            fairies.BlocksGameplay ||
            _player.CutsceneControlled ||
            !_saveData.HasGlobalFlag(record.CompletionFlag) ||
            !_saveData.HasGlobalFlag(record.UnscrambledFlag),
            "Fairy completion did not set both global flags and release Link.");
        _dialogue.Close();

        LoadValidationRoom(record.Group, record.StartRoom);
        FailIf(
            !_transitions.TryGetScreenTransitionDestinationForValidation(
                Vector2I.Up, out int north) ||
            north != 0x72,
            "The completed hide-and-seek game did not permanently unscramble the forest.");
    }

    private void AssertFairyFadeAlpha(float expected, string boundary)
    {
        FailIf(
            !Mathf.IsEqualApprox(_warpFade.Color.A, expected),
            $"Fairies' Woods {boundary} expected white alpha {expected}, " +
            $"got {_warpFade.Color.A}.");
    }

    private void AdvanceUntilFairyDialogue(
        FairiesWoodsEvent fairies,
        int frameLimit,
        string description)
    {
        for (int frame = 0; frame < frameLimit && !_dialogue.IsOpen; frame++)
            AdvanceFairiesWoodsFrame();
        FailIf(
            !_dialogue.IsOpen || fairies.Stage != FairiesWoodsStage.ExitScript,
            $"Fairies' Woods did not reach the {description}.");
    }

    private void AdvanceFairiesWoodsFrames(int frames)
    {
        for (int frame = 0; frame < frames; frame++)
            AdvanceFairiesWoodsFrame();
    }

    private void AdvanceFairiesWoodsFrame()
    {
        _entities.Update(FairiesFrame, _player);
        _roomEvents.Update(FairiesFrame);
    }

    private static void ClearFairiesWoodsTransient(
        FairiesWoodsEventRecord record,
        OracleRuntimeState runtime)
    {
        for (int offset = 0; offset < 0x10; offset++)
            runtime.SetWramByte(record.ActiveAddress + offset, 0);
    }
}
