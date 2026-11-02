
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedShovel(FrontendValidationRoot root, bool batched)
    {
        var session = root._gameplaySession!;
        var rooms = session.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player;
        var inventory = player.Inventory;
        var shovel = ((PlayerWorld)player.World).Shovel;
        var app = new ApplicationValidationFixture(root);
        OracleGame game = rooms.SaveData.Game;
        byte floor = (byte)(game == OracleGame.Ages ? 0x3a : 0x04);
        CheckSharedShovelOam(game, rooms.AssetRoot);
        byte[] tiles = game == OracleGame.Ages ? [0x01, 0xcb, 0x1c] : [0x01, 0xc9, 0xda, 0xdb, 0x10];
        foreach (byte tile in tiles)
        foreach (int direction in Enumerable.Range(0, 4))
        foreach (bool primary in new[] { true, false })
        {
            entities.Clear(); player.WarpTo(new(72, 80));
            var room = rooms.CurrentRoom;
            for (int y = 16; y < 112; y += 16)
            for (int x = 16; x < 144; x += 16)
                room.SetPositionTileAndCollision(new(x + 8, y + 8), floor, 0, 0);
            Vector2 target = new(72, 72);
            room.SetPositionTileAndCollision(target, tile, 0, 0);
            room.SetUnderlyingMetatile(target, floor);
            // Stay on adjacent dry floor when Seasons replaces the target with a hole.
            Vector2 position = direction switch { 0 => new(72, 80), 1 => new(63, 68), 2 => new(72, 57), _ => new(80, 68) };
            player.WarpTo(position); player.Face((Vector2I)OracleObjectMath.StrictCardinalVector(direction * 8));
            inventory.GiveTreasure(TreasureId.Shovel, 0);
            inventory.EquipA(primary ? TreasureId.Shovel : 0); inventory.EquipB(primary ? 0 : TreasureId.Shovel);
            var random = entities.Random.CaptureState();
            var native = new BraceletRom(room, position, direction, 0xff, game: game);
            native.LoadSave(rooms.SaveData);
            var clean = ValidationRom.LoadCleanUs(game);
            int properties = game == OracleGame.Ages ? 0xfdd4b : 0xfdd71;
            native.SetResidentObjectGraphics(2,clean.Span[properties + 0x10 * 4]);
            native.SetResidentObjectGraphics(3,clean.Span[properties + 0x51 * 4]);
            native[0xcc2d] = (byte)rooms.ActiveGroup; native[0xcc2e] = (byte)(room.Width > 160 ? 1 : 0);
            native[0xff94] = random.Rng1; native[0xff95] = random.Rng2;
            var audit = root._sound.AttachPlayRequestAudit();
            string button = primary ? "attack" : "item";
            int updates = 0;
            void Compare()
            {
                FailIf(player.IsUsingShovel != (native[0xd200] != 0),
                    $"{game}: Shovel ${tile:x2}, direction={direction}, A={primary}: native parent lifetime differs at {updates}.");
                int[] children = Enumerable.Range(0xd6, 10).Select(page => page << 8)
                    .Where(slot => native[slot] != 0 && native[slot + 1] == 0x15).ToArray();
                FailIf(shovel.ChildActive != (children.Length == 1), $"{game}: native Shovel child allocation/deletion differs at {updates}.");
                if (children.Length == 1)
                {
                    int child = children[0];
                    FailIf(shovel.ChildCounter != native[child + 6] || shovel.ChildPosition != new Vector2(native.Word(child + 0x0c) / 256f, native.Word(child + 0x0a) / 256f),
                        $"{game}: native Shovel child counter/position differs at {updates}.");
                }
                int packed = room.GetPackedPosition(target);
                int maturity = game == OracleGame.Ages ? 0xc65f : 0xc65c;
                FailIf(room.GetMetatile(target) != native[0xcf00 + packed] || room.GetUnderlyingMetatile(target) != native.Underlying(packed) ||
                    room.GetTerrainInfo(target).Collision != native[0xce00 + packed] || rooms.SaveData.GashaMaturity != native.Word(maturity),
                    $"{game}: native Shovel tile/underlying/collision/maturity side effects differ at {updates}.");
                var chips = entities.Entities<ShovelDebrisEffect>();
                int[] slots = Enumerable.Range(0xd0, 16).Select(page => (page << 8) + 0x40)
                    .Where(slot => native[slot] != 0 && native[slot + 1] == 0x0a).ToArray();
                FailIf(chips.Count != slots.Length, $"{game}: native Shovel debris lifetime differs at {updates}.");
                for (int index = 0; index < slots.Length; index++)
                {
                    var chip = chips[index]; int slot = slots[index];
                    FailIf(chip.AssetRoot != rooms.AssetRoot || chip.PrecisePosition != new Vector2(native.Word(slot + 0x0c) / 256f, native.Word(slot + 0x0a) / 256f) ||
                        chip.ZFixed != unchecked((short)native.Word(slot + 0x0e)) || chip.SpeedZ != unchecked((short)native.Word(slot + 0x14)),
                        $"{game}: native Shovel debris XYZ/speed differs at {updates}.");
                }
                var snow = entities.Entities<StationaryDebrisEffect>().Where(effect => effect.InteractionId == 9).ToArray();
                int[] snowSlots = Enumerable.Range(0xd0, 16).Select(page => (page << 8) + 0x40)
                    .Where(slot => native[slot] != 0 && native[slot + 1] == 9).ToArray();
                FailIf(snow.Length != snowSlots.Length, $"{game}: native snow debris allocation/deletion differs at {updates}.");
                for (int index = 0; index < snowSlots.Length; index++)
                {
                    int slot = snowSlots[index];
                    FailIf(snow[index].Visible != ((native[slot + 0x1a] & 0x80) != 0) || snow[index].CurrentParameter != native[slot + 0x21],
                        $"{game}: native snow debris slot flicker/animation differs at {updates}.");
                }
                var rng = entities.Random.CaptureState();
                FailIf(rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95],
                    $"{game}: native Shovel/drop RNG differs at {updates}, tile ${tile:x2}, direction {direction}, A={primary}: " +
                    $"runtime=${rng.Rng1:x2}/${rng.Rng2:x2}, calls={rng.Calls - random.Calls}; " +
                    $"native=${native[0xff94]:x2}/${native[0xff95]:x2}, calls={native.RandomCalls}; seed=${random.Rng1:x2}/${random.Rng2:x2}.");
            }
            void Step(int count, bool press = false)
            {
                int update = 0;
                app.Step(count, Vector2.Zero, held: press ? [button] : [], pressed: press ? [button] : [], batched: batched,
                    afterUpdate: () =>
                    {
                        native[0xcc00] = (byte)entities.FrameCounter;
                        native.Update(held: press, pressed: press && update++ == 0, primary: primary,
                            updateEnemies: true, updateInteractions: true, item: ItemId.Shovel, updateParts: true);
                        updates++; Compare();
                    });
            }
            Step(1, true); Step(4);
            rooms.RuntimeState.SetField("wDisabledObjects", 0x02);
            native[game == OracleGame.Ages ? 0xcc8a : 0xcca4] = 0x02;
            Step(3);
            rooms.RuntimeState.SetField("wDisabledObjects", 0);
            native[game == OracleGame.Ages ? 0xcc8a : 0xcca4] = 0;
            root._scene.Dialogue.ShowMessage("Shovel pause.", player.Position.Y); native[0xcba0] = 1;
            Step(3); root._scene.Dialogue.Close(); native[0xcba0] = 0;
            Step(30);
            Step(1, true); Step(30);
            int[] relevant = [SoundId.SndDig, SoundId.SndClink, SoundId.SndCutGrass, SoundId.SndSolvePuzzle];
            FailIf(!audit.Requests.Where(relevant.Contains).SequenceEqual(native.Sounds.Where(relevant.Contains)) || shovel.ChildActive || player.IsUsingShovel,
                $"{game}: shared Shovel repeat/completion or native cue order differs for ${tile:x2}.");
        }
        CheckSharedShovelCancellation(root, app, entities, player, shovel, batched);
        CheckSharedShovelHits(root, app, entities, player, shovel, batched);
        CheckSharedLinkMovementMasks(root, app, entities, player, batched);
    }

    private static void CheckSharedShovelCancellation(FrontendValidationRoot root, ApplicationValidationFixture app,
        RoomEntityManager entities, Player player, ShovelController shovel, bool batched)
    {
        var room = root._gameplaySession!.Rooms.CurrentRoom;
        var inventory = player.Inventory;
        inventory.EquipA(TreasureId.Shovel); inventory.EquipB(0);
        foreach (int phase in new[] { 3, 4, 6, 8 })
        foreach (bool physical in new[] { false, true })
        {
            entities.Clear(); player.WarpTo(new(72, 80)); player.Face(Vector2I.Up);
            // Isolate child cancellation from random soil rewards/enemy content.
            room.SetPositionTileAndCollision(new(72, 72), 0xa0, 0, 0);
            app.Step(1, Vector2.Zero, held: ["attack"], pressed: ["attack"], batched: batched);
            app.Step(phase, Vector2.Zero, batched: batched);
            bool child = phase is >= 4 and < 8;
            FailIf(shovel.ChildActive != child || child && shovel.ChildCounter != 8 - phase,
                "The shared Shovel child must retain its four-update boundary independently of its parent.");
            root._scene.Dialogue.ShowMessage("Clear Shovel parents.", player.Position.Y);
            app.Step(3, Vector2.Zero, batched: batched);
            player.ClearNativeItemParents();
            if (physical) entities.ClearPhysicalPlayerItems();
            app.Step(2, Vector2.Zero, batched: batched);
            FailIf(player.IsUsingShovel || shovel.ChildActive != (child && !physical) ||
                child && !physical && shovel.ChildCounter != 8 - phase,
                "Parent clearing must retain a frozen Shovel child; physical clearing must retire it through the item owner.");
            root._scene.Dialogue.Close(); app.Step(6, Vector2.Zero, batched: batched);
            FailIf(shovel.ChildActive, "Cancelled Shovel children must finish after their remaining eligible item updates.");
        }
    }

    private static void CheckSharedShovelHits(FrontendValidationRoot root, ApplicationValidationFixture app,
        RoomEntityManager entities, Player player, ShovelController shovel, bool batched)
    {
        var rooms = root._gameplaySession!.Rooms;
        OracleGame game = rooms.SaveData.Game;
        byte floor = (byte)(game == OracleGame.Ages ? 0x3a : 0x04);
        foreach (int side in new[] { -1, 0, 1 })
        {
            entities.Clear(); player.WarpTo(new(72, 80)); player.Face(Vector2I.Up);
            for (int y = 16; y < 112; y += 16)
            for (int x = 16; x < 144; x += 16)
                rooms.CurrentRoom.SetPositionTileAndCollision(new(x + 8, y + 8), floor, 0, 0);
            // Isolate contact geometry from random soil rewards/enemy content.
            rooms.CurrentRoom.SetPositionTileAndCollision(new(72,64),0xa0,0,0);
            player.Inventory.EquipA(TreasureId.Shovel); player.Inventory.EquipB(0);
            FailIf(!entities.TrySpawnDebugEnemy(0x32, 0, new(72, 64), out string error), error);
            var target = entities.Entities<KeeseCharacter>().Single();
            app.Step(1, Vector2.Zero, batched: batched);
            target.Position = new(72 + side * ((int)target.CollisionBounds.Size.X / 2 + 3), 68);
            target.Health = 9;
            int health = player.Inventory.HealthQuarters;
            app.Step(1, Vector2.Zero, held: ["attack"], pressed: ["attack"], batched: batched);
            for (int update = 0; !shovel.ChildActive && update < 10; update++) app.Step(1, Vector2.Zero, batched: batched);
            FailIf(!shovel.ChildActive, $"{game}: real Shovel input did not allocate its collision child.");
            var native = new ObjectCollisionRom(game);
            native[0xd080] = 1; native[0xd081] = 0x32; native[0xd0a4] = 0xb2;
            native[0xd0a5] = (byte)(game == OracleGame.Ages ? 0x1f : 0x29);
            native[0xd08b] = (byte)target.Position.Y; native[0xd08d] = (byte)target.Position.X;
            native[0xd0a6] = (byte)(target.CollisionBounds.Size.Y / 2); native[0xd0a7] = (byte)(target.CollisionBounds.Size.X / 2);
            native[0xd0a9] = 9;
            native[0xd600] = 1; native[0xd601] = 0x15;
            native[0xd624] = (byte)(game == OracleGame.Ages ? 0x8c : 0x8d);
            native[0xd626] = native[0xd627] = 3; native[0xd629] = 0xff;
            native[0xd60b] = (byte)shovel.ChildPosition.Y; native[0xd60d] = (byte)shovel.ChildPosition.X;
            native.Call(native.ScanAddress);
            FailIf(native.Dispatches.Count != (side >= 0 ? 1 : 0), $"{game}: clean-ROM Shovel scan must include the negative summed-radius edge and exclude the positive edge.");
            FailIf(target.Health != native[0xd0a9] || target.NativeHitPending != ((native[0xd0aa] & 0x80) != 0) ||
                target.InvincibilityCounter != unchecked((sbyte)native[0xd0ab]) || target.KnockbackCounter != native[0xd0ad] ||
                player.Inventory.HealthQuarters != health,
                $"{game}: real Shovel child contact, side={side}, differs from native scan: health={target.Health}/{native[0xd0a9]}, pending={target.NativeHitPending}/${native[0xd0aa]:x2}, inv={target.InvincibilityCounter}/{unchecked((sbyte)native[0xd0ab])}, recoil={target.KnockbackCounter}/{native[0xd0ad]}, player={player.Inventory.HealthQuarters}/{health}.");
            int rest = target.Counter1;
            bool hit = target.NativeHitPending;
            app.Step(1, Vector2.Zero, batched: batched);
            if (hit) FailIf(target.NativeHitPending || target.InvincibilityCounter != -0x14 || target.KnockbackCounter != 0x0b || target.Counter1 != rest,
                $"{game}: Shovel JUST_HIT must be consumed once before Keese recoil/AI on the following visit.");
        }
        player.WarpTo(new(72, 80)); entities.Clear();
    }

    private static void CheckSharedLinkMovementMasks(FrontendValidationRoot root, ApplicationValidationFixture app,
        RoomEntityManager entities, Player player, bool batched)
    {
        var rooms = root._gameplaySession!.Rooms;
        OracleGame game = rooms.SaveData.Game;
        player.WarpTo(new(72, 80)); player.Face(Vector2I.Right);
        player.Inventory.EquipA(0); player.Inventory.EquipB(0);
        var native = new BraceletRom(rooms.CurrentRoom, player.Position, 1, 0xff, game: game);
        foreach (byte mask in new byte[] { 0, 0x02, 0x04, 0x08, 0x0e, 0x01, 0x80 })
        {
            rooms.RuntimeState.SetField("wDisabledObjects", mask);
            Vector2 before = player.PrecisePosition;
            // link.s:linkState01 returns for $81 before its ordinary movement
            // dispatch; other categories still reach bank5's native wall/move code.
            app.Step(4, Vector2.Right, batched: batched,
                afterUpdate: () => { if ((mask & 0x81) == 0) native.Walk(8); });
            Vector2 expected = new(native.Word(0xd00c) / 256f, native.Word(0xd00a) / 256f);
            FailIf(player.PrecisePosition != expected || (mask & 0x81) == 0 && player.PrecisePosition == before,
                $"{game}: disabled-object mask ${mask:x2} must retain the normal-Link $81 gate and native movement: runtime={player.PrecisePosition}, native={expected}.");
        }
        rooms.RuntimeState.SetField("wDisabledObjects", 0);
    }

    private static void CheckSharedShovelOam(OracleGame game, string root)
    {
        var rom = ValidationRom.LoadCleanUs(game);
        int bank = game == OracleGame.Ages ? 0x16 : 0x14;
        int oamBank = game == OracleGame.Ages ? 0x14 : 0x13;
        int table = bank * 0x4000 + (game == OracleGame.Ages ? 0x1c03 : 0x16d5);
        int pointers = bank * 0x4000 + (game == OracleGame.Ages ? 0x2e68 : 0x2801);
        var imported = GeneratedTable.Load(root + "effects/shovel_debris.tsv",
            new("native debris comparison", GeneratedTableKeySemantics.Unique,
                ["direction", "tile-base", "palette", "speed-raw", "initial-speed-z", "gravity", "animation", "source"],
                ["direction"], headerRequired: true));
        int Word(int at) => rom.Span[at] | rom.Span[at + 1] << 8;
        for (int direction = 0; direction < 4; direction++)
        {
            var frames = OracleGraphicsCache.GetAnimationDefinition(imported.Rows[direction].RequiredString(6)).Frames;
            int animation = bank * 0x4000 + (Word(table + direction * 2) & 0x3fff);
            for (int frame = 0; frame < 2; frame++)
            {
                int at = animation + frame * 3;
                int cells = oamBank * 0x4000 + (Word(pointers + rom.Span[at + 1]) & 0x3fff);
                var expected = new System.Collections.Generic.List<string>();
                for (int cell = 0; cell < rom.Span[cells]; cell++)
                {
                    int offset = cells + 1 + cell * 4;
                    expected.Add($"{rom.Span[offset]},{rom.Span[offset + 1]},{rom.Span[offset + 2]},{rom.Span[offset + 3]}");
                }
                FailIf(frames[frame].Duration != rom.Span[at] || frames[frame].Parameter != rom.Span[at + 2] ||
                    frames[frame].EncodedOam != string.Join(';', expected),
                    $"{game}: Shovel direction ${direction:x2}, frame ${frame:x2} must use its clean-ROM duration/parameter/OAM, including native Y offsets.");
            }
        }
    }
}
