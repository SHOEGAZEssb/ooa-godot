using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSeedShooterLifecycleRom() => ValidateSeedShooterGameplayRom(false);

    private void ValidateSeedShooterCapacityRom() => ValidateSeedShooterGameplayRom(true);

    private void ValidateSeedShooterEyeRom() => ValidateSeedShooterGameplayRom(false, eyes: true);

    private void ValidateSeedShooterAmmoRom() => ValidateSeedShooterGameplayRom(false, ammunition: true);

    private void ValidateSeedShooterGalePegasusRom() => ValidateSeedShooterGameplayRom(false, otherSeeds: true);

    private void ValidateSeedShooterExclusiveAndClearRom() => ValidateSeedShooterGameplayRom(false, exclusive: true);

    private void ValidateSeedShooterReflectorRom() => ValidateSeedShooterGameplayRom(false, reflectors: true);

    private void ValidateRaftSeedShooterRom() => ValidateSeedShooterGameplayRom(false, raft: true);

    private void ValidateMinecartSeedShooterRom() => ValidateSeedShooterGameplayRom(false, minecart: true);

    private void ValidateSeedShooterAirborneRom() => ValidateSeedShooterGameplayRom(false, airborne: true);

    private void ValidateSeedShooterGameplayRom(bool capacity, bool eyes = false, bool ammunition = false, bool otherSeeds = false, bool exclusive = false, bool reflectors = false, bool raft = false, bool minecart = false, bool airborne = false)
    {
        int airborneReleases = 0;
        int hostCase1 = 0;
        foreach (bool primary in new[] { false, true })
        foreach (int initialAngle in eyes || exclusive ? new[] { 0 } : capacity || ammunition ? new[] { 0, 2 } : Enumerable.Range(0, 8))
        foreach (int seedItem in airborne || minecart || raft || exclusive || reflectors ? new[] { 0x20, 0x21, 0x22, 0x23, 0x24 } : otherSeeds ? new[] { 0x22, 0x23 } : capacity ? new[] { 0x20 } : new[] { 0x20, 0x21, 0x24 })
        foreach (bool walls in eyes || exclusive ? new[] { true } : airborne || minecart || raft || capacity || ammunition || reflectors ? new[] { false } : new[] { false, true })
        foreach (int initialCount in ammunition ? new[] { 0, 1 } : new[] { 0x20 })
        foreach (int cartDirection in minecart ? Enumerable.Range(0, 4) : new[] { 0 })
        foreach (int jumpUpdates in airborne ? new[] { 1, 15, 29 } : new[] { 0 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            // Aim/seed have separate dispatch regressions. Ride/jump
            // interactions retain each value through orthogonal cases.
            bool canonical = initialAngle == 0 && seedItem == 0x20 && cartDirection == 0 &&
                jumpUpdates == (airborne ? 1 : 0);
            if (minecart && cartDirection != (initialAngle / 2 + seedItem - 0x20) % 4) continue;
            if (airborne && !canonical && initialAngle % 4 != (seedItem - 0x20 + jumpUpdates / 14) % 4) continue;
            ReinitializeGameplayForValidation();
            if (raft) _saveData.SetGlobalFlag(0x26);
            LoadValidationRoom(raft ? 1 : minecart || eyes || reflectors ? 4 : 0, raft ? 0xa7 : minecart ? 0x00 : eyes ? 0xba : reflectors ? 0xa6 : 0x33); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Shooter, 1);
            if (airborne) _inventory.GiveTreasure(TreasureId.Feather, 1);
            _inventory.GiveTreasure(seedItem, initialCount);
            if (exclusive)
                _inventory.GiveTreasure(0x20 + (seedItem - 0x20 + 1) % 5, 0x20);
            _inventory.SelectShooterSeeds(seedItem - 0x20);
            _inventory.EquipA(primary ? TreasureId.Shooter : airborne ? TreasureId.Feather : 0);
            _inventory.EquipB(primary ? airborne ? TreasureId.Feather : 0 : TreasureId.Shooter);
            for (int y = 0; y < (reflectors ? _currentRoom.HeightInTiles : 8); y++)
            for (int x = 0; x < (reflectors ? _currentRoom.WidthInTiles : 10); x++)
            {
                bool wall = walls && (x is 2 or 7 || y is 1 or 6);
                bool water = raft && y is >= 3 and <= 6 && x is > 0 and < 9;
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8),
                    wall ? (byte)0x2e : water ? (byte)0xfc : (byte)0xa0,
                    wall ? (byte)0x0f : water ? (byte)0x10 : (byte)0, 0);
            }
            RaftRoomEntity? raftEntity = null;
            if (raft)
            {
                raftEntity = new RaftRoomEntity(new RaftSpawn(new(80, 56), 0, 1, 0xa7),
                    _currentRoom, new RaftDatabase().Behavior, _runtimeState);
                _entities.AddEntity(raftEntity);
            }
            MinecartRoomEntity? cart = null;
            Vector2I[] cartDirections = [Vector2I.Up, Vector2I.Right, Vector2I.Down, Vector2I.Left];
            if (minecart)
            {
                Vector2 center = new(88, 72);
                byte track = (byte)(cartDirection % 2 == 0 ? 0x5e : 0x5d);
                _currentRoom.SetPositionTileAndCollision(center, track, 0, 0);
                _currentRoom.SetPositionTileAndCollision(center + (Vector2)cartDirections[cartDirection] * 16, track, 0, 0);
                _currentRoom.SetPositionTileAndCollision(center - (Vector2)cartDirections[cartDirection] * 16, 0x5f, 0, 0);
                MinecartRuntimeState.Reset(_runtimeState, []);
                MinecartRuntimeState.BeginRide(_runtimeState, 0, 0x00, center, cartDirection);
                cart = new MinecartRoomEntity(new ActiveMinecart(-1, 0x00, 72, 88, cartDirection, true),
                    _currentRoom, new DungeonInteractionDatabase(), _runtimeState,
                    new DungeonInteractionVisualDatabase().Visual("minecart"), _sound.PlaySound);
                _entities.AddEntity(cart);
            }
            SeedShooterEyeStatueRoomEntity? eye = null;
            if (eyes)
            {
                var data = new SeedShooterEyeStatueDatabase();
                var record = data.GetRoomRecords(4, 0xba).Single(row => row.PackedPosition == 0x37);
                eye = new(record, data, new DungeonInteractionVisualDatabase().Visual("seed-shooter-eye-statue"), _entities.SetTrigger);
                _entities.AddEntity(eye);
            }
            RotatableSeedThingRoomEntity? reflector = null;
            if (reflectors)
            {
                var data = new DungeonMechanicDatabase();
                var record = data.GetRoomRecords(4, 0xa6).Single(row => row.Id == 0x33 && row.SubId == 0x08);
                reflector = new(record, data, new DungeonInteractionVisualDatabase().Visual("rotatable-seed-thing"),
                    _currentRoom, _runtimeState, () => _entities.FrameCounter, _entities.TryCreateSeedReflectorChild);
                _entities.AddEntity(reflector);
            }
            Vector2 linkPosition = new(80.25f, raft ? 40.5f : eyes ? 51.5f : 64.5f);
            if (minecart) linkPosition = new(88.25f, 72.5f);
            if (reflectors)
            {
                // seeds.s @shooterPositionOffsets, independently of the
                // generated offsets consumed by production item creation.
                Vector2[] offsets = [new(-4, -14), new(11, -4), new(12, 5), new(11, 9),
                    new(3, 13), new(-8, 10), new(-13, 5), new(-8, -8)];
                int releasedAngle = initialAngle;
                Vector2 approach = OracleObjectMovement.Shared.Direction(releasedAngle * 4);
                Vector2 separation = new(System.Math.Sign(approach.X), System.Math.Sign(approach.Y));
                separation *= (releasedAngle & 1) == 0 ? 14 : 10;
                linkPosition = new Vector2(104.25f, 40.5f) - separation - offsets[releasedAngle];
            }
            _player.WarpTo(linkPosition); _player.Face(Vector2I.Up);
            if (minecart)
            {
                _player.Face(cartDirections[cartDirection]);
                _player.FinishMinecartMount(new(88, 72), cartDirection, 0);
            }
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, minecart ? cartDirection : 0, (int)linkPosition.X, (int)linkPosition.Y)
                { HostilePartsEnabled = eyes || reflectors, CompanionDispatchEnabled = raft || minecart };
            rom[0xd00a] = 0x80; rom[0xd00c] = 0x40; rom.InitializeLinkGameplay();
            if (raft)
            {
                rom[0xd004] = 0; rom[0xd009] = 0;
                rom[0xd240] = 1; rom[0xd241] = 0xe6; rom[0xd242] = 1;
                rom[0xd24b] = 56; rom[0xd24d] = 80;
                rom.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
                FailIf(rom[0xd244] != 1, "Raft Shooter fixture must initialize native INTERAC$e6:$01 placement.");
            }
            if (minecart)
            {
                rom[0xd004] = 0; rom[0xd009] = 0;
                rom[0xd100] = 3; rom[0xd101] = 0x0a;
                rom[0xd108] = (byte)cartDirection; rom[0xd109] = (byte)(cartDirection * 8);
                rom[0xd10b] = 72; rom[0xd10d] = 88;
                rom.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
            }
            if (eyes)
            {
                // Crown $4:$ba's eye at packed $37 is PART $46, subid $00.
                rom[0xd0c0] = 1; rom[0xd0c1] = 0x46; rom[0xd0c2] = 0;
                rom[0xd0cb] = 56; rom[0xd0cd] = 120;
            }
            if (reflectors)
            {
                // Crown $4:$a6's first parent is PART$33:$08 at packed $26.
                rom[0xd0c0] = 1; rom[0xd0c1] = 0x33; rom[0xd0c2] = 0x08;
                rom[0xd0cb] = 40; rom[0xd0cd] = 104;
            }
            var sounds = _sound.AttachPlayRequestAudit();
            int button = primary ? 1 : 2, update = 0, hits = 0;
            if (capacity)
            {
                var bombs = new BombDatabase().Data;
                for (int index = 0; index < 5; index++)
                {
                    _entities.Spawn<BombEffect>(new BombSpawn(_player, bombs, 0, _ => { }));
                    int slot = 0xd700 + index * 0x100;
                    rom[slot] = 1; rom[slot + 1] = 3;
                    rom[slot + 0x0b] = 64; rom[slot + 0x0d] = 80;
                }
            }
            void Step(int count = 1, bool held = false, bool press = false, int angle = 0xff, bool directionEdge = false, bool feather = false)
            {
                Vector2 movement = angle == 0xff ? Vector2.Zero : OracleObjectMovement.Shared.Direction(angle);
                int directions = (movement.X > 0 ? 0x10 : movement.X < 0 ? 0x20 : 0) |
                    (movement.Y > 0 ? 0x80 : movement.Y < 0 ? 0x40 : 0);
                int itemsHeld = (held ? button : 0) | (feather ? primary ? 2 : 1 : 0);
                int edge = (press ? button : 0) | (feather ? primary ? 2 : 1 : 0) | (directionEdge ? directions : 0);
                StepGameplayUpdates(count, movement, MenuRomActions(itemsHeld | directions), MenuRomActions(edge), batched, () =>
                {
                    rom.UpdateGameplay(edge, itemsHeld | directions, angle, _entities.FrameCounter - (raft || minecart ? 1 : 0)); edge = 0;
                    string context = $"Shooter ITEM${seedItem:x2} A={primary} angle={initialAngle} walls={walls} raft={raft} minecart={minecart}/{cartDirection} jumpUpdates={jumpUpdates} update={++update}";
                    int parent = Enumerable.Range(0xd2, 4).Select(page => page << 8)
                        .SingleOrDefault(slot => rom[slot] != 0 && rom[slot + 1] == 0x0f);
                    FailIf(_player.IsUsingSeedShooter != (parent != 0) ||
                        (rom[0xd600] != 0 && rom[0xd601] == 0x0f) != _player.IsUsingSeedShooter,
                        context + ": parent/reserved weapon lifecycle differs.");
                    if (eye is not null)
                    {
                        FailIf(eye.Counter != rom[0xd0c6] || eye.Invincibility != unchecked((sbyte)rom[0xd0eb]) ||
                            eye.PendingHit != ((rom[0xd0ea] & 0x80) != 0) ||
                            eye.Visible != ((rom[0xd0da] & 0x80) != 0) || _entities.ActiveTriggers != rom[0xcca0],
                            context + $": PART$46 hit/lockout/activation differs: runtime={eye.Counter}/{eye.Invincibility}/{eye.PendingHit}/{eye.Visible}/{_entities.ActiveTriggers:x2}, native={rom[0xd0c6]}/{unchecked((sbyte)rom[0xd0eb])}/{rom[0xd0ea]:x2}/{rom[0xd0da]:x2}/{rom[0xcca0]:x2}.");
                        if (eye.PendingHit) hits++;
                    }
                    if (reflector is not null)
                    {
                        FailIf(reflector.Counter != rom[0xd0c6] || reflector.Orientation != rom[0xd0c8] ||
                            reflector.CollisionRadii != new Vector2(rom[0xd0e7], rom[0xd0e6]) ||
                            reflector.SeedBounceOrientation != rom[0xd0e1],
                            context + ": PART$33 parent timer/orientation/radii/animation parameter differs.");
                        var child = _entities.Entities<SeedReflectorChildRoomEntity>().Single();
                        FailIf(child.Position != new Vector2(rom.Word(0xd1cc) / 256f, rom.Word(0xd1ca) / 256f) ||
                            child.CollisionRadii != new Vector2(rom[0xd1e7], rom[0xd1e6]) ||
                            child.SeedBounceOrientation != rom[0xd1e1] || child.Visible || rom[0xd1cf] != 0xf2,
                            context + $": PART$33:$03 child runtime={child.Position}/{child.CollisionRadii}/{child.SeedBounceOrientation}, native={rom.Word(0xd1cc) / 256f},{rom.Word(0xd1ca) / 256f}/{rom[0xd1e7]},{rom[0xd1e6]}/{rom[0xd1e1]}/z{rom[0xd1cf]:x2}, id={rom[0xd1c1]:x2}/{rom[0xd1c2]:x2}.");
                    }
                    if (parent != 0)
                        FailIf(_seedSatchel.ShooterAngle != rom[parent + 9] ||
                            (rom[parent + 4] == 1 ? _seedSatchel.ShooterAimCounter : _seedSatchel.ShooterPostShotCounter) != rom[parent + 7],
                            context + ": eight-way aim counter/angle or post-shot counter differs.");
                    FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256.0f, rom.Word(0xd00a) / 256.0f) ||
                        CarriedObjectMotion.DirectionIndex(_player.FacingVector) != rom[0xd008],
                        context + ": Link movement/facing differs.");
                    if (airborne)
                    {
                        FailIf(_player.TopDownAirborne != (rom[0xcc5c] != 0) ||
                            (_player.ItemCreationZFixed & 0xffff) != rom.Word(0xd00e) ||
                            (_player.TopDownAirSpeedZ & 0xffff) != rom.Word(0xd014),
                            context + $": Link gravity/landing differs: runtime=air{_player.TopDownAirborne}/Z${_player.ItemCreationZFixed & 0xffff:x4}/Vz${_player.TopDownAirSpeedZ & 0xffff:x4}, native=air${rom[0xcc5c]:x2}/Z${rom.Word(0xd00e):x4}/Vz${rom.Word(0xd014):x4}.");
                        FailIf(!sounds.Requests.Where(id => id != SoundId.SndText).SequenceEqual(rom.Sounds),
                            context + ": full Feather/Shooter sound order differs.");
                    }
                    if (raftEntity is not null)
                    {
                        FailIf(_player.RaftRideActive != (rom[0xcc2c] == 0xd1) || _player.TopDownSwimming || _player.IsDrowning,
                            context + ": raft ownership/swimming/hazard differs.");
                        if (rom[0xd100] != 0 && rom[0xd104] != 0)
                            FailIf(raftEntity.PrecisePosition != new Vector2(rom.Word(0xd10c) / 256.0f, rom.Word(0xd10a) / 256.0f) ||
                                raftEntity.Angle != rom[0xd109] || raftEntity.Direction != rom[0xd108],
                                context + ": fixed raft motion/angle/facing differs.");
                        FailIf(!sounds.Requests.Where(id => id != SoundId.SndText).SequenceEqual(rom.Sounds),
                            context + ": full raft/seed sound order differs.");
                    }
                    if (minecart)
                    {
                        FailIf(_player.MinecartRideActive != (rom[0xcc2c] == 0xd1) ||
                            _player.TopDownAirborne != (rom[0xcc5c] != 0) ||
                            (_player.SwitchHookZFixed & 0xffff) != rom.Word(0xd00e),
                            context + ": cart rider/air/Z differs.");
                        for (int address = 0xcd80; address < 0xcdc0; address++)
                            FailIf(_runtimeState.ReadWramByte(address) != rom[address], context + $": static byte ${address:x4} differs.");
                        FailIf(!sounds.Requests.Where(id => id != SoundId.SndText).SequenceEqual(rom.Sounds),
                            context + ": full cart/seed sound order differs.");
                    }
                    var items = _entities.Entities<EmberSeedEffect>();
                    int[] children = Enumerable.Range(0xd7, 5).Select(page => page << 8)
                        .Where(slot => rom[slot] != 0 && rom[slot + 1] == seedItem).ToArray();
                    FailIf(items.Count != children.Length, context + ": shot allocation/deletion differs.");
                    FailIf(_entities.HasActiveShooterSeed != (rom[0xccda] != 0),
                        context + ": shared Shooter child-in-use signal differs.");
                    for (int index = 0; index < items.Count; index++)
                    {
                        var item = items[index]; int slot = children[index];
                        if (reflectors && (rom[slot + 0x2a] & 0x10) != 0) hits++;
                        int state = item.State == EmberState.Flying ? 1 : item.State == EmberState.Scent ? 2 : 3;
                        FailIf(state != rom[slot + 4] || item.PrecisePosition != new Vector2(rom.Word(slot + 0x0c) / 256.0f, rom.Word(slot + 0x0a) / 256.0f) ||
                            (item.ZFixed & 0xffff) != rom.Word(slot + 0x0e) || (item.SpeedZ & 0xffff) != rom.Word(slot + 0x14) ||
                            item.Angle * 4 != rom[slot + 9] || item.Visible != ((rom[slot + 0x1a] & 0x80) != 0) ||
                            item.CollisionEnabled != ((rom[slot + 0x24] & 0x80) != 0) ||
                            item.BouncesRemaining != rom[slot + 0x34] || item.ShooterElevation != rom[slot + 0x3e],
                            context + $": shot state/fixed XY/Z/angle/bounce/collision differs: runtime={state}/{item.PrecisePosition}/z{item.ZFixed}/vz{item.SpeedZ}/a{item.Angle * 4}/b{item.BouncesRemaining}/e{item.ShooterElevation}/visible{item.Visible}/collision{item.CollisionEnabled}, native={rom[slot + 4]}/{rom.Word(slot + 0x0c) / 256.0f},{rom.Word(slot + 0x0a) / 256.0f}/z{rom.Word(slot + 0x0e)}/vz{rom.Word(slot + 0x14)}/a{rom[slot + 9]}/b{rom[slot + 0x34]}/e{rom[slot + 0x3e]}/visible{(rom[slot + 0x1a] & 0x80) != 0}/collision{(rom[slot + 0x24] & 0x80) != 0}.");
                        int animationCounter = (int)typeof(EmberSeedEffect).GetField("_frameCounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(item)!;
                        FailIf(animationCounter != rom[slot + 0x20] || state > 1 && item.FlameCounter != rom[slot + 6] ||
                            seedItem == 0x24 && item.MysteryEffect != rom[slot + 3],
                            context + ": shot animation/effect counter or mystery selection differs.");
                        if (seedItem == 0x23 && state > 1)
                            FailIf(item.GaleSubstate != rom[slot + 5] || item.GaleCounter2 != rom[slot + 7] || item.GalePalette != rom[slot + 0x1c],
                                context + ": Gale state/counter/palette differs.");
                    }
                    for (int address = 0xc6b9; address <= 0xc6bd; address++)
                        FailIf(_saveData.ReadWramByte(address) != rom[address], context + $": seed byte ${address:x4} differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls,
                        context + ": shot RNG differs.");
                    FailIf(!sounds.Requests.Where(id => id is 0x52 or 0x72 or 0x85 or 0x7b or 0x90 or 0xd0 or 0xcb)
                        .SequenceEqual(rom.Sounds.Where(id => id is 0x52 or 0x72 or 0x85 or 0x7b or 0x90 or 0xd0 or 0xcb)),
                        context + ": shot/bounce/effect sounds differ.");
                });
            }
            if (raftEntity is not null)
            {
                for (int approach = 0; !raftEntity.LinkRiding && approach < 32; approach++) Step(angle: 0x10);
                FailIf(!raftEntity.LinkRiding || rom[0xcc2c] != 0xd1,
                    "Raft Shooter must reach and mount from actual dry-shore geometry.");
                Step(24);
            }
            if (capacity)
            {
                Step(1, true, true, initialAngle * 4);
                Step();
                Step(12);
                foreach (var bomb in _entities.Entities<BombEffect>()) bomb.Discard();
                for (int slot = 0xd700; slot <= 0xdb00; slot += 0x100) rom.DeleteDynamicItem(slot);
                Step(2);
            }
            if (ammunition)
            {
                Step(1, true, true, initialAngle * 4);
                Step(4, true);
                if (initialCount == 0)
                {
                    FailIf(_player.IsUsingSeedShooter, "Empty Shooter entered aiming.");
                    Step();
                    _inventory.GiveTreasure(seedItem, 1);
                    rom[0xc6b9 + seedItem - 0x20] = 1;
                    Step(1, true, true, initialAngle * 4);
                    Step(4, true);
                }
                // Change the authoritative ammo owner while aiming; state 1
                // must recheck clearSelfIfNoSeeds before angle/button handling.
                FailIf(!_inventory.TryConsumeSelectedShooterSeed(out int consumed) || consumed != seedItem,
                    "Shooter ammo fixture could not consume its remaining seed.");
                rom[0xc6b9 + seedItem - 0x20] = 0;
                Step(1, true);
                FailIf(_player.IsUsingSeedShooter || _entities.Entities<EmberSeedEffect>().Count != 0,
                    "Shooter did not cancel aim when selected ammo became empty.");
                Step();
                _inventory.GiveTreasure(seedItem, 0x20);
                rom[0xc6b9 + seedItem - 0x20] = 0x20;
            }
            for (int repeat = 0; repeat < 2; repeat++)
            {
                int hitsBefore = hits;
                if (airborne)
                {
                    Step(jumpUpdates, feather: true);
                    FailIf(!_player.TopDownAirborne || rom[0xcc5c] == 0,
                        "Airborne Shooter must launch through the opposite equipped Feather before aiming.");
                }
                Step(1, true, true, initialAngle * 4);
                if (airborne)
                    FailIf(!_player.IsUsingSeedShooter, "Ordinary Feather air state must permit fresh Shooter aiming.");
                if (raftEntity is not null)
                    FailIf(!_player.IsUsingSeedShooter, "Raft must permit a fresh Shooter aiming parent.");
                if (cart is not null && repeat == 0)
                {
                    Step(32, true, angle: initialAngle * 4);
                    FailIf(!cart.Dismounting || !_player.IsUsingSeedShooter,
                        "Cart must reach its platform while retaining the held aiming parent across dismount.");
                }
                Step(4, true, angle: (reflectors ? initialAngle : (initialAngle + 2) & 7) * 4);
                _dialogue.ShowMessage("Shooter aim pause.", _player.Position.Y); rom[0xcba0] = 1;
                Step(6, true);
                _dialogue.Close(); rom[0xcba0] = 0;
                Step(12, true, angle: (reflectors ? initialAngle : (initialAngle + 2) & 7) * 4);
                Step(1, true, angle: (reflectors ? initialAngle : (initialAngle + 4) & 7) * 4, directionEdge: true);
                Step(); // Release creates the selected projectile.
                FailIf(_seedSatchel.ShooterPostShotCounter != 12, "Shooter release did not begin its twelve-update post-shot wait.");
                if (cart is not null && repeat == 0)
                {
                    FailIf(!_player.MinecartJumpActive || _entities.Entities<EmberSeedEffect>().Count != 1,
                        "Retained minecart Shooter parent must create its physical child before dismount landing.");
                    airborneReleases++;
                }
                if (airborne && jumpUpdates == 1)
                {
                    FailIf(!_player.TopDownAirborne || _entities.Entities<EmberSeedEffect>().Count != 1,
                        "Early Feather aiming must release a physical Shooter child before Link lands.");
                    airborneReleases++;
                }
                Step(3);
                _dialogue.ShowMessage("Shooter flight pause.", _player.Position.Y); rom[0xcba0] = 1;
                Step(6);
                _dialogue.Close(); rom[0xcba0] = 0;
                if (exclusive && repeat == 0)
                {
                    Step(9);
                    FailIf(_player.IsUsingSeedShooter || _entities.Entities<EmberSeedEffect>().Count != 1,
                        "Shooter exclusivity fixture did not retain a child after retiring its parent.");
                    int selected = (seedItem - 0x20 + 1) % 5;
                    _inventory.SelectShooterSeeds(selected); rom[0xc6c5] = (byte)selected;
                    Step(1, true, true, initialAngle * 4);
                    Step();
                    FailIf(_player.IsUsingSeedShooter || _saveData.ReadWramByte(0xc6b9 + selected) != 0x20,
                        "A different selected seed bypassed the shared Shooter cap or consumed ammo.");
                    _player.ClearNativeItemParents();
                    _entities.ClearPhysicalPlayerItems();
                    rom.ClearPhysicalItems();
                    _inventory.SelectShooterSeeds(seedItem - 0x20); rom[0xc6c5] = (byte)(seedItem - 0x20);
                    Step(3);
                }
                int began = update;
                while ((_player.IsUsingSeedShooter || _entities.Entities<EmberSeedEffect>().Count != 0) && update - began < 180) Step(3);
                FailIf(_player.IsUsingSeedShooter || _entities.Entities<EmberSeedEffect>().Count != 0,
                    "Shooter did not retire its parent and bounded projectile/effect.");
                FailIf(airborne && _player.TopDownAirborne, "Shooter completion must not leave its Feather jump frozen in the air.");
                FailIf(eyes && hits - hitsBefore < (seedItem == 0x20 ? 2 : 1),
                    "PART$46 fixture did not exercise contact and the Ember flame's later reactivation.");
                FailIf(reflectors && repeat == 0 && hits == 0,
                    $"PART$33 ITEM${seedItem:x2} angle={initialAngle} Link={linkPosition}: fixture did not contact or reflect the first Shooter seed through its actual collision geometry.");
                Step(eyes ? 48 : 4);
                FailIf(eye is not null && (eye.Visible || eye.Counter != 0 || _entities.ActiveTriggers != 0),
                    "PART$46 did not expire after the final seed contact.");
            }
        }
        FailIf((minecart || airborne) && airborneReleases == 0, "Shooter comparison did not release any physical children while airborne.");
        GD.Print($"Validated clean-US Shooter gameplay: capacity={capacity}, eye={eyes}, ammo={ammunition}, Gale/Pegasus={otherSeeds}, exclusivity/clear={exclusive}, PART$33 reflector={reflectors}, raft={raft}, minecart={minecart}, Feather airborne={airborne}, airborne releases={airborneReleases}; native per-update parent/child position/counters/collision, ammo/RNG/sounds, dialogue and repeat through split/batched updates.");
    }
}
