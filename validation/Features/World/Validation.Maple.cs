using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMapleEvents()
    {
        ValidateMapleImportedContract();
        ValidateMapleInventoryDrops();
        ValidateMapleSpawnThresholds();
        ValidateMapleTargetOrdering();
        ValidateMapleRewards();
        ValidateMaplePastEncounter();
        ValidateMapleScreenTransitionLock();
        ValidateMapleBookExchange();
        ValidateMapleLinkedVehicle();

        GD.Print(
            "Validated Maple's 119 eligible locations, 30/15-kill thresholds, " +
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
                _playerWorld.CheckRoomExit(_player);
                if (_transitions.IsTransitioning ||
                    _rooms.ActiveGroup != 1 ||
                    _rooms.CurrentRoom.Id != 0x02 ||
                    _player.PrecisePosition != expected)
                {
                    throw new InvalidOperationException(
                        "Maple's wDisableScreenTransitions lock did not retain " +
                        $"Link at the original screen boundary: attempted " +
                        $"{attempt}, got {_player.PrecisePosition} in " +
                        $"{_rooms.ActiveGroup}:{_rooms.CurrentRoom.Id:x2}.");
                }
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
        var database = new MapleEventDatabase();
        MaplePathRecord shadow = database.Path(MaplePathKind.Shadow, 0);
        MaplePathRecord movement = database.Path(MaplePathKind.Movement, 7);
        if (database.LocationCount != 119 ||
            database.PathStepCount != 61 ||
            database.ItemCount != 14 ||
            database.Visual.Animations.Length != 32 ||
            !database.IsEligibleLocation(0, 0x01, 0) ||
            !database.IsEligibleLocation(0, 0x01, 1) ||
            !database.IsEligibleLocation(0, 0x01, 2) ||
            !database.IsEligibleLocation(1, 0x02, 0) ||
            database.IsEligibleLocation(0, 0x03, 0) ||
            database.IsEligibleLocation(2, 0x02, 0) ||
            shadow is not
                { StartY: 0x10, StartX: 0xb8, TurnDelay: 2 } ||
            shadow.Steps.Count != 5 ||
            movement is not
                { StartY: 0xf0, StartX: 0x70, TurnDelay: 2 } ||
            movement.Steps.Count != 7 ||
            database.Item(0) is not
                { Value: 0x3c, Treasure: TreasureDatabase.TreasureHeartPiece } ||
            database.Item(10) is not
                { Value: 4, Treasure: TreasureDatabase.TreasureBombs } ||
            database.Item(13) is not
                { Value: 1, Treasure: TreasureDatabase.TreasureRupees } ||
            string.IsNullOrWhiteSpace(database.Text(0x0700)) ||
            string.IsNullOrWhiteSpace(database.Text(0x0713)) ||
            !database.Text(0x070e).Contains(
                "\\col(0x84)\\item(0x09)",
                StringComparison.Ordinal) ||
            database.Constant("normal-kill-threshold") != 30 ||
            database.Constant("ring-kill-threshold") != 15)
        {
            throw new InvalidOperationException(
                "The imported Maple location/path/visual/item/text contract changed.");
        }
        _dialogue.ShowMessage(database.Text(0x070e), 0x48);
        if (_dialogue.CurrentMessage.Contains(
                "\\item", StringComparison.OrdinalIgnoreCase) ||
            !_dialogue.GlyphUsesTradeItemFontForValidation(0, 0, 11) ||
            _dialogue.GlyphCodeForValidation(0, 0, 11) != 0x09 ||
            _dialogue.TradeItemNonBackgroundPixelCountForValidation(0x09) != 56)
        {
            throw new InvalidOperationException(
                "TX_070e did not render Touching Book glyph $09 from " +
                "gfx_font_tradeitems as one textbox character.");
        }
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
                $"res://assets/oracle/gfx/{visual.Sprite}.png"),
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
                if (animation.FrameIndex != frameIndex ||
                    animation.CurrentOffset != expectedOffset ||
                    actualSize != expectedSize ||
                    hasCells &&
                    !TextureHasOpaquePixel(animation.CurrentTexture))
                {
                    throw new InvalidOperationException(
                        $"Maple animation ${animationIndex:x2} frame {frameIndex} " +
                        $"lost its signed OAM bounds: expected {expectedOffset}/" +
                        $"{expectedSize}, got {animation.CurrentOffset}/{actualSize}.");
                }
                frameCount++;
                if (outsideFixed)
                    formerlyClippedFrames++;
                animation.Advance(0xff);
            }
        }

        if (frameCount != 76 || formerlyClippedFrames != 48)
        {
            throw new InvalidOperationException(
                $"Expected 76 Maple frames with 48 outside fixed 32x32 bounds, " +
                $"got {frameCount} and {formerlyClippedFrames}.");
        }

        AnimationDefinition retainedGraphics =
            OracleGraphicsCache.GetAnimationDefinition(
                visual.Animations[0x0b]);
        const string ExpectedRetainedOam =
            "249,0,62,2;249,8,66,2;9,248,68,2;9,0,70,2;9,8,72,2";
        if (retainedGraphics.Frames.Length != 2 ||
            retainedGraphics.Frames[1].EncodedOam != ExpectedRetainedOam)
        {
            throw new InvalidOperationException(
                "Maple animation $0b frame 1 no longer retains the spr_maple " +
                "$03e0 graphics slice loaded by frame 0.");
        }

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
        if (!actual.SequenceEqual(expected))
        {
            throw new InvalidOperationException(
                $"Maple's {pose} lost its partial graphics-load retention: " +
                $"expected [{string.Join(',', expected)}], " +
                $"got [{string.Join(',', actual)}].");
        }
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
        var treasures = new TreasureDatabase();
        OracleSaveData save = OracleSaveData.CreateStandardGame();
        var inventory = new InventoryState(treasures, save);
        inventory.GiveTreasure(TreasureDatabase.TreasureSword, 1);
        inventory.GiveTreasure(TreasureDatabase.TreasureSeedSatchel, 1);
        if (!inventory.TryTakeMapleDrop(5, out int emberDrop) ||
            emberDrop != 5 || inventory.EmberSeeds != 0x15)
        {
            throw new InvalidOperationException(
                "Maple's erroneous sword check did not remove five packed-BCD Ember Seeds.");
        }

        inventory.GiveTreasure(TreasureDatabase.TreasureSwitchHook, 1);
        inventory.GiveTreasure(TreasureDatabase.TreasureBombs, 0x10);
        if (!inventory.TryTakeMapleDrop(10, out int bombDrop) ||
            bombDrop != 10 || inventory.Bombs != 0x06)
        {
            throw new InvalidOperationException(
                "Maple's Switch Hook check did not remove four packed-BCD Bombs.");
        }

        int healthBefore = inventory.HealthQuarters;
        if (!inventory.TryTakeMapleDrop(12, out int heartDrop) ||
            heartDrop != 11 || inventory.HealthQuarters != healthBefore - 4)
        {
            throw new InvalidOperationException(
                "Maple's item-$0c branch did not take one heart and scatter item $0b.");
        }

        inventory.GiveTreasure(TreasureDatabase.TreasureRupees, 3);
        int rupeesBefore = inventory.Rupees;
        if (!inventory.TryTakeMapleDrop(13, out int rupeeDrop) ||
            rupeeDrop != 12 || inventory.Rupees != rupeesBefore - 1)
        {
            throw new InvalidOperationException(
                "Maple's one-rupee branch did not preserve the original five-rupee output bug.");
        }

        OracleSaveData unavailableSave = OracleSaveData.CreateStandardGame();
        var unavailable = new InventoryState(treasures, unavailableSave);
        unavailable.GiveTreasure(TreasureDatabase.TreasureSeedSatchel, 1);
        if (unavailable.TryTakeMapleDrop(7, out _))
        {
            throw new InvalidOperationException(
                "Maple dropped Pegasus Seeds without the original mistaken treasure-$07 check.");
        }
    }

    private void ValidateMapleSpawnThresholds()
    {
        using var harness = new MapleValidationHarness(
            this, group: 0, room: 0x01);
        harness.Save.SetMapleKillCounter(29);
        harness.Load();
        if (harness.Manager.Entities<MapleEncounter>().Count != 0 ||
            harness.Save.MapleKillCounter != 29 ||
            harness.Manager.RandomCalls != 256)
        {
            throw new InvalidOperationException(
                "Maple spawned before 30 kills or room parsing lost its 256 RNG calls.");
        }

        harness.Save.SetMapleKillCounter(30);
        harness.Load();
        if (harness.Manager.Entities<MapleEncounter>().Count != 1 ||
            harness.Save.MapleKillCounter != 0)
        {
            throw new InvalidOperationException(
                "An eligible present room did not spawn Maple and reset her kill counter.");
        }

        harness.Inventory.GiveTreasure(
            harness.Treasures.GetObject("TREASURE_OBJECT_RING_BOX_00"));
        harness.Inventory.GrantAppraisedRingForDebug((int)RingId.Maples);
        if (!harness.Inventory.SetRingBoxSlotFromList(
                0, (int)RingId.Maples) ||
            !harness.Inventory.EquipRingAt(0))
        {
            throw new InvalidOperationException(
                "Could not equip Maple's Ring for encounter validation.");
        }

        harness.Save.SetMapleKillCounter(14);
        harness.Load();
        if (harness.Manager.Entities<MapleEncounter>().Count != 0)
        {
            throw new InvalidOperationException(
                "Maple's Ring spawned Maple before its 15-kill boundary.");
        }
        harness.Save.SetMapleKillCounter(15);
        harness.Load();
        if (harness.Manager.Entities<MapleEncounter>().Count != 1 ||
            harness.Save.MapleKillCounter != 0)
        {
            throw new InvalidOperationException(
                "Maple's Ring did not lower the encounter boundary to 15 kills.");
        }
    }

    private void ValidateMapleTargetOrdering()
    {
        using var harness = new MapleValidationHarness(
            this, group: 0, room: 0x01);
        var database = new MapleEventDatabase();
        var encounter = new MapleEncounterState();
        var random = new OracleRandom();
        MapleDroppedItem first = CreateMapleTarget(
            database.Item(5), encounter, harness, random, 0,
            new Vector2(40, 40));
        MapleDroppedItem later = CreateMapleTarget(
            database.Item(5), encounter, harness, random, 1,
            new Vector2(60, 40));
        if (!ReferenceEquals(encounter.ChooseTarget(40, 50), later))
        {
            throw new InvalidOperationException(
                "Maple no longer selects the later part slot on an equal-distance normal-item tie.");
        }

        MapleDroppedItem unique = CreateMapleTarget(
            database.Item(1), encounter, harness, random, 2,
            new Vector2(120, 100));
        if (!ReferenceEquals(encounter.ChooseTarget(40, 50), unique))
        {
            throw new InvalidOperationException(
                "Maple no longer prioritizes unique item IDs $00-$04 before normal loot.");
        }

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
            slot, position, 0, static (_, _) => { });
        encounter.Register(item);
        item.UpdateFrame(harness.Player);
        return item;
    }

    private void ValidateMaplePastEncounter()
    {
        using var harness = new MapleValidationHarness(
            this, group: 1, room: 0x02);
        harness.Inventory.GiveTreasure(
            TreasureDatabase.TreasureSword, 1);
        harness.Inventory.GiveTreasure(
            TreasureDatabase.TreasureSeedSatchel, 1);
        harness.Save.SetMapleKillCounter(30);
        harness.Load();
        MapleEncounter maple =
            harness.Manager.Entities<MapleEncounter>().Single();

        harness.Step();
        if (maple.Stage != MapleEncounterStage.EntryDelay ||
            maple.Vehicle != 0 || maple.Variation != 0 ||
            maple.AnimationIndex != 0x19 ||
            maple.ShadowTexture.GetWidth() != 8 ||
            maple.ShadowTexture.GetHeight() != 16 ||
            maple.ShadowOffset != new Vector2(-4, 3) ||
            maple.DropPattern is < 0 or > 1 ||
            harness.Manager.RandomCalls != 257)
        {
            throw new InvalidOperationException(
                "Maple's broom initialization did not retain the exact one-cell " +
                "terrain shadow or consume one post-parse RNG value.");
        }
        bool firstShadowPhase = maple.ShadowDrawn;
        harness.Step();
        if (maple.ShadowDrawn == firstShadowPhase)
        {
            throw new InvalidOperationException(
                "Maple's automatic terrain shadow did not flicker on alternating updates.");
        }
        harness.Step();
        harness.Step();
        if (maple.Stage != MapleEncounterStage.Flying ||
            !harness.Sounds.Contains(OracleSoundEngine.MusMapleTheme))
        {
            throw new InvalidOperationException(
                "Maple's three-update entry delay did not begin her theme and shadow flight.");
        }

        AdvanceMapleToCollision(harness, maple);
        List<MapleDroppedItem> droppedItems =
            harness.Manager.Entities<MapleDroppedItem>();
        int dropped = droppedItems.Count;
        if (maple.Stage != MapleEncounterStage.Recoiling ||
            dropped is < 5 or > 10 ||
            droppedItems.Take(5).Any(item => item.ZFixed >= 0) ||
            droppedItems.Skip(5).Any(item => item.ZFixed != 0) ||
            !maple.ScreenTransitionsDisabled ||
            !maple.MenusDisabled ||
            harness.Manager.HorizontalScreenShakeCounter != 14 ||
            harness.Player.KnockbackFrames != 23 ||
            !harness.Sounds.Contains(OracleSoundEngine.SndScentSeed))
        {
            throw new InvalidOperationException(
                "Maple's collision did not scatter loot, knock both actors back, lock the room, " +
                $"and begin the 15-update horizontal shake (stage={maple.Stage}, " +
                $"drops={dropped}, transitions={maple.ScreenTransitionsDisabled}, " +
                $"menus={maple.MenusDisabled}, shake={harness.Manager.HorizontalScreenShakeCounter}, " +
                $"knockback={harness.Player.KnockbackFrames}, sounds={string.Join(',', harness.Sounds)}).");
        }

        AdvanceMapleToCompletion(harness, maple);
        int expectedOutcome = maple.LinkScore == maple.MapleScore
            ? 0x0708
            : maple.LinkScore < maple.MapleScore ? 0x0706 : 0x0707;
        if (!maple.Finished ||
            (harness.Save.MapleState & 0x0f) != 1 ||
            !harness.Save.HasGlobalFlag(
                OracleSaveData.GlobalFlagMapleMetInPast) ||
            !harness.Dialogues.Contains(0x0712) ||
            !harness.Dialogues.Contains(expectedOutcome) ||
            harness.Manager.ScreenTransitionsDisabled ||
            harness.Manager.PlayerMenusDisabled ||
            !harness.RoomMusic.Contains((1, 0x02)))
        {
            throw new InvalidOperationException(
                "Maple's past greeting, race result, departure, or persistent meeting state failed.");
        }
    }

    private void ValidateMapleRewards()
    {
        using var harness = new MapleValidationHarness(
            this, group: 0, room: 0x01);
        harness.Load();
        var database = new MapleEventDatabase();

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
        if (swordDrop.Finished)
        {
            throw new InvalidOperationException(
                "The sword granted Maple's scattered drop before its next part update.");
        }
        harness.Step();
        if (!swordDrop.Finished ||
            harness.Inventory.Rupees != swordRupeesBefore + 1)
        {
            throw new InvalidOperationException(
                "Sword collision did not collect Maple's grounded scattered item.");
        }
        harness.Player.WarpTo(new Vector2(16, 112), recordSafe: false);

        int potionSoundCount = harness.Sounds.Count(
            sound => sound == OracleSoundEngine.SndGetSeed);
        CollectMapleReward(harness, database.Item(4));
        if (!harness.Inventory.HasTreasure(
                TreasureDatabase.TreasurePotion) ||
            harness.Sounds.Count(
                sound => sound == OracleSoundEngine.SndGetSeed) !=
                    potionSoundCount + 1)
        {
            throw new InvalidOperationException(
                "Maple's Potion reward did not use its explicit SND_GETSEED override.");
        }

        harness.Inventory.GiveTreasure(
            harness.Treasures.GetObject("TREASURE_OBJECT_RING_BOX_00"));
        harness.Inventory.GrantAppraisedRingForDebug((int)RingId.GoldJoy);
        if (!harness.Inventory.SetRingBoxSlotFromList(
                0, (int)RingId.GoldJoy) ||
            !harness.Inventory.EquipRingAt(0))
        {
            throw new InvalidOperationException(
                "Could not equip the Gold Joy Ring for Maple reward validation.");
        }
        CollectMapleReward(harness, database.Item(5));
        if (harness.Inventory.EmberSeeds != 0x10)
        {
            throw new InvalidOperationException(
                "The Gold Joy Ring did not double Maple's five-Ember-Seed reward.");
        }

        int randomBeforeRing = harness.Manager.RandomCalls;
        CollectMapleReward(harness, database.Item(2));
        if (harness.Inventory.UnappraisedRingCount != 1 ||
            harness.Manager.RandomCalls != randomBeforeRing + 3)
        {
            throw new InvalidOperationException(
                "Maple's ring reward did not consume two scatter RNG values and " +
                "one shared RNG value for its imported tier.");
        }

        MapleDroppedItem heart = SpawnGroundedMapleReward(
            harness, database.Item(0));
        harness.Player.WarpTo(heart.Position, recordSafe: false);
        harness.Step(closeDialogue: false);
        if (!heart.Finished ||
            (harness.Save.MapleState & 0x80) == 0 ||
            harness.Inventory.HeartPieces != 1 ||
            !harness.Player.IsHoldingItemTwoHands ||
            !harness.Dialogue.IsOpen ||
            harness.Save.HasRoomFlag(
                0, 0x01, OracleSaveData.RoomFlagItem))
        {
            throw new InvalidOperationException(
                "Maple's Heart Piece did not use the held treasure path, set bit 7, " +
                "and avoid the room-item flag.");
        }
        harness.Dialogue.Close();
        harness.Interactions.Update(1.0 / 60.0, harness.Player);
        if (harness.Player.IsHoldingItemTwoHands)
        {
            throw new InvalidOperationException(
                "Closing Maple's Heart Piece text did not release Link's held-item pose.");
        }
    }

    private static void CollectMapleReward(
        MapleValidationHarness harness,
        MapleItemRecord record)
    {
        MapleDroppedItem item = SpawnGroundedMapleReward(harness, record);
        harness.Player.WarpTo(item.Position, recordSafe: false);
        harness.Step();
        if (!item.Finished)
        {
            throw new InvalidOperationException(
                $"Maple reward item ${record.Index:x2} was not collected on grounded contact.");
        }
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
            var encounter = new MapleEncounterState();
            MapleDroppedItem item =
                harness.Manager.Spawn<MapleDroppedItem>(
                    new MapleDroppedItemSpawn(
                        record,
                        encounter,
                        encounter.AllocateSlot(),
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
            TreasureDatabase.TreasureTradeItem, 0x08);
        harness.Save.SetMapleState(0x08);
        harness.Save.SetMapleKillCounter(30);
        harness.Load();
        MapleEncounter maple =
            harness.Manager.Entities<MapleEncounter>().Single();
        harness.Step();
        if (maple.Vehicle != 1 || maple.Variation != 1)
        {
            throw new InvalidOperationException(
                "A ninth unlinked Maple encounter did not select the vacuum.");
        }

        AdvanceMapleToCollision(harness, maple);
        if (harness.Manager.Entities<MapleDroppedItem>().Count != 0 ||
            (harness.Save.MapleState & 0x10) == 0)
        {
            throw new InvalidOperationException(
                "The Touching Book collision scattered ordinary loot or lost its temporary state bit.");
        }

        AdvanceMapleToCompletion(harness, maple);
        int[] exchangeTexts = [0x070d, 0x070e, 0x070f, 0x0710, 0x0711];
        if (!maple.Finished ||
            harness.Inventory.TradeItem != 0x09 ||
            (harness.Save.MapleState & 0x3f) != 0x29 ||
            exchangeTexts.Any(text => !harness.Dialogues.Contains(text)) ||
            harness.Player.IsHoldingItemTwoHands ||
            !harness.RoomMusic.Contains((0, 0x01)))
        {
            throw new InvalidOperationException(
                "The Touching Book exchange did not grant and present the Magic Oar, " +
                "set completion, clear the temporary bit, and depart.");
        }
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
        if (maple.Vehicle != 2 || maple.Variation != 1)
        {
            throw new InvalidOperationException(
                "A ninth linked-game Maple encounter did not select the UFO.");
        }

        harness.Save.SetMapleState(0x0f);
        harness.Save.SetMapleKillCounter(30);
        harness.Load();
        maple = harness.Manager.Entities<MapleEncounter>().Single();
        harness.Step();
        if (maple.Vehicle != 2 || maple.Variation != 2)
        {
            throw new InvalidOperationException(
                "Maple's capped fifteenth meeting did not unlock the full UFO path variation.");
        }
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
                if (!TextureHasOpaquePixel(maple.CurrentTexture))
                {
                    throw new InvalidOperationException(
                        $"Maple's main-flight animation ${maple.AnimationIndex:x2} " +
                        "was fully transparent after the shadow path.");
                }
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
        if (!maple.Finished)
        {
            throw new InvalidOperationException(
                $"Maple did not finish from stage {maple.Stage}, substate {maple.Substate}, " +
                $"position {maple.Position}, angle ${maple.Angle:x2}, target " +
                $"${maple.TargetAngle:x2}, scores {maple.LinkScore}/{maple.MapleScore} " +
                "within 6000 updates.");
        }
    }

    private sealed class MapleValidationHarness : IDisposable
    {
        private readonly ValidationRoot _owner;

        internal Node Scene { get; }
        internal OracleSaveData Save { get; }
        internal TreasureDatabase Treasures { get; }
        internal InventoryState Inventory { get; }
        internal RoomSession Rooms { get; }
        internal RoomEntityManager Manager { get; }
        internal ValidationRingPlayerWorld PlayerWorld { get; }
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
            Scene = new Node
            {
                Name = $"MapleValidation_{group}_{room:x2}"
            };
            _owner.AddChild(Scene);
            Save = OracleSaveData.CreateStandardGame();
            Treasures = new TreasureDatabase();
            Inventory = new InventoryState(Treasures, Save, () => -1);
            long tick = 0;
            Rooms = new RoomSession(
                group, room, () => tick, () => tick = 0, Save);
            PlayerWorld = new ValidationRingPlayerWorld();
            Player = new Player { Name = "MapleValidationPlayer" };
            Player.Initialize(
                PlayerWorld, Inventory, new Vector2(80, 80),
                new OracleRandom());
            Scene.AddChild(Player);
            var interfaceLayer = new Node { Name = "Interface" };
            var roomView = new RoomView { Name = "RoomView" };
            Dialogue = new DialogueBox { Name = "Dialogue" };
            Dialogue.SetBackgroundPaletteProvider(
                (palette, shade) =>
                    Rooms.CurrentRoom.ResolveBackgroundPaletteColor(
                        palette, shade));
            Scene.AddChild(interfaceLayer);
            Scene.AddChild(roomView);
            Scene.AddChild(Dialogue);
            Manager = new RoomEntityManager(
                Scene, new NpcDatabase(), new EnemyDatabase(),
                new ItemDropDatabase(), new TimePortalDatabase(),
                new OracleRandom(), Save, new OracleRuntimeState(),
                Inventory, () => tick, Treasures, Rooms);
            Manager.MapleDialogueRequested +=
                (textId, _, _) => Dialogues.Add(textId);
            Manager.SoundRequested += Sounds.Add;
            Manager.RoomMusicRequested +=
                (musicGroup, musicRoom) =>
                    RoomMusic.Add((musicGroup, musicRoom));
            Interactions = new InteractionController(
                Rooms, Manager, new SignDatabase(), new ChestDatabase(),
                Treasures, Dialogue, Scene, roomView,
                static position => position, () => tick, Inventory,
                interfaceLayer, Sounds.Add);
        }

        internal void Load() =>
            Manager.LoadRoom(Rooms.ActiveGroup, Rooms.CurrentRoom);

        internal void Step(bool closeDialogue = true)
        {
            PlayerWorld.FrameCounter =
                (PlayerWorld.FrameCounter + 1) & 0xff;
            Manager.Update(1.0 / 60.0, Player);
            if (closeDialogue && Dialogue.IsOpen)
                Dialogue.Close();
            Interactions.Update(1.0 / 60.0, Player);
            Player._PhysicsProcess(1.0 / 60.0);
        }

        public void Dispose()
        {
            Manager.Clear();
            Manager.Dispose();
            _owner.RemoveChild(Scene);
            Scene.QueueFree();
        }
    }
}
