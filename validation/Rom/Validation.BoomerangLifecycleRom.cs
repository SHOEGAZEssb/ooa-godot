using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateBoomerangLifecycleRom() => ValidateBoomerangGameplayRom(0);

    private void ValidateBoomerangTerrainRom()
    {
        // Independent source cases: ordinary solid $2e, overworld passable
        // $fd, and cliff keys $64/$0a/$0b with signed elevation changes.
        for (int terrain = 1; terrain <= 5; terrain++) ValidateBoomerangGameplayRom(terrain);
    }

    private void ValidateBoomerangAllocationRom() => ValidateBoomerangGameplayRom(6);
    private void ValidateBoomerangAirborneRom() => ValidateBoomerangGameplayRom(0, airborne: true);
    private void ValidateRaftBoomerangRom() => ValidateBoomerangGameplayRom(0, raft: true);
    private void ValidateMinecartBoomerangRom() => ValidateBoomerangGameplayRom(0, minecart: true);

    private void ValidateBoomerangGameplayRom(int terrain, bool airborne = false, bool raft = false, bool minecart = false)
    {
        static T Private<T>(object owner, string field) => (T)owner.GetType().GetField(field,
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
        int hostCase1 = 0;
        foreach (bool primary in new[] { false, true })
        foreach (int ring in terrain == 0 && !raft && !minecart ? new[] { 0xff, (int)RingId.RangL1, (int)RingId.RangL2 } : new[] { 0xff })
        foreach (int angle in Enumerable.Range(0, 8).Select(index => index * 4))
        foreach (int jumpUpdates in airborne ? new[] { 1, 15, 29 } : new[] { 0 })
        foreach (int cartDirection in minecart ? Enumerable.Range(0, 4) : new[] { 0 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            bool canonical = angle == 0 && ring == 0xff && cartDirection == 0 &&
                jumpUpdates == (airborne ? 1 : 0);
            if (minecart && cartDirection != angle / 8) continue;
            if (airborne && !canonical && angle / 4 % 4 != jumpUpdates / 14) continue;
            ReinitializeGameplayForValidation();
            if (raft) _saveData.SetGlobalFlag(0x26);
            LoadValidationRoom(raft ? 1 : minecart ? 4 : 0, raft ? 0xa7 : minecart ? 0x00 : 0x33); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Boomerang, 0);
            if (airborne) _inventory.GiveTreasure(TreasureId.Feather, 1);
            _inventory.EquipA(primary ? TreasureId.Boomerang : airborne ? TreasureId.Feather : 0);
            _inventory.EquipB(primary ? airborne ? TreasureId.Feather : 0 : TreasureId.Boomerang);
            if (ring != 0xff)
            {
                _inventory.GiveTreasure(TreasureId.RingBox, 1);
                _inventory.GrantAppraisedRingForDebug(ring);
                FailIf(!_inventory.SetRingBoxSlotFromList(0, ring) || !_inventory.EquipRingAt(0),
                    $"Could not equip Boomerang ROM ring ${ring:x2}.");
            }
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
            {
                bool water = raft && y is >= 3 and <= 6 && x is > 0 and < 9;
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8),
                    water ? (byte)0xfc : (byte)0xa0, water ? (byte)0x10 : (byte)0, 0);
            }
            if (terrain is >= 1 and <= 5)
            {
                int tile = terrain switch { 1 => 0x2e, 2 => 0xfd, 3 => 0x64, 4 => 0x0a, _ => 0x0b };
                for (int y = 0; y < 8; y++)
                for (int x = 0; x < 10; x++)
                    if (x is 2 or 7 || y is 1 or 6)
                        _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), (byte)tile, 0x0f, 0);
            }
            RaftRoomEntity? raftEntity = null;
            if (raft)
            {
                raftEntity = new RaftRoomEntity(new RaftSpawn(new(80, 56), 0, 1, 0xa7),
                    _currentRoom, new RaftDatabase().Behavior, _runtimeState);
                _entities.AddEntity(raftEntity);
            }
            Vector2I[] cartDirections = [Vector2I.Up, Vector2I.Right, Vector2I.Down, Vector2I.Left];
            MinecartRoomEntity? cart = null;
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
            Vector2 linkPosition = minecart ? new(88.25f, 72.5f) : raft ? new(80.25f, 40.5f) :
                airborne ? new(80.25f, 64.5f) : new(80, 64);
            _player.WarpTo(linkPosition); _player.Face(minecart ? cartDirections[cartDirection] : Vector2I.Up);
            if (minecart) _player.FinishMinecartMount(new(88, 72), cartDirection, 0);
            var initialRandom = _random.CaptureState();
            var rom = new SomariaRom(_saveData, initialRandom, _currentRoom, minecart ? cartDirection : 0,
                (int)linkPosition.X, (int)linkPosition.Y) { CompanionDispatchEnabled = raft || minecart };
            if (airborne || raft || minecart) { rom[0xd00a] = 0x80; rom[0xd00c] = 0x40; }
            rom.InitializeLinkGameplay();
            if (raft || minecart)
            {
                rom[0xd004] = 0; rom[0xd009] = 0;
                if (raft)
                {
                    rom[0xd240] = 1; rom[0xd241] = 0xe6; rom[0xd242] = 1;
                    rom[0xd24b] = 56; rom[0xd24d] = 80;
                }
                else
                {
                    rom[0xd100] = 3; rom[0xd101] = 0x0a;
                    rom[0xd108] = (byte)cartDirection; rom[0xd109] = (byte)(cartDirection * 8);
                    rom[0xd10b] = 72; rom[0xd10d] = 88;
                }
                rom.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
            }
            var sounds = _sound.AttachPlayRequestAudit();
            int button = primary ? 1 : 2;
            int update = 0;
            void Step(int count = 1, int held = 0, int pressed = 0, int inputAngle = 0xff)
            {
                Vector2 movement = inputAngle == 0xff ? Vector2.Zero : OracleObjectMovement.Shared.Direction(inputAngle);
                int directions = (movement.X > 0 ? 0x10 : movement.X < 0 ? 0x20 : 0) |
                    (movement.Y > 0 ? 0x80 : movement.Y < 0 ? 0x40 : 0);
                int edge = pressed;
                StepGameplayUpdates(count, movement, MenuRomActions(held | directions), MenuRomActions(pressed), batched, () =>
                {
                    rom.UpdateGameplay(edge, held | directions, inputAngle, _entities.FrameCounter - (raft || minecart ? 1 : 0)); edge = 0;
                    string context = $"Boomerang terrain={terrain} A={primary} ring=${ring:x2} angle=${angle:x2} jumpUpdates={jumpUpdates} raft={raft} cart={minecart}/{cartDirection} update={++update}";
                    FailIf(_player.NativeItemUseActive != (rom[0xcc5f] != 0),context + ": wLinkUsingItem1 must follow the live animation parent through return and repeat use.");
                    int[] nativeItems = Enumerable.Range(0xd7, 5).Select(page => page << 8)
                        .Where(slot => rom[slot] != 0 && rom[slot + 1] == 6).ToArray();
                    var items = _entities.Entities<BoomerangItem>();
                    FailIf(items.Count != nativeItems.Length, context + ": ITEM$06 allocation/deletion differs.");
                    if (items.Count == 1)
                    {
                        BoomerangItem item = items[0]; int slot = nativeItems[0];
                        // Ages ITEM$06 starts with damage $fe; the two Rang
                        // branches add signed -1/$0d or -2/$29 in state00.
                        int expectedDamage = ring == 0x0d ? 3 : ring == 0x29 ? 4 : 2;
                        FailIf(item.Damage != expectedDamage || -(sbyte)rom[slot + 0x28] != expectedDamage,
                            context + ": base/Rang Ring damage differs from the native attribute and ring branches.");
                        var passage = Private<ItemTilePassage>(item, "_tilePassage");
                        FailIf(Private<int>(passage, "_elevation") != rom[slot + 0x3e],
                            context + ": signed cliff elevation accumulation differs.");
                        FailIf(item.State != rom[slot + 4] || item.Counter != rom[slot + 6] ||
                            item.Angle != rom[slot + 9] || item.ZHigh != unchecked((sbyte)rom[slot + 0x0f]) ||
                            item.PrecisePosition != new Vector2(rom.Word(slot + 0x0c) / 256.0f, rom.Word(slot + 0x0a) / 256.0f) ||
                            item.CollisionEnabled != ((rom[slot + 0x24] & 0x80) != 0) ||
                            item.Visible != ((rom[slot + 0x1a] & 0x80) != 0),
                            context + $": runtime state/count/angle/XY={item.State}/{item.Counter}/${item.Angle:x2}/{item.PrecisePosition}, native={rom[slot + 4]}/{rom[slot + 6]}/${rom[slot + 9]:x2}/{rom.Word(slot + 0x0c) / 256.0f},{rom.Word(slot + 0x0a) / 256.0f}.");
                    }
                    int parent = rom[0xd300] != 0 && rom[0xd301] == 6 ? 0xd300 : rom[0xd400] != 0 && rom[0xd401] == 6 ? 0xd400 : 0;
                    FailIf(_player.IsUsingBoomerang != (parent != 0) ||
                        parent != 0 && (_entities.BoomerangParent.Counter != rom[parent + 0x20] ||
                            _entities.BoomerangParent.Parameter != rom[parent + 0x21]),
                        context + ": parent throw pose counter/parameter/lifetime differs.");
                    FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256.0f, rom.Word(0xd00a) / 256.0f) ||
                        CarriedObjectMotion.DirectionIndex(_player.FacingVector) != rom[0xd008] ||
                        !sounds.Requests.Where(id => id is SoundId.SndBoomerang or SoundId.SndClink)
                            .SequenceEqual(rom.Sounds.Where(id => id is SoundId.SndBoomerang or SoundId.SndClink)),
                        context + $": Link movement lock/facing/sound differs: runtime={_player.PrecisePosition}/dir{CarriedObjectMotion.DirectionIndex(_player.FacingVector)}, native={rom.Word(0xd00c) / 256f},{rom.Word(0xd00a) / 256f}/dir{rom[0xd008]}.");
                    if (airborne || raft || minecart)
                    {
                        FailIf(_player.TopDownAirborne != (rom[0xcc5c] != 0) ||
                            (_player.ItemCreationZFixed & 0xffff) != rom.Word(0xd00e) ||
                            (_player.TopDownAirSpeedZ & 0xffff) != rom.Word(0xd014),
                            context + ": Link full Z/gravity/landing differs.");
                        FailIf(!sounds.Requests.Where(id => id != SoundId.SndText).SequenceEqual(rom.Sounds),
                            context + ": Feather/Boomerang sound order differs.");
                        var random = _random.CaptureState();
                        FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                            random.Calls - initialRandom.Calls != rom.RandomCalls,
                            context + ": shared RNG differs.");
                        FailIf(raft && (_player.RaftRideActive != (rom[0xcc2c] == 0xd1) ||
                            _player.TopDownSwimming || _player.IsDrowning), context + ": raft ownership/swimming differs.");
                        if (raftEntity is not null && _player.RaftRideActive)
                            FailIf(Private<Vector2>(raftEntity, "_precisePosition") != new Vector2(rom.Word(0xd10c) / 256f, rom.Word(0xd10a) / 256f),
                                context + ": full fixed raft position differs.");
                        FailIf(minecart && (_player.MinecartRideActive != (rom[0xcc2c] == 0xd1) ||
                            _player.MinecartJumpActive != ((rom[0xcc5c] & 0x80) != 0)),
                            context + ": minecart rider/locked jump ownership differs.");
                        if (minecart)
                            for (int address = 0xcd80; address < 0xcdc0; address++)
                                FailIf(_runtimeState.ReadWramByte(address) != rom[address], context + $": static byte ${address:x4} differs.");
                    }
                });
            }
            if (terrain == 6)
            {
                // Five ordinary native ITEM_BOMB $03 children fill the actual
                // dynamic pool. Failed throws must clear both movement bits
                // and the start signal, then require another button edge.
                var bombs = new BombDatabase().Data;
                for (int index = 0; index < 5; index++)
                {
                    _entities.Spawn<BombEffect>(new BombSpawn(_player, bombs, 0, _ => { }));
                    int slot = 0xd700 + index * 0x100;
                    rom[slot] = 1; rom[slot + 1] = 3;
                    rom[slot + 0x0b] = 64; rom[slot + 0x0d] = 80;
                }
                Step(1, button, button, angle);
                FailIf(_player.IsUsingBoomerang || _player.StartedItemAnimationThisUpdate ||
                    _entities.Entities<BoomerangItem>().Count != 0,
                    "Boomerang full-pool failure retained a parent/use signal or created a child.");
                Step(4, button);
                _entities.ClearPhysicalPlayerItems(); rom.ClearPhysicalItems();
                Step(6, button);
                FailIf(_entities.Entities<BoomerangItem>().Count != 0,
                    "Freeing the full item pool retriggered a held Boomerang without a new edge.");
                Step(4);
            }
            for (int repeat = 0; repeat < 2; repeat++)
            {
                if (raft && repeat == 0)
                {
                    int began = update;
                    while (!_player.RaftRideActive && update - began < 32) Step(1, inputAngle: 16);
                    FailIf(!_player.RaftRideActive, "Raft Boomerang fixture could not approach and mount through its shore.");
                    Step(24);
                }
                // Start just before the cart reaches its dismount boundary,
                // keeping the eight-update throw parent alive across it.
                if (minecart && repeat == 0) Step(30);
                if (airborne)
                {
                    int featherButton = primary ? 2 : 1;
                    Step(jumpUpdates, featherButton, featherButton);
                    FailIf(!_player.TopDownAirborne, "Airborne Boomerang must launch through the opposite equipped Feather button.");
                }
                Step(1, button, button, repeat == 0 ? angle : 0xff);
                FailIf(_entities.Entities<BoomerangItem>().Count != 1, "Boomerang throw did not create its physical child.");
                if (raft || minecart && repeat == 0)
                    FailIf(_entities.BoomerangParent.Mode != (raft ? 0x21 : 0x25),
                        "Vehicle Boomerang parent did not select native raft $21 or minecart $25 throw mode.");
                Step(6);
                FailIf(minecart && repeat == 0 && (!_player.MinecartJumpActive || !_player.IsUsingBoomerang),
                    "Minecart dismount did not preserve the existing Boomerang parent into the locked jump.");
                _dialogue.ShowMessage("Boomerang flight pause.", _player.Position.Y);
                rom[0xcba0] = 1;
                Step(6);
                _dialogue.Close(); rom[0xcba0] = 0;
                Step(6);
                Step(6, inputAngle: 8); // Homing observes Link's moving target.
                int begin = update;
                while (_entities.Entities<BoomerangItem>().Count != 0 && update - begin < 180)
                {
                    // The hidden state04 still occupies the native cap. A
                    // failed throw clears its own use/movement/turning bits.
                    bool repress = terrain != 0 && (update - begin) % 2 == 0;
                    Step(1, repress ? button : 0, repress ? button : 0);
                }
                FailIf(_entities.Entities<BoomerangItem>().Count != 0 || _player.IsUsingBoomerang,
                    "Boomerang failed to catch, delete and retire its throw parent.");
                FailIf(airborne && _player.TopDownAirborne,
                    "Boomerang throw/catch retained the Feather jump after its native landing boundary.");
                Step(4);
                if (repeat == 0 && terrain is 1 or 2)
                    FailIf(sounds.Requests.Count(id => id == SoundId.SndClink) != (terrain == 1 ? 1 : 0),
                        $"Boomerang terrain {terrain} did not distinguish an ordinary wall from passable $fd.");
            }
            Step(1, button, button, angle);
            Step(9); // Eight pose updates, terminal parameter, then state1 clears.
            FailIf(_player.IsUsingBoomerang || _entities.Entities<BoomerangItem>().Count != 1,
                "Boomerang cancellation fixture did not reach its parent-free flight.");
            _entities.ClearPhysicalPlayerItems(); rom.ClearPhysicalItems();
            Step(6);
            Step(1, button, button, angle);
            int restart = update;
            while (_entities.Entities<BoomerangItem>().Count != 0 && update - restart < 180) Step();
            FailIf(_entities.Entities<BoomerangItem>().Count != 0,
                "Boomerang did not complete a repeated throw after native physical-item clearing.");
        }
        GD.Print($"Validated clean-US Boomerang terrain {terrain}, ordinary Feather air throws={airborne}, raft={raft}, minecart={minecart}, A/B parents, eight input angles, full fixed flight/return/moving-target homing/catch delay, visibility, sound, dialogue pause, physical clearing and repeated use through split/batched application updates.");
    }
}
