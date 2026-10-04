using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePegasusShieldScrollRom()
    {
        var graphics = new ScreenTransitionGraphicsDatabase();
        int hostCase = 0;
        foreach (var route in new[] { (Group: 0, Source: 0x11, Target: 0x12), (Group: 4, Source: 0x07, Target: 0x03) })
        foreach (int direction in Enumerable.Range(0, 4))
        foreach (bool primary in new[] { false, true })
        foreach (bool batched in RomHostSchedules(hostCase++))
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(route.Group, route.Source); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.SeedSatchel, 1);
            _inventory.GiveTreasure(TreasureId.PegasusSeeds, 0x20);
            _inventory.GiveTreasure(TreasureId.Shield, 1);
            _inventory.SelectSatchelSeeds(2);
            _inventory.EquipA(primary ? TreasureId.SeedSatchel : TreasureId.Shield);
            _inventory.EquipB(primary ? TreasureId.Shield : TreasureId.SeedSatchel);
            // Controlled ground and scroll-setup boundary. Room parsing, enemy
            // consumers and room scripts have their own native comparisons.
            for (int y = 8; y < _currentRoom.Height; y += 16)
            for (int x = 8; x < _currentRoom.Width; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), 0x2c, 0, 0);
            Vector2I vector = direction switch { 0 => Vector2I.Up, 1 => Vector2I.Right, 2 => Vector2I.Down, _ => Vector2I.Left };
            _player.WarpTo(new(vector.X < 0 ? 6.75f : vector.X > 0 ? _currentRoom.Width - 6.25f : 80.75f,
                vector.Y < 0 ? 6.25f : vector.Y > 0 ? _currentRoom.Height - 7.75f : 64.25f));
            _player.Face(Vector2I.Down);
            var randomStart = _random.CaptureState();
            var rom = new SomariaRom(_saveData, randomStart, _currentRoom, 2,
                (int)_player.PrecisePosition.X, (int)_player.PrecisePosition.Y);
            rom[0xd00a] = 0x40; rom[0xd00c] = 0xc0; rom.InitializeLinkGameplay();
            var sounds = _sound.AttachPlayRequestAudit();
            ScrollRom? scroll = null;
            int nativeRandomStart = 0, update = 0, heldShield = primary ? 2 : 1;
            void Step(int count = 1, int pressed = 0, int held = 0)
            {
                int edge = pressed;
                StepGameplayUpdates(count, Vector2.Zero, MenuRomActions(held), MenuRomActions(pressed), batched, () =>
                {
                    // updateAllObjects runs before the screen-transition tail.
                    // Each native owner supplies the other's preceding state.
                    if (scroll is not null) rom[0xcd00] = scroll[0xcd00];
                    rom.UpdateGameplay(edge, held, 0xff, _entities.FrameCounter); edge = 0;
                    string context = $"Pegasus/Shield scroll large={route.Group == 4} dir={direction} A={primary} batch={batched} update={++update}";
                    FailIf(_seedSatchel.Pegasus.RawCounter != rom.Word(0xcc6c) || _inventory.PegasusSeeds != rom[0xc6bb],
                        context + ": timer/ammo differs.");
                    FailIf(_player.IsUsingShield != (rom[0xcc6f] != 0), context + ": retained Shield state differs.");
                    ComparePegasusDustRom(rom, false, context);
                    FailIf(!sounds.Requests.Where(id => id is SoundId.SndShield or 0xa3)
                        .SequenceEqual(rom.Sounds.Where(id => id is SoundId.SndShield or 0xa3)), context + ": item cues differ.");
                    if (scroll is not null && scroll[0xcd04] != 2)
                    {
                        scroll.Update();
                        rom.Word(0xd00a, scroll.Word(0xd00a)); rom.Word(0xd00c, scroll.Word(0xd00c));
                        FailIf(_transitions.ScrollActive != (scroll[0xcd04] != 2), context + ": completion boundary differs.");
                    }
                    Vector2 actual = new(((int)(_player.PrecisePosition.X * 256) & 0xffff) / 256.0f,
                        ((int)(_player.PrecisePosition.Y * 256) & 0xffff) / 256.0f);
                    FailIf(actual != new Vector2(rom.Word(0xd00c) / 256.0f, rom.Word(0xd00a) / 256.0f),
                        context + ": Link motion differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls - randomStart.Calls != rom.RandomCalls - nativeRandomStart,
                        context + ": item/shared RNG differs at the declared room-load boundary.");
                });
            }
            // Alternate startup animation and established two-cloud slots
            // without multiplying the room/direction/button fixtures.
            Step((direction & 1) == 0 ? 1 : 25, 3, 3);
            var dust = _entities.Entities<PegasusDustRoomEntity>().Single();
            FailIf(!_player.IsUsingShield || !_seedSatchel.Pegasus.Active,
                "Scroll fixture must allocate both equipped items through gameplay input.");
            int loadedUnique = graphics.ForTileset(_currentRoom.TilesetId).Unique |
                (_currentRoom.LoadsUniqueGraphicsAfterScroll ? 0x80 : 0);
            _transitions.BeginScroll(_player, vector, route.Target);
            // Destination parsing is outside the bounded native owners above.
            // Remove its consumers while retaining the outgoing ITEM $df.
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var active = (System.Collections.Generic.List<IRoomEntity>)typeof(RoomEntityManager).GetField("_activeEntities", flags)!.GetValue(_entities)!;
            typeof(RoomEntityManager).GetMethod("ClearEntities", flags)!.Invoke(_entities, [active]);
            typeof(RoomEventController).GetField("_eventsByPriority", flags)!.SetValue(_roomEvents, System.Array.Empty<IRoomEvent>());
            randomStart = _random.CaptureState(); nativeRandomStart = rom.RandomCalls;
            rom[0xff94] = randomStart.Rng1; rom[0xff95] = randomStart.Rng2;
            int unique = graphics.ForTileset(_currentRoom.TilesetId).Unique |
                (_currentRoom.LoadsUniqueGraphicsAfterScroll ? 0x80 : 0);
            scroll = new ScrollRom(route.Group == 4, direction, (int)(_player.PrecisePosition.X * 256),
                (int)(_player.PrecisePosition.Y * 256), unique, loadedUnique);
            rom.Word(0xd00a, scroll.Word(0xd00a)); rom.Word(0xd00c, scroll.Word(0xd00c));
            int counter = _seedSatchel.Pegasus.RawCounter;
            Step(3, held: heldShield); Step(_transitions.ScrollTotalFrames - 6); Step(3, held: heldShield);
            FailIf(_transitions.ScrollActive || _seedSatchel.Pegasus.RawCounter != counter ||
                !_entities.Entities<PegasusDustRoomEntity>().Contains(dust),
                "Native enabled=$03 dust and both item parents must survive the final frozen object update.");
            rom.CopyRoom(_currentRoom);
            rom[0xcc2d] = (byte)_currentRoom.Group; rom[0xcc30] = (byte)_currentRoom.Id;
            Step(3, held: heldShield); Step(); Step(1, heldShield, heldShield); Step(8, held: heldShield);
        }
        GD.Print("Validated clean-US Pegasus/Shield small/large scrolls in all directions, A/B, retained timer/parent and advancing reserved dust, held/released input, final frozen update, resumed use and split/batched gameplay at declared room-load boundaries.");
    }
}
