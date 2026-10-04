using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSeedSatchelLifecycleRom() => ValidateSeedSatchelGameplayRom(false);

    private void ValidateSeedSatchelCapacityRom() => ValidateSeedSatchelGameplayRom(true);

    private void ValidateGaleSatchelIndoorRom() => ValidateSeedSatchelGameplayRom(false, gale: true);
    private void ValidateGaleSatchelAirborneRom() => ValidateSeedSatchelGameplayRom(false, gale: true, airborne: true);

    private void ValidateSeedSatchelClearingRom() => ValidateSeedSatchelGameplayRom(false, clearing: true);

    private void ValidateSeedSatchelReflectorRom() => ValidateSeedSatchelGameplayRom(false, reflectors: true);

    private void ValidateSeedSatchelAirborneRom() => ValidateSeedSatchelGameplayRom(false, airborne: true);

    private void ValidateSeedSatchelGameplayRom(bool capacity, bool gale = false, bool clearing = false, bool reflectors = false, bool airborne = false)
    {
        int hostCase1 = 0;
        foreach (bool landedClear in clearing ? new[] { false, true } : new[] { false })
        foreach (bool primary in new[] { false, true })
        foreach (int direction in Enumerable.Range(0, 4))
        foreach (int seedItem in gale ? new[] { 0x23 } : capacity ? new[] { 0x20 } : new[] { 0x20, 0x21, 0x24 })
        foreach (int jumpUpdates in airborne ? new[] { 1, 15, 29 } : new[] { 0 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            if (airborne && direction != (seedItem - 0x20 + jumpUpdates / 14) % 4) continue;
            ReinitializeGameplayForValidation();
            LoadValidationRoom(gale || reflectors ? 4 : 0, gale ? 0xba : reflectors ? 0xa6 : 0x33); _entities.Clear();
            FailIf(gale && (_currentRoom.TilesetFlags & 1) != 0, "Indoor Gale fixture must not have TILESETFLAG_OUTDOORS.");
            _inventory.GiveTreasure(TreasureId.SeedSatchel, 1);
            if (airborne) _inventory.GiveTreasure(TreasureId.Feather, 1);
            _inventory.GiveTreasure(seedItem, 0x20);
            if (capacity) _inventory.GiveTreasure(0x21, 0x20);
            _inventory.SelectSatchelSeeds(seedItem - 0x20);
            _inventory.EquipA(primary ? TreasureId.SeedSatchel : airborne ? TreasureId.Feather : 0);
            _inventory.EquipB(primary ? airborne ? TreasureId.Feather : 0 : TreasureId.SeedSatchel);
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), 0xa0, 0, 0);
            Vector2 facing = OracleObjectMovement.Shared.Direction(direction * 8);
            RotatableSeedThingRoomEntity? reflector = null;
            Vector2 linkPosition = new(80.25f, 64.5f);
            if (reflectors)
            {
                var data = new DungeonMechanicDatabase();
                var record = data.GetRoomRecords(4, 0xa6).Single(row => row.Id == 0x33 && row.SubId == 0x08);
                reflector = new(record, data, new DungeonInteractionVisualDatabase().Visual("rotatable-seed-thing"),
                    _currentRoom, _runtimeState, () => _entities.FrameCounter, _entities.TryCreateSeedReflectorChild);
                _entities.AddEntity(reflector);
                // Independent seeds.s Satchel XY offsets. Start within the
                // PART's combined radii at Z=-2, through the real item parent.
                Vector2[] offsets = [new(0, -4), new(4, 1), new(0, 5), new(-5, 1)];
                linkPosition = new Vector2(104.25f, 40.5f) - facing * 6 - offsets[direction];
            }
            _player.WarpTo(linkPosition); _player.Face(new((int)facing.X, (int)facing.Y));
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, direction, (int)linkPosition.X, (int)linkPosition.Y) { HostilePartsEnabled = reflectors };
            rom[0xd00a] = 0x80; rom[0xd00c] = 0x40; rom.InitializeLinkGameplay();
            if (reflectors)
            {
                rom[0xd0c0] = 1; rom[0xd0c1] = 0x33; rom[0xd0c2] = 0x08;
                rom[0xd0cb] = 40; rom[0xd0cd] = 104;
            }
            var sounds = _sound.AttachPlayRequestAudit();
            int button = primary ? 1 : 2, update = 0, contacts = 0;
            if (capacity)
            {
                var bombs = new BombDatabase().Data;
                for (int index = 0; index < 4; index++)
                {
                    _entities.Spawn<BombEffect>(new BombSpawn(_player, bombs, 0, _ => { }));
                    int slot = 0xd700 + index * 0x100;
                    rom[slot] = 1; rom[slot + 1] = 3;
                    rom[slot + 0x0b] = 64; rom[slot + 0x0d] = 80;
                }
            }
            void Step(int count = 1, bool press = false, int angle = 0xff, bool feather = false)
            {
                Vector2 movement = angle == 0xff ? Vector2.Zero : OracleObjectMovement.Shared.Direction(angle);
                int directions = (movement.X > 0 ? 0x10 : movement.X < 0 ? 0x20 : 0) |
                    (movement.Y > 0 ? 0x80 : movement.Y < 0 ? 0x40 : 0);
                int itemsHeld = (press ? button : 0) | (feather ? primary ? 2 : 1 : 0);
                int edge = itemsHeld;
                StepGameplayUpdates(count, movement, MenuRomActions(itemsHeld | directions), MenuRomActions(edge), batched, () =>
                {
                    rom.UpdateGameplay(edge, itemsHeld | directions, angle, _entities.FrameCounter); edge = 0;
                    string context = $"Satchel ITEM${seedItem:x2} A={primary} dir={direction} jumpUpdates={jumpUpdates} update={++update}";
                    bool parent = Enumerable.Range(0xd2, 4).Any(page => rom[page << 8] != 0 && rom[(page << 8) + 1] == 0x19);
                    if (reflector is not null)
                    {
                        FailIf(reflector.Counter != rom[0xd0c6] || reflector.Orientation != rom[0xd0c8] ||
                            reflector.CollisionRadii != new Vector2(rom[0xd0e7], rom[0xd0e6]) || reflector.SeedBounceOrientation != rom[0xd0e1],
                            context + ": PART$33 parent timer/orientation/radii/parameter differs.");
                        var child = _entities.Entities<SeedReflectorChildRoomEntity>().Single();
                        FailIf(child.Position != new Vector2(rom.Word(0xd1cc) / 256f, rom.Word(0xd1ca) / 256f) ||
                            child.CollisionRadii != new Vector2(rom[0xd1e7], rom[0xd1e6]) || child.SeedBounceOrientation != rom[0xd1e1] ||
                            child.Visible || rom[0xd1cf] != 0xf2, context + ": PART$33:$03 child properties differ.");
                    }
                    FailIf(_player.IsUsingSeedSatchel != parent, context + ": parent initialization/retirement differs.");
                    FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256.0f, rom.Word(0xd00a) / 256.0f) ||
                        CarriedObjectMotion.DirectionIndex(_player.FacingVector) != rom[0xd008],
                        context + ": movement lock/facing differs.");
                    if (airborne)
                    {
                        FailIf(_player.TopDownAirborne != (rom[0xcc5c] != 0) ||
                            (_player.ItemCreationZFixed & 0xffff) != rom.Word(0xd00e) ||
                            (_player.TopDownAirSpeedZ & 0xffff) != rom.Word(0xd014),
                            context + ": Link full Z/gravity/landing differs.");
                        FailIf(!sounds.Requests.Where(id => id != SoundId.SndText).SequenceEqual(rom.Sounds),
                            context + ": full Feather/Satchel sound order differs.");
                    }
                    var items = _entities.Entities<EmberSeedEffect>();
                    int[] children = Enumerable.Range(0xd7, 5).Select(page => page << 8)
                        .Where(slot => rom[slot] != 0 && rom[slot + 1] is 0x20 or 0x21 or 0x23 or 0x24).ToArray();
                    FailIf(items.Count != children.Length, context + ": seed allocation/deletion differs.");
                    for (int index = 0; index < items.Count; index++)
                    {
                        var item = items[index]; int slot = children.Single(child => rom[child + 1] == item.SeedItem);
                        if (reflectors && (rom[slot + 0x2a] & 0x10) != 0) contacts++;
                        FailIf(reflectors && item.BouncesRemaining != rom[slot + 0x34], context + ": Satchel reflector bounce count differs.");
                        int state = item.State == EmberState.Flying ? 1 : item.State is EmberState.Scent or EmberState.Gale ? 2 : 3;
                        FailIf(state != rom[slot + 4] || item.PrecisePosition != new Vector2(rom.Word(slot + 0x0c) / 256.0f, rom.Word(slot + 0x0a) / 256.0f) ||
                            (item.ZFixed & 0xffff) != rom.Word(slot + 0x0e) || (item.SpeedZ & 0xffff) != rom.Word(slot + 0x14) ||
                            item.NativeAngle != rom[slot + 9] || item.Visible != ((rom[slot + 0x1a] & 0x80) != 0) ||
                            item.CollisionEnabled != ((rom[slot + 0x24] & 0x80) != 0),
                            context + $": seed state/full XY/Z/angle/collision differs: runtime={state}/{item.PrecisePosition}/{item.ZFixed}/{item.SpeedZ}/a{item.NativeAngle}/v{item.Visible}/c{item.CollisionEnabled}, native={rom[slot + 4]}/{rom.Word(slot + 0x0c) / 256.0f},{rom.Word(slot + 0x0a) / 256.0f}/{rom.Word(slot + 0x0e)}/{rom.Word(slot + 0x14)}/a{rom[slot + 9]}/v{rom[slot + 0x1a]:x2}/c{rom[slot + 0x24]:x2}.");
                        int counter = (int)typeof(EmberSeedEffect).GetField("_frameCounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(item)!;
                        FailIf(counter != rom[slot + 0x20] || state > 1 && item.FlameCounter != rom[slot + 6] ||
                            item.SeedItem == 0x24 && item.MysteryEffect != rom[slot + 3],
                            context + $": seed animation/effect differs: runtime=frame{counter}/life{item.FlameCounter}/mystery{item.MysteryEffect}, native=frame{rom[slot + 0x20]}/life{rom[slot + 6]}/mystery{rom[slot + 3]}/state{rom[slot + 4]}/hit{rom[slot + 0x2a]:x2}.");
                        if (gale && state == 2)
                            FailIf(item.GaleSubstate != rom[slot + 5] || item.GaleCounter2 != rom[slot + 7] || item.GalePalette != rom[slot + 0x1c] || item.GaleMenuRequested,
                                context + ": indoor Gale substate/counter/palette or menu request differs.");
                    }
                    for (int address = 0xc6b9; address <= 0xc6bd; address++)
                        FailIf(_saveData.ReadWramByte(address) != rom[address], context + $": BCD seed byte ${address:x4} differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls,
                        context + ": seed shared RNG differs.");
                    FailIf(!sounds.Requests.Where(id => id is 0x52 or 0x72 or 0x85 or 0x7b or 0x90)
                        .SequenceEqual(rom.Sounds.Where(id => id is 0x52 or 0x72 or 0x85 or 0x7b or 0x90)),
                        context + ": seed landing/activation sounds differ.");
                });
            }
            for (int repeat = 0; repeat < 2; repeat++)
            {
                int contactsBefore = contacts;
                if (airborne)
                {
                    Step(jumpUpdates, feather: true);
                    FailIf(!_player.TopDownAirborne || rom[0xcc5c] == 0,
                        "Airborne Satchel must launch through the opposite equipped Feather button before throwing.");
                }
                Step(1, true, (direction * 8 + 8) & 0x1f);
                if (airborne)
                    FailIf(_entities.Entities<EmberSeedEffect>().Count != 1,
                        "Ordinary Feather air state must permit a fresh physical Satchel child.");
                Step(3);
                _dialogue.ShowMessage("Seed flight pause.", _player.Position.Y); rom[0xcba0] = 1;
                Step(6);
                _dialogue.Close(); rom[0xcba0] = 0;
                FailIf(reflectors && repeat == 0 && contacts == contactsBefore,
                    $"PART$33 ITEM${seedItem:x2} dir={direction}: Satchel fixture did not publish a native reflector contact.");
                if (clearing)
                {
                    if (landedClear) Step(8);
                    FailIf(_entities.Entities<EmberSeedEffect>().Count != 1 ||
                        (_entities.Entities<EmberSeedEffect>().Single().State != EmberState.Flying) != landedClear ||
                        _player.IsUsingSeedSatchel == landedClear,
                        "Satchel clearing fixture missed its flying/live-parent or landed/retired-parent boundary.");
                    _player.ClearNativeItemParents();
                    rom.ClearItemParents();
                    Step();
                    FailIf(_player.IsUsingSeedSatchel || _entities.Entities<EmberSeedEffect>().Count != 1,
                        "Clearing ITEM$19's parent also cleared its independently owned physical seed.");
                    _entities.ClearPhysicalPlayerItems();
                    rom.ClearPhysicalItems();
                    Step(3);
                    FailIf(_entities.Entities<EmberSeedEffect>().Count != 0 || _player.IsUsingSeedSatchel,
                        "Native physical clearing retained a Satchel parent or seed child.");
                }
                if (capacity && repeat == 0)
                {
                    Step(8);
                    FailIf(_player.IsUsingSeedSatchel || _entities.Entities<EmberSeedEffect>().Single().State != EmberState.Burning,
                        "Capacity fixture did not reach a landed Ember item with its parent retired.");
                    _inventory.SelectSatchelSeeds(1); rom[0xc6c4] = 1;
                    Step(1, true);
                    FailIf(_player.IsUsingSeedSatchel || _inventory.ScentSeeds != 0x20,
                        "A burning ITEM$20 released its slot or let Scent consume ammo with a full child pool.");
                    foreach (var bomb in _entities.Entities<BombEffect>()) bomb.Discard();
                    for (int slot = 0xd700; slot <= 0xda00; slot += 0x100) rom.DeleteDynamicItem(slot);
                    Step(); Step(1, true);
                    FailIf(!_player.IsUsingSeedSatchel || _inventory.ScentSeeds != 0x19,
                        "Freeing capacity did not allow a new Scent throw alongside the retained Ember item.");
                    _inventory.SelectSatchelSeeds(0); rom[0xc6c4] = 0;
                }
                int began = update;
                while (_entities.Entities<EmberSeedEffect>().Count != 0 && update - began < 360) Step(3);
                FailIf(_entities.Entities<EmberSeedEffect>().Count != 0 || _player.IsUsingSeedSatchel,
                    "Satchel seed did not finish its bounded native effect and retire its parent.");
                Step(4);
            }
        }
        GD.Print($"Validated clean-US Satchel gameplay: capacity={capacity}, indoor Gale={gale}, physical clearing={clearing}, reflector={reflectors}, airborne={airborne}; A/B, four retained facings, full fixed flight/Z/animation/effect counters/expiry, ammo/RNG/sounds, dialogue and repeat through split/batched updates.");
    }
}
