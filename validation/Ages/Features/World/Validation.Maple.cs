
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateMapleEvents()
    {
        ValidateMapleImportedContract();
        ValidateMapleInventoryDrops();
        ValidateMapleSpawnThresholds();
        ValidateMapleSpecialObjectAdmission();
        ValidateMapleTargetOrdering();
        ValidateMapleRewards();
        ValidateMaplePastEncounter();
        ValidateMapleScreenTransitionLock();
        ValidateMapleBookExchange();
        ValidateMapleLinkedVehicle();

        GD.Print(
            "Validated Maple's 110 native bitmap locations, 30/15-kill thresholds, " +
            "76 full positioned OAM frames with per-tile partial graphics retention, " +
            "one-cell alternating terrain shadow, shared RNG entrance, unsigned 8.8 " +
            "path wrapping, opaque on-screen broom/vacuum/UFO entry, exact Link-drop bugs, " +
            "ordered target selection, collision/recoil/shake/menu and screen-edge lock, " +
            "scattered-item race and scoring, past greeting/global flag, " +
            "outcome/departure persistence, and Touching Book to Magic Oar exchange.");
    }

    private void ValidateMapleScreenTransitionLock()
    {
        LoadValidationRoom(1, 0x02);
        _runtimeState.SetField("wScrollMode", 1); // Native boundary polling is admitted only in mode $01.
        Func<bool> previousDisabledSource =
            _transitions.ScreenTransitionsDisabledSource;
        _transitions.ScreenTransitionsDisabledSource = static () => true;
        try
        {
            (Vector2 Attempt, Vector2 Expected)[] boundaries =
            [
                (new Vector2(4.5f, 64.0f), new Vector2(6.5f, 64.0f)),
                (new Vector2(155.5f, 64.0f), new Vector2(154.5f, 64.0f)),
                (new Vector2(80.0f, 4.25f), new Vector2(80.0f, 6.25f)),
                (new Vector2(80.0f, 122.75f), new Vector2(80.0f, 121.75f))
            ];
            foreach ((Vector2 attempt, Vector2 expected) in boundaries)
            {
                _player.WarpTo(attempt, recordSafe: false);
                _transitions.CheckRoomExit(_player);
                FailIf(
                    _transitions.IsTransitioning ||
                    _rooms.ActiveGroup != 1 ||
                    _rooms.CurrentRoom.Id != 0x02 ||
                    _player.PrecisePosition != expected,
                    "Maple's wDisableScreenTransitions lock did not retain " +
                    $"Link at the original screen boundary: attempted " +
                    $"{attempt}, got {_player.PrecisePosition} in " +
                    $"{_rooms.ActiveGroup}:{_rooms.CurrentRoom.Id:x2}.");
            }
        }
        finally
        {
            _transitions.ScreenTransitionsDisabledSource =
                previousDisabledSource;
        }
    }

    private void ValidateMapleImportedContract()
    {
        var database = new MapleEventDatabase(AgesGameContent.AssetRoot);
        var admission = MapleAdmissionData.For(AgesGameContent.AssetRoot);
        MaplePathRecord shadow = database.Path(MaplePathKind.Shadow, 0);
        MaplePathRecord movement = database.Path(MaplePathKind.Movement, 7);
        FailIf(
            admission.LocationCount != 110 ||
            database.PathStepCount != 61 ||
            database.ItemCount != 14 ||
            database.Visual.Animations.Length != 32 ||
            !admission.IsEligibleLocation(0, 0x01, 0) ||
            !admission.IsEligibleLocation(0, 0x01, 0x0c) ||
            !admission.IsEligibleLocation(0, 0x01, 0x0d) ||
            !admission.IsEligibleLocation(1, 0x02, 0) ||
            admission.IsEligibleLocation(0, 0x03, 0) ||
            admission.IsEligibleLocation(2, 0x02, 0) ||
            shadow is not
                { StartY: 0x10, StartX: 0xb8, TurnDelay: 2 } ||
            shadow.Steps.Count != 5 ||
            movement is not
                { StartY: 0xf0, StartX: 0x70, TurnDelay: 2 } ||
            movement.Steps.Count != 7 ||
            database.Item(0) is not
                { Value: 0x3c, Treasure: TreasureId.HeartPiece } ||
            database.Item(10) is not
                { Value: 4, Treasure: TreasureId.Bombs } ||
            database.Item(13) is not
                { Value: 1, Treasure: TreasureId.Rupees } ||
            string.IsNullOrWhiteSpace(database.Text(0x0700)) ||
            string.IsNullOrWhiteSpace(database.Text(0x0713)) ||
            !database.Text(0x070e).Contains(
                "\\col(0x84)\\item(0x09)",
                StringComparison.Ordinal) ||
            database.Constant("normal-kill-threshold") != 30 ||
            database.Constant("ring-kill-threshold") != 15 ||
            database.Constant("birth-y") != 0x18 || database.Constant("birth-x") != 0xb8 ||
            database.Constant("initial-y") != 0x10 || database.Constant("initial-x") != 0xb8,
            "The imported Maple location/path/visual/item/text contract changed.");
        _dialogue.ShowMessage(database.Text(0x070e), 0x48);
        FailIf(
            _dialogue.CurrentMessage.Contains(
                "\\item", StringComparison.OrdinalIgnoreCase) ||
            !_dialogue.GlyphUsesTradeItemFontForValidation(0, 0, 11) ||
            _dialogue.GlyphCodeForValidation(0, 0, 11) != 0x09 ||
            _dialogue.TradeItemNonBackgroundPixelCountForValidation(0x09) != 56,
            "TX_070e did not render Touching Book glyph $09 from " +
            "gfx_font_tradeitems as one textbox character.");
        _dialogue.Close();
        ValidateMaplePositionedAnimations(database);
    }

    private void ValidateMaplePositionedAnimations(
        MapleEventDatabase database)
    {
        MapleVisualRecord visual = database.Visual;
        var animation = new EnemyAnimationPlayer(
            this, visual.Animations.Length);
        animation.Load(
            OracleGraphicsCache.LoadImage(
                AgesGameContent.AssetRoot + $"gfx/{visual.Sprite}.png"),
            visual.Animations,
            visual.TileBase,
            visual.Palette,
            positionedOam: true);

        int frameCount = 0;
        int formerlyClippedFrames = 0;
        for (int animationIndex = 0;
             animationIndex < visual.Animations.Length;
             animationIndex++)
        {
            AnimationDefinition definition =
                OracleGraphicsCache.GetAnimationDefinition(
                    visual.Animations[animationIndex]);
            animation.SetAnimation(animationIndex);
            for (int frameIndex = 0;
                 frameIndex < definition.Frames.Length;
                 frameIndex++)
            {
                AnimationFrameDefinition frame =
                    definition.Frames[frameIndex];
                (Vector2 expectedOffset, Vector2I expectedSize,
                    bool hasCells, bool outsideFixed) =
                    MapleOamBounds(frame.EncodedOam);
                var actualSize = new Vector2I(
                    animation.CurrentTexture.GetWidth(),
                    animation.CurrentTexture.GetHeight());
                FailIf(
                    animation.FrameIndex != frameIndex ||
                    animation.CurrentOffset != expectedOffset ||
                    actualSize != expectedSize ||
                    hasCells &&
                    !TextureHasOpaquePixel(animation.CurrentTexture),
                    $"Maple animation ${animationIndex:x2} frame {frameIndex} " +
                    $"lost its signed OAM bounds: expected {expectedOffset}/" +
                    $"{expectedSize}, got {animation.CurrentOffset}/{actualSize}.");
                frameCount++;
                if (outsideFixed)
                    formerlyClippedFrames++;
                animation.Advance(0xff);
            }
        }

        FailIf(
            frameCount != 76 || formerlyClippedFrames != 48,
            $"Expected 76 Maple frames with 48 outside fixed 32x32 bounds, " +
            $"got {frameCount} and {formerlyClippedFrames}.");

        AnimationDefinition retainedGraphics =
            OracleGraphicsCache.GetAnimationDefinition(
                visual.Animations[0x0b]);
        const string ExpectedRetainedOam =
            "249,0,62,2;249,8,66,2;9,248,68,2;9,0,70,2;9,8,72,2";
        FailIf(
            retainedGraphics.Frames.Length != 2 ||
            retainedGraphics.Frames[1].EncodedOam != ExpectedRetainedOam,
            "Maple animation $0b frame 1 no longer retains the spr_maple " +
            "$03e0 graphics slice loaded by frame 0.");

        ValidateMapleResolvedTiles(
            visual, 0x05, 1, [56, 82, 78, 80],
            "right-facing broom flight");
        ValidateMapleResolvedTiles(
            visual, 0x07, 1, [56, 82, 78, 80],
            "left-facing broom flight");
        ValidateMapleResolvedTiles(
            visual, 0x10, 3, [152, 154, 136, 138, 140],
            "right-facing post-crash ground pose");
        ValidateMapleResolvedTiles(
            visual, 0x11, 3, [152, 154, 136, 138, 140],
            "left-facing post-crash ground pose");
    }

    private static void ValidateMapleResolvedTiles(
        MapleVisualRecord visual,
        int animationIndex,
        int frameIndex,
        int[] expected,
        string pose)
    {
        AnimationDefinition animation =
            OracleGraphicsCache.GetAnimationDefinition(
                visual.Animations[animationIndex]);
        int[] actual = animation.Frames[frameIndex].EncodedOam
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(block => int.Parse(block.Split(',')[2]))
            .ToArray();
        FailIf(
            !actual.SequenceEqual(expected),
            $"Maple's {pose} lost its partial graphics-load retention: " +
            $"expected [{string.Join(',', expected)}], " +
            $"got [{string.Join(',', actual)}].");
    }

    private static (
        Vector2 Offset,
        Vector2I Size,
        bool HasCells,
        bool OutsideFixed)
        MapleOamBounds(string encodedOam)
    {
        int loopMarker = encodedOam.LastIndexOf('~');
        if (loopMarker >= 0)
            encodedOam = encodedOam[..loopMarker];
        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;
        foreach (string block in encodedOam.Split(
            ';', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] fields = block.Split(',');
            if (fields.Length != 4)
                continue;
            int y = SignedByte(int.Parse(fields[0]));
            int x = SignedByte(int.Parse(fields[1])) + 8;
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x + 8);
            maxY = Math.Max(maxY, y + 16);
        }

        if (minX == int.MaxValue)
        {
            return (
                new Vector2(-16, -16),
                new Vector2I(32, 32),
                HasCells: false,
                OutsideFixed: false);
        }
        return (
            new Vector2(minX - 16, minY - 16),
            new Vector2I(maxX - minX, maxY - minY),
            HasCells: true,
            OutsideFixed:
                minX < 0 || minY < 0 || maxX > 32 || maxY > 32);
    }

    private static int SignedByte(int value) =>
        value >= 0x80 ? value - 0x100 : value;

    private static void ValidateMapleInventoryDrops()
    {
        var treasures = new TreasureDatabase(AgesGameContent.AssetRoot);
        OracleSaveData save = OracleSaveData.CreateStandardGame(OracleGame.Ages);
        var inventory = new InventoryState(treasures, save);
        inventory.GiveTreasure(TreasureId.Sword, 1);
        inventory.GiveTreasure(TreasureId.SeedSatchel, 1);
        FailIf(
            !inventory.TryTakeMapleDrop(5, out int emberDrop) ||
            emberDrop != 5 || inventory.EmberSeeds != 0x15,
            "Maple's erroneous sword check did not remove five packed-BCD Ember Seeds.");

        inventory.GiveTreasure(TreasureId.SwitchHook, 1);
        inventory.GiveTreasure(TreasureId.Bombs, 0x10);
        FailIf(
            !inventory.TryTakeMapleDrop(10, out int bombDrop) ||
            bombDrop != 10 || inventory.Bombs != 0x06,
            "Maple's Switch Hook check did not remove four packed-BCD Bombs.");

        int healthBefore = inventory.HealthQuarters;
        FailIf(
            !inventory.TryTakeMapleDrop(12, out int heartDrop) ||
            heartDrop != 11 || inventory.HealthQuarters != healthBefore - 4,
            "Maple's item-$0c branch did not take one heart and scatter item $0b.");

        inventory.GiveTreasure(TreasureId.Rupees, 3);
        int rupeesBefore = inventory.Rupees;
        FailIf(
            !inventory.TryTakeMapleDrop(13, out int rupeeDrop) ||
            rupeeDrop != 12 || inventory.Rupees != rupeesBefore - 1,
            "Maple's one-rupee branch did not preserve the original five-rupee output bug.");

        OracleSaveData unavailableSave = OracleSaveData.CreateStandardGame(OracleGame.Ages);
        var unavailable = new InventoryState(treasures, unavailableSave);
        unavailable.GiveTreasure(TreasureId.SeedSatchel, 1);
        FailIf(
            unavailable.TryTakeMapleDrop(7, out _),
            "Maple dropped Pegasus Seeds without the original mistaken treasure-$07 check.");
    }

    private void ValidateMapleSpawnThresholds()
    {
        using var harness = new MapleValidationHarness(
            this, group: 0, room: 0x01);
        harness.Save.SetMapleKillCounter(29);
        harness.Load();
        FailIf(
            harness.Manager.Entities<MapleEncounter>().Count != 0 ||
            harness.Save.MapleKillCounter != 29 ||
            harness.Manager.RandomCalls != 256,
            "Maple spawned before 30 kills or room parsing lost its 256 RNG calls.");

        harness.Save.SetMapleKillCounter(30);
        harness.Load();
        bool rejectedCapture = false;
        try { harness.Manager.CaptureDebugState(); }
        catch (NotSupportedException error) when (error.Message.Contains("$0e")) { rejectedCapture = true; }
        FailIf(harness.Manager.CanCaptureDebugState || !rejectedCapture,
            "Maple's active source state and path cannot be silently lost in a debug room reconstruction.");
        FailIf(
            harness.Manager.Entities<MapleEncounter>().Count != 1 ||
            harness.Save.MapleKillCounter != 0 ||
            harness.Manager.RuntimeState.ReadWramByte(0xd100) != 1 ||
            harness.Manager.RuntimeState.ReadWramByte(0xd101) != 0x0e ||
            harness.Manager.RuntimeState.ReadWramByte(0xd10b) != 0x18 ||
            harness.Manager.RuntimeState.ReadWramByte(0xd10d) != 0xb8 ||
            harness.Manager.RuntimeState.ReadField("wIsMaplePresent") != 1 ||
            harness.Manager.RuntimeState.ReadField("wcc85") != 1,
            "An eligible present room did not spawn Maple and reset her kill counter.");

        harness.Inventory.GiveTreasure(
            harness.Treasures.GetObject("TREASURE_OBJECT_RING_BOX_00"));
        harness.Inventory.GrantAppraisedRingForDebug((int)RingId.Maples);
        FailIf(
            !harness.Inventory.SetRingBoxSlotFromList(
                0, (int)RingId.Maples) ||
            !harness.Inventory.EquipRingAt(0),
            "Could not equip Maple's Ring for encounter validation.");

        harness.Save.SetMapleKillCounter(14);
        harness.Load();
        FailIf(
            harness.Manager.Entities<MapleEncounter>().Count != 0,
            "Maple's Ring spawned Maple before its 15-kill boundary.");
        harness.Save.SetMapleKillCounter(15);
        harness.Load();
        FailIf(
            harness.Manager.Entities<MapleEncounter>().Count != 1 ||
            harness.Save.MapleKillCounter != 0,
            "Maple's Ring did not lower the encounter boundary to 15 kills.");
    }

    private void ValidateMapleSpecialObjectAdmission()
    {
        // roomInitialization.s tests enabled before resetting kills or writing
        // the header. Every occupied native ID blocks, including non-animals.
        foreach (int id in Enumerable.Range(0x0a, 0x0a))
        {
            using var harness = new MapleValidationHarness(this, 0, 0x01);
            var memory = harness.Manager.RuntimeState;
            harness.Save.WriteField("wCompanionStates", 0x80);
            harness.Save.WriteField("wDimitriState", 0x80);
            harness.Save.WriteField("wMooshState", 0x80);
            CompanionRuntimeState.Begin(memory, id, new(72.75f, 64.25f), 2);
            memory.SetWramByte(0xd104, id == 0x13 ? (byte)1 : (byte)5);
            memory.SetWramByte(0xd13d, 0x96);
            memory.SetWramByte(0xd13e, 0x69);
            byte[] before = Enumerable.Range(0xd100, 0x40).Select(memory.ReadWramByte).ToArray();
            harness.Save.SetMapleKillCounter(30);
            memory.SetField("wIsMaplePresent", 1);
            harness.Load();
            FailIf(harness.Manager.Entities<MapleEncounter>().Count != 0 ||
                harness.Save.MapleKillCounter != 30 || memory.ReadField("wIsMaplePresent") != 0 ||
                memory.ReadField("wcc85") != 0 ||
                !Enumerable.Range(0xd100, 0x40).Select(memory.ReadWramByte).SequenceEqual(before),
                $"checkAndSpawnMaple must preserve occupied SPECIALOBJECT ${id:x2} and kills while clearing only its presence signal.");
        }
        ReinitializeGameplayForValidation();
        _saveData.SetMapleKillCounter(30);
        CompanionRuntimeState.Remember(_runtimeState, 0x0b, 0, 0x01, new(72, 64));
        LoadValidationRoom(0, 0x01);
        FailIf(_entities.EntityAdapters<RickyScriptedCompanionRoomEntity>().Count() != 1 ||
            _entities.Entities<MapleEncounter>().Count != 0 || _saveData.MapleKillCounter != 30 ||
            _runtimeState.ReadWramByte(0xd100) != 1 || _runtimeState.ReadWramByte(0xd101) != 0x0b ||
            _runtimeState.ReadField("wIsMaplePresent") != 0,
            "loadRememberedCompanion must occupy $d100 before checkAndSpawnMaple, preserving the 30-kill threshold.");
        using (var harness = new MapleValidationHarness(this, 0, 0x01))
        {
            var memory = harness.Manager.RuntimeState;
            harness.Save.SetMapleKillCounter(30);
            memory.SetWramByte(0xd10a, 0x40);
            memory.SetWramByte(0xd10c, 0xc0);
            memory.SetWramByte(0xd10e, 0x20);
            harness.Load();
            var maple = harness.Manager.Entities<MapleEncounter>().Single();
            FailIf(maple.BookTargetPosition != new Vector2(184.75f, 24.25f),
                "checkAndSpawnMaple must retain the native XY fractions at birth Y=$18.");
            harness.Step();
            FailIf(maple.BookTargetPosition != new Vector2(184.75f, 16.25f) ||
                memory.ReadWramByte(0xd104) != 1 || memory.ReadWramByte(0xd10a) != 0x40 ||
                memory.ReadWramByte(0xd10b) != 0x10 || memory.ReadWramByte(0xd10c) != 0xc0 ||
                memory.ReadWramByte(0xd10d) != 0xb8 || memory.ReadWramByte(0xd10e) != 0x20 ||
                memory.ReadWramByte(0xd10f) != 0x88 || memory.ReadField("wcc85") != 0,
                "mapleState0 writes high XYZ only, publishes state1 and clears wcc85 on its first native visit.");
        }
        using (var harness = new MapleValidationHarness(this, 0, 0x01))
        {
            var memory = harness.Manager.RuntimeState;
            harness.Save.SetMapleKillCounter(30);
            memory.SetField("wcc85", 0x80);
            memory.SetField("wIsMaplePresent", 1);
            // Entry after reload's range clear, as for a retained scroll flag.
            var native = new SomariaRom(harness.Save, harness.Manager.Random.CaptureState(),
                harness.Rooms.CurrentRoom, 2, 80, 80);
            native[0xcc05] = memory.ReadField("wcc05");
            native[0xcc85] = 0x80; native[0xcdda] = 1;
            native[0xcd02] = memory.ReadField("wScreenTransitionDirection");
            native.InitializeRoomInteractions();
            harness.Manager.LoadRoom(0, harness.Rooms.CurrentRoom);
            FailIf(harness.Manager.Entities<MapleEncounter>().Count != 0 ||
                harness.Save.MapleKillCounter != 30 || memory.ReadField("wcc85") != native[0xcc85] || native[0xcc85] != 0 ||
                memory.ReadField("wIsMaplePresent") != 0 || CompanionRuntimeState.AnyActive(memory),
                "initializeRoom must retain kills on Maple's early return, then consume the one-shot $80 pointer gate during ordinary parsing without allocating Maple.");
        }
    }

    private void ValidateMapleTargetOrdering()
    {
        using var harness = new MapleValidationHarness(
            this, group: 0, room: 0x01);
        var database = new MapleEventDatabase(AgesGameContent.AssetRoot);
        var encounter = new MapleEncounterState(harness.Manager.RuntimeState);
        var random = new OracleRandom();
        MapleDroppedItem first = CreateMapleTarget(
            database.Item(5), encounter, harness, random, 0,
            new Vector2(40, 40));
        MapleDroppedItem later = CreateMapleTarget(
            database.Item(5), encounter, harness, random, 1,
            new Vector2(60, 40));
        FailIf(
            !ReferenceEquals(encounter.ChooseTarget(40, 50), later),
            "Maple no longer selects the later part slot on an equal-distance normal-item tie.");

        MapleDroppedItem unique = CreateMapleTarget(
            database.Item(1), encounter, harness, random, 2,
            new Vector2(120, 100));
        FailIf(
            !ReferenceEquals(encounter.ChooseTarget(40, 50), unique),
            "Maple no longer prioritizes unique item IDs $00-$04 before normal loot.");

        first.Free();
        later.Free();
        unique.Free();
    }

    private static MapleDroppedItem CreateMapleTarget(
        MapleItemRecord record,
        MapleEncounterState encounter,
        MapleValidationHarness harness,
        OracleRandom random,
        int slot,
        Vector2 position)
    {
        var item = new MapleDroppedItem();
        item.Initialize(
            record, encounter, harness.Rooms.CurrentRoom, random,
            slot, position, 0, static (_, _) => true);
        encounter.Register(item);
        item.UpdateFrame(harness.Player);
        return item;
    }

    private void ValidateMaplePastEncounter()
    {
        using var harness = new MapleValidationHarness(
            this, group: 1, room: 0x02);
        // Isolate Maple's RNG and horizontal shake from $dc:$05's tremors.
        harness.Save.SetGlobalFlag(AgesGlobalFlag.TuniNutPlaced);
        harness.Inventory.GiveTreasure(
            TreasureId.Sword, 1);
        harness.Inventory.GiveTreasure(
            TreasureId.SeedSatchel, 1);
        harness.Save.SetMapleKillCounter(30);
        harness.Load();
        MapleEncounter maple =
            harness.Manager.Entities<MapleEncounter>().Single();

        harness.Step();
        FailIf(
            maple.Stage != MapleEncounterStage.EntryDelay ||
            maple.Vehicle != 0 || maple.Variation != 0 ||
            maple.AnimationIndex != 0x19 ||
            harness.Manager.RuntimeState.ReadWramByte(0xd104) != 1 ||
            harness.Manager.RuntimeState.ReadWramByte(0xd10b) != 0x10 ||
            harness.Manager.RuntimeState.ReadWramByte(0xd10d) != 0xb8 ||
            harness.Manager.RuntimeState.ReadWramByte(0xd10f) != 0x88 ||
            maple.ShadowTexture.GetWidth() != 8 ||
            maple.ShadowTexture.GetHeight() != 16 ||
            maple.ShadowOffset != new Vector2(-4, 3) ||
            maple.DropPattern is < 0 or > 1 ||
            harness.Manager.RandomCalls != 257,
            "Maple's broom initialization did not retain the exact one-cell " +
            "terrain shadow or consume one post-parse RNG value.");
        bool firstShadowPhase = maple.ShadowDrawn;
        harness.Step();
        FailIf(
            maple.ShadowDrawn == firstShadowPhase,
            "Maple's automatic terrain shadow did not flicker on alternating updates.");
        harness.Step();
        harness.Step();
        FailIf(
            maple.Stage != MapleEncounterStage.Flying ||
            !harness.Sounds.Contains(SoundId.MusMapleTheme),
            "Maple's three-update entry delay did not begin her theme and shadow flight.");

        AdvanceMapleToCollision(harness, maple);
        List<MapleDroppedItem> droppedItems =
            harness.Manager.Entities<MapleDroppedItem>();
        int dropped = droppedItems.Count;
        // mapleSpawnItemDrop_variant returns nonzero airborne Z on successful
        // unique allocations, so those items do not consume the five attempts.
        int mapleDrops = droppedItems.TakeWhile(item => item.ZFixed < 0).Count();
        int uniqueMapleDrops = droppedItems.Take(mapleDrops).Count(item => item.ItemIndex < 5);
        FailIf(
            maple.Stage != MapleEncounterStage.Recoiling ||
            mapleDrops != 5 + uniqueMapleDrops || dropped - mapleDrops is < 0 or > 5 ||
            droppedItems.Skip(mapleDrops).Any(item => item.ZFixed != 0) ||
            !maple.ScreenTransitionsDisabled ||
            !maple.MenusDisabled ||
            harness.Manager.HorizontalScreenShakeCounter != 14 ||
            harness.Player.KnockbackFrames != 23 ||
            !harness.Sounds.Contains(SoundId.SndScentSeed),
            "Maple's collision did not scatter loot, knock both actors back, lock the room, " +
            $"and begin the 15-update horizontal shake (stage={maple.Stage}, " +
            $"drops={dropped}, transitions={maple.ScreenTransitionsDisabled}, " +
            $"menus={maple.MenusDisabled}, shake={harness.Manager.HorizontalScreenShakeCounter}, " +
            $"knockback={harness.Player.KnockbackFrames}, sounds={string.Join(',', harness.Sounds)}).");

        AdvanceMapleToCompletion(harness, maple);
        int expectedOutcome = maple.LinkScore == maple.MapleScore
            ? 0x0708
            : maple.LinkScore < maple.MapleScore ? 0x0706 : 0x0707;
        FailIf(
            !maple.Finished ||
            (harness.Save.MapleState & 0x0f) != 1 ||
            !harness.Save.HasGlobalFlag(
                AgesGlobalFlag.MapleMetInPast) ||
            !harness.Dialogues.Contains(0x0712) ||
            !harness.Dialogues.Contains(expectedOutcome) ||
            harness.Manager.ScreenTransitionsDisabled ||
            harness.Manager.PlayerMenusDisabled ||
            !harness.RoomMusic.Contains((1, 0x02)),
            "Maple's past greeting, race result, departure, or persistent meeting state failed.");
    }

    private void ValidateMapleRewards()
    {
        using var harness = new MapleValidationHarness(
            this, group: 0, room: 0x01);
        // Exercise reward RNG in isolation; room $0:$01 also contains three
        // Arrow Darknuts whose native route changes consume the shared RNG.
        harness.Load(); // Retain the source room-parse RNG before isolating the parts.
        harness.Manager.LoadCutsceneRoom(0, harness.Rooms.CurrentRoom, includeTimePortals: false);
        var database = new MapleEventDatabase(AgesGameContent.AssetRoot);

        int swordRupeesBefore = harness.Inventory.Rupees;
        MapleDroppedItem swordDrop = SpawnGroundedMapleReward(
            harness, database.Item(13));
        harness.Player.WarpTo(
            swordDrop.Position - new Vector2(16, -2),
            recordSafe: false);
        harness.Player.StartSwordAttackForValidation(Vector2.Up);
        Rect2 swordHitbox = harness.Player.GetSwordHitbox();
        harness.Manager.ApplySwordHit(
            swordHitbox, harness.Player.Position);
        FailIf(swordDrop.Finished, "The sword granted Maple's scattered drop before its next part update.");
        harness.Step();
        FailIf(
            !swordDrop.Finished ||
            harness.Inventory.Rupees != swordRupeesBefore + 1,
            "Sword collision did not collect Maple's grounded scattered item.");
        harness.Player.WarpTo(new Vector2(16, 112), recordSafe: false);

        int potionSoundCount = harness.Sounds.Count(
            sound => sound == SoundId.SndGetSeed);
        CollectMapleReward(harness, database.Item(4));
        FailIf(
            !harness.Inventory.HasTreasure(
                TreasureId.Potion) ||
            harness.Sounds.Count(
                sound => sound == SoundId.SndGetSeed) !=
                    potionSoundCount + 1,
            "Maple's Potion reward did not use its explicit SND_GETSEED override.");

        harness.Inventory.GiveTreasure(
            harness.Treasures.GetObject("TREASURE_OBJECT_RING_BOX_00"));
        harness.Inventory.GrantAppraisedRingForDebug((int)RingId.GoldJoy);
        FailIf(
            !harness.Inventory.SetRingBoxSlotFromList(
                0, (int)RingId.GoldJoy) ||
            !harness.Inventory.EquipRingAt(0),
            "Could not equip the Gold Joy Ring for Maple reward validation.");
        CollectMapleReward(harness, database.Item(5));
        FailIf(
            harness.Inventory.EmberSeeds != 0x10,
            "The Gold Joy Ring did not double Maple's five-Ember-Seed reward.");

        int randomBeforeRing = harness.Manager.RandomCalls;
        CollectMapleReward(harness, database.Item(2));
        FailIf(
            harness.Inventory.UnappraisedRingCount != 1 ||
            harness.Manager.RandomCalls != randomBeforeRing + 3,
            "Maple's ring reward did not consume two scatter RNG values and " +
            "one shared RNG value for its imported tier.");

        MapleDroppedItem heart = SpawnGroundedMapleReward(
            harness, database.Item(0));
        harness.Player.WarpTo(heart.Position, recordSafe: false);
        for (int wait = 0; !harness.Dialogue.IsOpen && wait < 6; wait++)
            harness.Step(closeDialogue: false);
        harness.Step(closeDialogue: false); // INTERAC$60 state3 queues Link's held-item state on its next visit.
        harness.AgesPlayerWorld.NativeTextActive = harness.Dialogue.IsOpen;
        harness.Player.AdvanceApplicationUpdate();
        harness.Player.AdvanceApplicationUpdate();
        FailIf(
            !heart.Finished ||
            (harness.Save.MapleState & 0x80) == 0 ||
            harness.Inventory.HeartPieces != 1 ||
            !harness.Player.IsHoldingItemTwoHands ||
            !harness.Dialogue.IsOpen ||
            harness.Save.HasRoomFlag(
                0, 0x01, OracleSaveData.RoomFlagItem),
            $"Maple Heart Piece: finished={heart.Finished}, state=${harness.Save.MapleState:x2}, hearts={harness.Inventory.HeartPieces}, held={harness.Player.IsHoldingItemTwoHands}, dialogue={harness.Dialogue.IsOpen}, flags=${harness.Save.GetRoomFlags(0, 1):x2}.");
        harness.Dialogue.Close();
        harness.AgesPlayerWorld.NativeTextActive = false;
        harness.Player.AdvanceApplicationUpdate();
        harness.Interactions.Update(1.0 / 60.0, harness.Player);
        FailIf(
            harness.Player.IsHoldingItemTwoHands,
            "Closing Maple's Heart Piece text did not release Link's held-item pose.");
    }

    private static void CollectMapleReward(
        MapleValidationHarness harness,
        MapleItemRecord record)
    {
        MapleDroppedItem item = SpawnGroundedMapleReward(harness, record);
        harness.Player.WarpTo(item.Position, recordSafe: false);
        harness.Step();
        harness.Step(); // Post-object health0 is consumed on the following PART visit.
        FailIf(
            !item.Finished,
            $"Maple reward item ${record.Index:x2} was not collected on grounded contact.");
    }

    private static MapleDroppedItem SpawnGroundedMapleReward(
        MapleValidationHarness harness,
        MapleItemRecord record)
    {
        Vector2[] origins =
        [
            new(80, 64), new(40, 48), new(120, 48),
            new(40, 96), new(120, 96), new(80, 96)
        ];
        foreach (Vector2 origin in origins)
        {
            var encounter = new MapleEncounterState(harness.Manager.RuntimeState);
            MapleDroppedItem item =
                harness.Manager.Spawn<MapleDroppedItem>(
                    new MapleDroppedItemSpawn(
                        record,
                        encounter,
                        0,
                        origin,
                        0));
            harness.Player.WarpTo(
                new Vector2(16, 112), recordSafe: false);
            for (int update = 0;
                 update < 500 &&
                 !item.Finished &&
                 item.State != MapleDroppedItemState.Grounded;
                 update++)
            {
                harness.Step();
            }
            if (item.State == MapleDroppedItemState.Grounded)
                return item;
        }
        throw new InvalidOperationException(
            $"Maple reward item ${record.Index:x2} entered hazards from all six test origins.");
    }

    private void ValidateMapleBookExchange()
    {
        using var harness = new MapleValidationHarness(
            this, group: 0, room: 0x01);
        harness.Inventory.GiveTreasure(
            TreasureId.TradeItem, 0x08);
        harness.Save.SetMapleState(0x08);
        harness.Save.SetMapleKillCounter(30);
        harness.Load();
        MapleEncounter maple =
            harness.Manager.Entities<MapleEncounter>().Single();
        harness.Step();
        FailIf(
            maple.Vehicle != 1 || maple.Variation != 1,
            "A ninth unlinked Maple encounter did not select the vacuum.");

        AdvanceMapleToCollision(harness, maple);
        FailIf(
            harness.Manager.Entities<MapleDroppedItem>().Count != 0 ||
            (harness.Save.MapleState & 0x10) == 0,
            "The Touching Book collision scattered ordinary loot or lost its temporary state bit.");

        AdvanceMapleToCompletion(harness, maple);
        int[] exchangeTexts = [0x070d, 0x070e, 0x070f, 0x0710, 0x0711];
        FailIf(
            !maple.Finished ||
            harness.Inventory.TradeItem != 0x09 ||
            (harness.Save.MapleState & 0x3f) != 0x29 ||
            exchangeTexts.Any(text => !harness.Dialogues.Contains(text)) ||
            harness.Player.IsHoldingItemTwoHands ||
            !harness.RoomMusic.Contains((0, 0x01)),
            "The Touching Book exchange did not grant and present the Magic Oar, " +
            "set completion, clear the temporary bit, and depart.");
    }

    private void ValidateMapleLinkedVehicle()
    {
        using var harness = new MapleValidationHarness(
            this, group: 0, room: 0x01);
        harness.Save.SetLinkedGame(true);
        harness.Save.SetMapleState(0x08);
        harness.Save.SetMapleKillCounter(30);
        harness.Load();
        MapleEncounter maple =
            harness.Manager.Entities<MapleEncounter>().Single();
        harness.Step();
        FailIf(
            maple.Vehicle != 2 || maple.Variation != 1,
            "A ninth linked-game Maple encounter did not select the UFO.");

        harness.Save.SetMapleState(0x0f);
        harness.Save.SetMapleKillCounter(30);
        harness.Load();
        maple = harness.Manager.Entities<MapleEncounter>().Single();
        harness.Step();
        FailIf(
            maple.Vehicle != 2 || maple.Variation != 2,
            "Maple's capped fifteenth meeting did not unlock the full UFO path variation.");
    }

    private static void AdvanceMapleToCollision(
        MapleValidationHarness harness,
        MapleEncounter maple)
    {
        bool visibleMainFlight = false;
        for (int update = 0; update < 1000; update++)
        {
            if (maple.Stage == MapleEncounterStage.Flying &&
                maple.MainFlight &&
                OracleObjectMath.IsInsideOriginalScreenBoundary(
                    maple.Position))
            {
                visibleMainFlight = true;
                FailIf(
                    !TextureHasOpaquePixel(maple.CurrentTexture),
                    $"Maple's main-flight animation ${maple.AnimationIndex:x2} " +
                    "was fully transparent after the shadow path.");
                if (maple.Position.X is >= 24 and <= 136 &&
                    maple.Position.Y is >= 32 and <= 112)
                {
                    harness.Player.WarpTo(
                        maple.Position, recordSafe: false);
                }
            }
            harness.Step();
            if (maple.Stage == MapleEncounterStage.Recoiling)
            {
                if (visibleMainFlight)
                    return;
                throw new InvalidOperationException(
                    "Maple collided before her main-flight sprite crossed the viewport.");
            }
        }
        throw new InvalidOperationException(
            $"Maple did not visibly enter and collide after her shadow path within " +
            $"1000 updates (position={maple.Position}, animation=${maple.AnimationIndex:x2}).");
    }

    private static bool TextureHasOpaquePixel(Texture2D texture)
    {
        Image image = texture.GetImage();
        for (int y = 0; y < image.GetHeight(); y++)
        for (int x = 0; x < image.GetWidth(); x++)
        {
            if (image.GetPixel(x, y).A > 0.1f)
                return true;
        }
        return false;
    }

    private static void AdvanceMapleToCompletion(
        MapleValidationHarness harness,
        MapleEncounter maple)
    {
        for (int update = 0; update < 6000 && !maple.Finished; update++)
            harness.Step();
        FailIf(
            !maple.Finished,
            $"Maple did not finish from stage {maple.Stage}, substate {maple.Substate}, " +
            $"position {maple.Position}, angle ${maple.Angle:x2}, target " +
            $"${maple.TargetAngle:x2}, scores {maple.LinkScore}/{maple.MapleScore} " +
            "within 6000 updates.");
    }

    private sealed class MapleValidationHarness : IDisposable
    {
        private readonly ValidationRoot _owner;
        private readonly RoomEntityValidationFixture _entities;

        internal Node Scene { get; }
        internal OracleSaveData Save { get; }
        internal TreasureDatabase Treasures { get; }
        internal InventoryState Inventory { get; }
        internal RoomSession Rooms { get; }
        internal RoomEntityManager Manager { get; }
        internal ValidationRingPlayerWorld AgesPlayerWorld { get; }
        internal Player Player { get; }
        internal DialogueBox Dialogue { get; }
        internal InteractionController Interactions { get; }
        internal List<int> Dialogues { get; } = [];
        internal List<int> Sounds { get; } = [];
        internal List<(int Group, int Room)> RoomMusic { get; } = [];

        internal MapleValidationHarness(
            ValidationRoot owner,
            int group,
            int room)
        {
            _owner = owner;
            Save = OracleSaveData.CreateStandardGame(OracleGame.Ages);
            Treasures = new TreasureDatabase(AgesGameContent.AssetRoot);
            Inventory = new InventoryState(Treasures, Save, () => -1);
            long tick = 0;
            Rooms = AgesRoomContent.CreateSession(
                group, room, () => tick, () => tick = 0, Save, runtimeState: Inventory.RuntimeState);
            _entities = RoomEntityValidationFixture.Attach(
                _owner, $"MapleValidation_{group}_{room:x2}", new()
                {
                    SaveData = Save,
                    RuntimeState = Inventory.RuntimeState,
                    Inventory = Inventory,
                    AnimationTick = () => tick,
                    Treasures = Treasures,
                    Rooms = Rooms
                });
            Scene = _entities.Root;
            AgesPlayerWorld = new ValidationRingPlayerWorld();
            Player = new Player { Name = "MapleValidationPlayer" };
            Player.Initialize(
                AgesPlayerWorld, Inventory, new Vector2(80, 80),
                new OracleRandom());
            Scene.AddChild(Player);
            var interfaceLayer = new Node { Name = "Interface" };
            var roomView = new RoomView { Name = "RoomView" };
            Dialogue = new DialogueBox { AssetRoot = AgesGameContent.AssetRoot, Name = "Dialogue" };
            Dialogue.SetBackgroundPaletteProvider(
                (palette, shade) =>
                    Rooms.CurrentRoom.ResolveBackgroundPaletteColor(
                        palette, shade));
            Scene.AddChild(interfaceLayer);
            Scene.AddChild(roomView);
            Scene.AddChild(Dialogue);
            Manager = _entities.Manager;
            Manager.MapleDialogueRequested +=
                (textId, _, _) => Dialogues.Add(textId);
            Manager.SoundRequested += Sounds.Add;
            Manager.RoomMusicRequested +=
                (musicGroup, musicRoom) =>
                    RoomMusic.Add((musicGroup, musicRoom));
            Interactions = AgesInteractionValidation.Create(
                Rooms, Manager, new SignDatabase(AgesGameContent.AssetRoot), new ChestDatabase(AgesGameContent.AssetRoot),
                Treasures, Dialogue, Scene, roomView,
                static position => position, () => tick, Inventory,
                interfaceLayer, Sounds.Add);
        }

        internal void Load() =>
            Manager.LoadRoom(Rooms.ActiveGroup, Rooms.Load(Rooms.ActiveGroup, Rooms.CurrentRoom.Id));

        internal void Step(bool closeDialogue = true)
        {
            MainThreadClock.Advance(Save, Manager.RuntimeState);
            AgesPlayerWorld.FrameCounter = Manager.FrameCounter;
            Manager.UpdateSpecialObjectsBeforePlayer(Player);
            Manager.UpdateObjects(1.0 / 60.0, Player);
            Manager.ResolvePostObjectCollisions(Player);
            if (closeDialogue && Dialogue.IsOpen)
                Dialogue.Close();
            Interactions.Update(1.0 / 60.0, Player);
            Player._PhysicsProcess(1.0 / 60.0);
        }

        public void Dispose()
        {
            _entities.Dispose();
        }
    }
}
