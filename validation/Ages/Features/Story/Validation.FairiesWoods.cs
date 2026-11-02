
namespace Oracles;

public sealed partial class ValidationRoot
{
    private const double FairiesFrame = 1.0 / 60.0;

    private void ValidateFairiesWoodsNativeControl()
    {
        foreach (bool batched in new[] { false, true })
        {
            InitializeCompletedIntroRoom(0, 0x11);
            _saveData.WriteWramByte(0xc6a2, 1); // TREASURE_ESSENCE bit0.
            var fairies = _roomEvents.Get<FairiesWoodsEvent>();
            var record = fairies.Database.Event;
            _runtimeState.SetWramByte(record.ActiveAddress, 1);
            _runtimeState.SetWramByte(record.FoundAddress, 7);
            LoadValidationRoom(record.Group, record.StartRoom);
            _player.WarpTo(new(80, 112));
            for (int update = 0; update < 4 && !_dialogue.IsOpen; update++)
                StepGameplayUpdates(1, Vector2.Zero);
            FailIf(fairies.Stage != FairiesWoodsStage.CompletionWaitInitial ||
                _runtimeState.ReadField("wDisabledObjects") != 0x21 ||
                _runtimeState.ReadField("wMenuDisabled") != 0x80 ||
                _player.CutsceneControlled ||
                _entities.Entities<NpcCharacter>().Count(npc => npc.Name.ToString().StartsWith("FairiesWoods_01_", StringComparison.Ordinal)) != 3,
                "Interaction $6c:$00 must publish $21/$80 and create three discovered fairies before cutscene $13.");
            CompareFairyCompletionWithNative(fairies, record, batched);
            StepGameplayUpdates(1, Vector2.Zero, held: ["inventory"], pressed: ["inventory"]);
            FailIf(_inventoryMenu.IsActive || !((IGameplayForegroundPhases)this).StatusBarUpdatesAllowed,
                "The normal handler must resume after cutscene $13, while TX_110b still blocks Start.");
            _dialogue.Close();
            StepGameplayUpdates(1, Vector2.Zero);
            StepGameplayUpdates(1, Vector2.Zero, held: ["inventory"], pressed: ["inventory"]);
            StepGameplayUpdates(22, Vector2.Zero, batched: batched);
            FailIf(!_inventoryMenu.IsOpen || !_gameplayPause.IsLeased,
                "Releasing TX_110b must restore the shared Start menu without a private fairy control owner.");
            StepGameplayUpdates(1, Vector2.Zero, held: ["inventory"], pressed: ["inventory"]);
            StepGameplayUpdates(22, Vector2.Zero, batched: batched);
            FailIf(_inventoryMenu.IsActive || _gameplayPause.IsLeased,
                "The shared Start menu must release its normal pause after fairy completion.");
        }

        foreach (bool replace in new[] { false, true })
        {
            InitializeCompletedIntroRoom(0, 0x11);
            _saveData.WriteWramByte(0xc6a2, 1);
            var fairies = _roomEvents.Get<FairiesWoodsEvent>();
            var record = fairies.Database.Event;
            _runtimeState.SetWramByte(record.ActiveAddress, 1);
            LoadValidationRoom(0, 0x81);
            _player.WarpTo(new(72, 88));
            FailIf(_collision.Collides(_player.Position),
                "Hidden fairy cancellation must start Link on actual forest floor.");
            StepGameplayUpdates(1, Vector2.Zero); // $6c:$01 state0 captures the unchanged tile.
            Vector2 hidden = new(88, 40); // $6c:$01 table position $25.
            byte tile = _currentRoom.GetMetatile(hidden);
            FailIf(!_currentRoom.ReplaceMetatile(hidden, tile, (byte)(tile ^ 1), (long)_animationTicks),
                "Hidden fairy cancellation requires its actual changed metatile.");
            StepGameplayUpdates(12, Vector2.Zero, batched: true);
            FailIf(fairies.Stage != FairiesWoodsStage.RevealScript ||
                _runtimeState.ReadField("wDisabledObjects") != 0x21 ||
                _runtimeState.ReadField("wMenuDisabled") != 0x80 ||
                _runtimeState.ReadField("wDisableScreenTransitions") != 1,
                "Hidden fairy update $0c must own the shared object, menu and transition controls.");
            var successor = new NativeScriptInputControl(_runtimeState);
            if (replace)
            {
                successor.SetDisabledObjects(0x21);
                successor.SetMenuDisabled(0x80);
                successor.SetScreenTransitionsDisabled(1);
            }
            fairies.Cancel();
            FailIf(_runtimeState.ReadField("wDisabledObjects") != (replace ? 0x21 : 0) ||
                _runtimeState.ReadField("wMenuDisabled") != (replace ? 0x80 : 0) ||
                _runtimeState.ReadField("wDisableScreenTransitions") != (replace ? 1 : 0),
                "Fairy cancellation must release its controls and retain later identical writes.");
            successor.Cancel();
            Vector2 before = _player.PrecisePosition;
            StepGameplayUpdates(2, Vector2.Down);
            FailIf(_player.PrecisePosition.Y <= before.Y || _player.CutsceneControlled,
                "Cancellation must return Link to ordinary movement on the actual forest floor.");
        }
    }

    private void ValidateFairiesWoodsSequence()
    {
        FairiesWoodsEvent fairies = _roomEvents.Get<FairiesWoodsEvent>();
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
                EssenceTreasure: TreasureId.Essence,
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
            !_transitions.TryGetScreenTransitionDestination(
                Vector2I.Up, out int scrambled) ||
            scrambled != 0x70,
            "Room $0:$82 did not use the pre-completion scrambled north exit.");
        _saveData.SetGlobalFlag(record.UnscrambledFlag);
        FailIf(
            !_transitions.TryGetScreenTransitionDestination(
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
        var controller = _entities.EntityAdapters<FairyHidingMinigameRoomEntity>().Single();
        FailIf(controller.State != 0 || _entities.NativeInteractionSlot(controller) != 2 || fairies.Flights.Count != 0,
            "Room $0:$82 must allocate its leading $6c:$00 in slot $d2 before state0 or children.");
        AdvanceFairiesWoodsFrame();
        FailIf(
            fairies.Stage != FairiesWoodsStage.StartPending ||
            fairies.FoundFairies != 0 ||
            fairies.SignalValue != 0,
            "Room $0:$82 did not arm the untouched fairy hide-and-seek intro.");

        AdvanceFairiesWoodsFrame();
        FailIf(controller.State != 2 || fairies.Flights.Count != 0 || runtime.ReadWramByte(record.ActiveAddress) != 1,
            "$6c:$00 state1 must install its script without executing its first spawn command.");
        AdvanceFairiesWoodsFrame();
        FailIf(
            fairies.Stage != FairiesWoodsStage.IntroScript ||
            fairies.Flights.Count != 1 ||
            fairies.Flights[0].PresetIndex != 0 ||
            !fairies.BlocksGameplay ||
            _player.CutsceneControlled || runtime.ReadField("wDisabledObjects") != 0x21 ||
            runtime.ReadField("wMenuDisabled") != 0x80 ||
            runtime.ReadWramByte(record.ActiveAddress) != 1,
            "The fairy intro did not create preset $00 and lock Link on " +
            "its first script update.");

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
                StationaryDebrisEffect? puff =
                    _entities.Entities<StationaryDebrisEffect>().Find(candidate =>
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
            !_transitions.TryGetScreenTransitionDestination(
                direction, out int target) ||
            target != expectedRoom,
            $"The live fairy route expected room ${expectedRoom:x2}, " +
            $"got ${target:x2}.");
        _transitions.BeginValidationScroll(_player, direction, target);
        for (int frame = 0; frame < 60 && _transitions.ScrollActive; frame++)
        {
            _transitions.UpdateScroll(FairiesFrame);
            _entities.AdvanceEntityFrames(FairiesFrame, _player);
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
        _player.WarpTo(new Vector2(record.ExitX - 24, record.ExitY));
        ApproachFairyExit(fairies, "initial forest-exit prompt");
        Vector2 before = _player.PrecisePosition;
        _dialogue.SubmitChoiceForValidation(1);
        AdvanceFairiesWoodsFrame();
        FailIf(
            !fairies.HasState ||
            runtime.ReadWramByte(record.ActiveAddress) != 1 ||
            _player.CutsceneControlled || _player.PrecisePosition != before ||
            runtime.ReadField("wDisabledObjects") != 0 || runtime.ReadField("wMenuDisabled") != 0,
            "Answering No at TX_110c did not force Link back into the forest.");
        AdvanceFairiesWoodsFrame(); // checkLinkForceState consumes the request.
        FailIf(_player.PrecisePosition != before || !_player.NativeForcedMovementActive,
            "The forest exit's state $0b request must be consumed before movement initializes.");
        for (int frame = 1; frame <= 8; frame++)
        {
            AdvanceFairiesWoodsFrame();
            FailIf(_player.PrecisePosition != before - new Vector2(Math.Min(frame, 7), 0) ||
                _player.NativeForcedMovementActive != (frame < 8),
                $"Forest exit state $0b update{frame}: parameter $08 must make seven SPEED_100 moves before normal Link resumes.");
        }
        FailIf(
            runtime.ReadWramByte(record.ActiveAddress) != 1,
            "The No branch incorrectly cleared fairy search state.");

        ApproachFairyExit(fairies, "repeated forest-exit prompt");
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

    private void ApproachFairyExit(FairiesWoodsEvent fairies, string description)
    {
        FailIf(_collision.Collides(_player.Position),
            "The fairy exit prompt must be approached through actual room $0:$92 floor.");
        for (int update = 0; update < 60 && !_dialogue.IsOpen; update++)
            StepGameplayUpdates(1, Vector2.Right, held: ["move_right"]);
        FailIf(!_dialogue.IsOpen || fairies.Stage != FairiesWoodsStage.ExitScript ||
            _player.InvincibilityFrames >= 0 || _player.KnockbackFrames != 0,
            $"Fairies' Woods did not reach the {description} through its native collision/func_0c_4177 handoff.");
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
        AdvanceFairiesWoodsFrame(); // Source state0 captures the tile before it changes.
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
            fairies.Stage != FairiesWoodsStage.RevealScript ||
            runtime.ReadField("wDisableScreenTransitions") != 1,
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

        FairiesWoodsStage expectedStage = FairiesWoodsStage.Inactive;
        for (int frame = 0;
             frame < 2000 && (lastFairy ? !_transitions.IsTransitioning : fairies.Stage != expectedStage);
             frame++)
        {
            if (_dialogue.IsOpen)
            {
                FailIf(_dialogue.Position.Y != 96,
                    $"Fairy ${fairyIndex:x2} discovery must retain the source bottom position, got Y={_dialogue.Position.Y}.");
                _dialogue.Close();
            }
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
            runtime.ReadField("wDisableScreenTransitions") != 0 ||
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
        AdvanceFairiesWoodsFrames(2); // Parent allocation, then discovered actors' script setup.
        List<NpcCharacter> discovered =
            _entities.Entities<NpcCharacter>().FindAll(npc =>
                npc.Name.ToString().StartsWith(
                    "FairiesWoods_01_", StringComparison.Ordinal));
        FailIf(
            fairies.Stage != FairiesWoodsStage.SearchRoom ||
            discovered.Count != expectedCount,
            $"Room $0:$82 did not restore {expectedCount} discovered fairies.");
        NpcCharacter first = discovered.Single(npc => npc.Record.Var03 == 0);
        byte originalFound = _runtimeState.ReadWramByte(record.FoundAddress);
        try
        {
            // The source chooses its script in state0, retaining TX_1108 even
            // if the live found count changes before this actor is replaced.
            if (expectedCount == 1) _runtimeState.SetWramByte(record.FoundAddress, 3);
            _player.WarpTo(first.Position + new Vector2(0, 32));
            for (int repeat = 0; repeat < 2; repeat++)
            {
                FailIf(_collision.Collides(_player.Position) || _entities.BlocksLink(_player.Position),
                    "The discovered fairy approach must start on actual floor outside its collision box.");
                StepGameplayUpdates(32, Vector2.Up, held: ["move_up"]);
                FailIf(_collision.Collides(_player.Position) || _entities.BlocksLink(_player.Position) ||
                    _entities.FindTalkTarget(_player) != first,
                    $"The discovered fairy cannot be approached through room $0:$82 floor; Link={_player.Position}.");
                StepGameplayUpdates(1, Vector2.Zero, held: ["attack"], pressed: ["attack"]);
                FailIf(!_dialogue.IsOpen || _dialogue.CurrentMessage != DialogueBox.PlainText(fairies.Database.Text(textId).Message) ||
                    _player.InvincibilityFrames != -128 || _player.KnockbackFrames != 0 || _player.CutsceneControlled ||
                    _runtimeState.ReadField("wDisabledObjects") != 0 || _runtimeState.ReadField("wMenuDisabled") != 0,
                    $"Discovered fairy repeat{repeat} must consume A in its slot, run func_0c_4177 and show captured TX_{textId:x4} without an input mask.");
                _dialogue.Close();
                StepGameplayUpdates(128, Vector2.Zero);
                StepGameplayUpdates(16, Vector2.Down, held: ["move_down"]);
            }
        }
        finally { _runtimeState.SetWramByte(record.FoundAddress, originalFound); }
        ValidateDiscoveredFairyScrollRetention(
            fairies, record, expectedCount);
    }

    private void ValidateDiscoveredFairyScrollRetention(
        FairiesWoodsEvent fairies,
        FairiesWoodsEventRecord record,
        int expectedCount)
    {
        FailIf(
            !_transitions.TryGetScreenTransitionDestination(
                Vector2I.Up, out int target) ||
            target != 0x70,
            "Room $0:$82 did not retain its imported upward forest route.");

        _transitions.BeginValidationScroll(_player, Vector2I.Up, target);
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
            _entities.AdvanceEntityFrames(FairiesFrame, _player);
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
        for (int frame = 0; frame < 120 && (_transitions.IsTransitioning || fairies.Stage != FairiesWoodsStage.CompletionWaitInitial); frame++)
            AdvanceFairiesWoodsFrame();
        FailIf(
            _transitions.IsTransitioning ||
            _activeGroup != record.Group ||
            _currentRoom.Id != record.StartRoom ||
            fairies.Stage != FairiesWoodsStage.CompletionWaitInitial ||
            !_dialogue.IsOpen ||
            _saveData.HasGlobalFlag(record.CompletionFlag) ||
            _saveData.HasGlobalFlag(record.UnscrambledFlag),
            $"The third fairy warp did not open TX_110a before committing flags (stage={fairies.Stage}, transition={_transitions.IsTransitioning}, palette={_transitions.PaletteFadeActive}, Link={_player.Position}).");

        CompareFairyCompletionWithNative(fairies, record, batched: false);
        _dialogue.Close();

        LoadValidationRoom(record.Group, record.StartRoom);
        FailIf(
            !_transitions.TryGetScreenTransitionDestination(
                Vector2I.Up, out int north) ||
            north != 0x72,
            "The completed hide-and-seek game did not permanently unscramble the forest.");
    }

    private void CompareFairyCompletionWithNative(FairiesWoodsEvent fairies, FairiesWoodsEventRecord record, bool batched)
    {
        var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0,
            Mathf.FloorToInt(_player.Position.X), Mathf.FloorToInt(_player.Position.Y));
        int stateAddress = _runtimeState.FieldAddress("wCutsceneState");
        const int indexAddress = 0xc2ef; // wThreadStateBuffer+$0f, wCutsceneIndex.
        int paletteAddress = _runtimeState.FieldAddress("wPaletteThread_mode");
        int counterAddress = _runtimeState.FieldAddress("wTmpcbb6");
        var palette = native.CreateMenuView();
        palette.SeedRetainedSpritePalette(7, [0, 0, 0, 0]);
        native[0xffa7] = 0x80;
        native.LatchDisplayedObjectPalettes();
        native[0xcfd0] = 1; native[0xcfd1] = 7;
        native[0xcc8a] = 0x21; native[0xcc02] = 0x80;
        native[stateAddress] = 0; native[indexAddress] = 0x13;
        native.AdvanceFairyHidingCutscene();
        FailIf(native[stateAddress] != 1 || native.TextGeneration != 1,
            "Native fairy completion must open TX_110a and enter state1 before the text wait.");
        int initialMysterySounds = _sound.PlayRequestsFor(record.MysterySound);
        _dialogue.Close(); native[0xcba0] = 0;
        bool active = true;
        int updates = 0;
        void Compare()
        {
            if (!active) return;
            native.AdvancePaletteThread();
            native.AdvanceFairyHidingCutscene();
            native.LatchDisplayedObjectPalettes();
            updates++;
            int expectedState = fairies.Stage switch
            {
                FairiesWoodsStage.CompletionFastFade1 or FairiesWoodsStage.CompletionHold1 => 2,
                FairiesWoodsStage.CompletionFastFade2 or FairiesWoodsStage.CompletionHold2 => 3,
                FairiesWoodsStage.CompletionSlowFade => 4,
                FairiesWoodsStage.CompletionFinalize or FairiesWoodsStage.Inactive => 5,
                _ => -1
            };
            active = native[indexAddress] == 0x13;
            FailIf(expectedState != native[stateAddress] || fairies.NativeActive != active ||
                _transitions.NativePaletteThreadMode != native[paletteAddress] ||
                fairies.CompletionCounter != native[counterAddress] ||
                _runtimeState.ReadField("wDisabledObjects") != native[0xcc8a] ||
                _runtimeState.ReadField("wMenuDisabled") != native[0xcc02] ||
                _runtimeState.ReadWramByte(record.ActiveAddress) != native[0xcfd0] ||
                ((IGameplayForegroundPhases)this).StatusBarUpdatesAllowed || _inventoryMenu.IsActive,
                $"Fairy global update{updates}: native state/counter/palette/control/dispatch differs (state={fairies.Stage}/{native[stateAddress]}, counter={fairies.CompletionCounter}/{native[counterAddress]}, mode={_transitions.NativePaletteThreadMode:x2}/{native[paletteAddress]:x2}).");
            // A black probe measures the actual uploaded white offset. Stopping
            // clears the thread register without uploading a new palette.
            AssertFairyFadeAlpha((palette.DisplayedObjectColor(7, 1) & 31) / 31f,
                $"native global update{updates}");
            foreach (int flag in new[] { 0x0e, 0x2b })
                FailIf(_saveData.HasGlobalFlag(flag) != ((native[_runtimeState.FieldAddress("wGlobalFlags") + flag / 8] & (1 << (flag & 7))) != 0),
                    $"Fairy global update{updates}: global flag${flag:x2} differs.");
            if (native.TextGeneration == 2)
                FailIf(!_dialogue.IsOpen || _dialogue.Position.Y != 96,
                    "Native TX_110b must retain the bottom position before and during the final flag commit.");
        }
        while (active && updates < 400)
            StepGameplayUpdates(batched ? 3 : 1, Vector2.Zero,
                held: ["inventory"], pressed: ["inventory"], batched:batched, afterUpdate:Compare);
        FailIf(active || updates != 303 || native.TextGeneration != 2 || fairies.BlocksGameplay ||
            _sound.PlayRequestsFor(record.MysterySound) - initialMysterySounds != 3 ||
            native.Sounds.Count(sound => sound == record.MysterySound) != 3,
            $"Fairy completion must finish on native update303 with two text requests and three mystery cues (updates={updates}).");
    }

    private void AssertFairyFadeAlpha(float expected, string boundary)
    {
        FailIf(
            !Mathf.IsEqualApprox(_warpFade.Color.A, expected),
            $"Fairies' Woods {boundary} expected white alpha {expected}, " +
            $"got {_warpFade.Color.A}.");
    }

    private void AdvanceFairiesWoodsFrames(int frames)
    {
        for (int frame = 0; frame < frames; frame++)
            AdvanceFairiesWoodsFrame();
    }

    private void AdvanceFairiesWoodsFrame()
    {
        StepGameplayUpdates(1, Vector2.Zero);
    }

    private static void ClearFairiesWoodsTransient(
        FairiesWoodsEventRecord record,
        OracleRuntimeState runtime)
    {
        for (int offset = 0; offset < 0x10; offset++)
            runtime.SetWramByte(record.ActiveAddress + offset, 0);
    }
}
