using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateShovelLifecycleRom() => ValidateShovelLifecycleRom(false);
    private void ValidateShovelTilesRom() => ValidateShovelLifecycleRom(true);
    private void ValidateShovelDropsRom() => ValidateShovelLifecycleRom(false, drops: true);
    private void ValidateShovelClearingRom() => ValidateShovelLifecycleRom(false, clearing: true);
    private void ValidateShovelAirborneRom() => ValidateShovelLifecycleRom(false, airborne: true);

    private void ValidateShovelLifecycleRom(bool allDirtTiles, bool drops = false, bool clearing = false, bool airborne = false)
    {
        int hostCase1 = 0;
        foreach (bool primary in new[] { false, true })
        foreach (int direction in Enumerable.Range(0, 4))
        foreach (int jumpUpdates in airborne ? new[] { 1, 15, 29, -3, -5, -9 } : new[] { 0 })
        foreach (int clearPhase in clearing ? new[] { 3, 4, 6, 8, 22 } : new[] { -1 })
        foreach (bool physical in clearing ? new[] { false, true } : new[] { false })
        foreach (byte tile in clearing || airborne ? new byte[] { 0x01 } : drops ? new byte[] { 0x01, 0xcd } : allDirtTiles
            // breakableTiles.s @overworld: shovel modes $11/$12/$13/$0e.
            ? new[] { 0x01, 0xaf, 0xbf, 0xcb, 0xcc, 0xcd }
                .Concat(Enumerable.Range(0x10, 12)).Concat(Enumerable.Range(0x20, 12))
                .Concat(Enumerable.Range(0x30, 12)).Select(value => (byte)value)
            : new byte[] { 0x01, 0xcb, 0x1c })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            // Each source tile still digs twice. Direction and button are
            // independent dispatch inputs: rotate them across table rows.
            if (allDirtTiles && tile != 0x01 && direction != (tile & 3)) continue;
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x33); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Shovel, 0);
            if (airborne) _inventory.GiveTreasure(TreasureId.Feather, 1);
            _inventory.EquipA(primary ? TreasureId.Shovel : airborne ? TreasureId.Feather : 0);
            _inventory.EquipB(primary ? airborne ? TreasureId.Feather : 0 : TreasureId.Shovel);
            _saveData.WriteWramByte(0xc65f, 0);
            _saveData.WriteWramByte(0xc660, 0);
            _saveData.SetRoomFlag(0, 0x33, 0x80, false);
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), 0x3a, 0, 0);
            Vector2 target = new(72, 72);
            _currentRoom.SetPositionTileAndCollision(target, tile, null, 0);
            Vector2 position = direction switch
            {
                0 => new(72.25f, 80.5f), 1 => new(63.25f, 68.5f),
                2 => new(72.25f, 63.5f), _ => new(80.25f, 68.5f)
            };
            _player.WarpTo(position); _player.Face(new Vector2I((int)OracleObjectMovement.Shared.Direction(direction * 8).X,
                (int)OracleObjectMovement.Shared.Direction(direction * 8).Y));
            int expectedDrop = -1;
            if (drops)
            {
                var probe = new TileBreakRom();
                int selectedSeed;
                for (selectedSeed = 0; selectedSeed < 0x10000; selectedSeed++)
                {
                    probe.Reset(_saveData, selectedSeed);
                    probe.SetTile(0, tile, tile, _currentRoom); probe.Break(6);
                    if (probe[0xd0c0] != 0 && probe[0xd0c2] is 2 or 3)
                    {
                        expectedDrop = probe[0xd0c2];
                        break;
                    }
                }
                FailIf(selectedSeed == 0x10000, $"Shovel tile ${tile:x2} had no native rupee drop seed.");
                _random.RestoreState(_random.CaptureState() with { Rng1 = (byte)selectedSeed, Rng2 = (byte)(selectedSeed >> 8) });
            }
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, direction, (int)position.X, (int)position.Y)
                { HostilePartsEnabled = true };
            rom[0xd00a] = 0x80; rom[0xd00c] = 0x40;
            rom.InitializeLinkGameplay();
            var sounds = _sound.AttachPlayRequestAudit();
            int button = primary ? 1 : 2, update = 0, dropsObserved = 0;
            void Step(int count = 1, bool press = false, int angle = 0xff, bool feather = false, bool held = false)
            {
                Vector2 movement = angle == 0xff ? Vector2.Zero : OracleObjectMovement.Shared.Direction(angle);
                int directions = (movement.X > 0 ? 0x10 : movement.X < 0 ? 0x20 : 0) |
                    (movement.Y > 0 ? 0x80 : movement.Y < 0 ? 0x40 : 0);
                int itemsHeld = (press || held ? button : 0) | (feather ? primary ? 2 : 1 : 0);
                int edge = (press ? button : 0) | (feather ? primary ? 2 : 1 : 0);
                StepGameplayUpdates(count, movement, MenuRomActions(itemsHeld | directions), MenuRomActions(edge), batched, () =>
                {
                    rom.UpdateGameplay(edge, itemsHeld | directions, angle, _entities.FrameCounter); edge = 0;
                    string context = $"Shovel A={primary} dir={direction} tile=${tile:x2} clear={clearPhase}/{physical} jumpUpdates={jumpUpdates} update={++update}";
                    bool parent = Enumerable.Range(0xd2, 4).Any(page => rom[page << 8] != 0 && rom[(page << 8) + 1] == 0x15);
                    FailIf(_player.IsUsingShovel != parent, context + ": parent lifecycle differs.");
                    int[] children = Enumerable.Range(0xd6, 10).Select(page => page << 8)
                        .Where(slot => rom[slot] != 0 && rom[slot + 1] == 0x15).ToArray();
                    FailIf(_player.ShovelChildActive != (children.Length == 1),
                        context + ": physical collision child's four-update lifetime differs.");
                    if (children.Length == 1)
                    {
                        int child = children[0];
                        FailIf(rom[child + 4] != 1 || rom[child + 6] != _shovel.ChildCounter ||
                            new Vector2(rom.Word(child + 0x0c) / 256.0f, rom.Word(child + 0x0a) / 256.0f) !=
                                _shovel.ChildPosition,
                            context + $": shovel child state/counter/full fixed offset differs: runtime={_shovel.ChildCounter}/{_shovel.ChildPosition}, native={rom[child + 4]}/{rom[child + 6]}/{rom.Word(child + 0x0c) / 256f},{rom.Word(child + 0x0a) / 256f}.");
                    }
                    FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256.0f, rom.Word(0xd00a) / 256.0f) ||
                        CarriedObjectMotion.DirectionIndex(_player.FacingVector) != rom[0xd008],
                        context + ": movement lock/facing differs.");
                    if (airborne)
                    {
                        FailIf(_player.TopDownAirborne != (rom[0xcc5c] != 0) ||
                            (_player.ItemCreationZFixed & 0xffff) != rom.Word(0xd00e) ||
                            (_player.TopDownAirSpeedZ & 0xffff) != rom.Word(0xd014),
                            context + ": full Link Z/gravity/landing differs.");
                        FailIf(!sounds.Requests.Where(id => id != SoundId.SndText).SequenceEqual(rom.Sounds),
                            context + ": full Feather/Shovel sound order differs.");
                    }
                    int packed = 0x44;
                    FailIf(_currentRoom.GetMetatile(target) != rom[0xcf00 + packed] ||
                        _currentRoom.GetTerrainInfo(target).Collision != rom[0xce00 + packed] ||
                        _currentRoom.GetUnderlyingMetatile(target) != rom.Underlying(packed),
                        context + ": digging tile/collision/underlying differs.");
                    FailIf(_saveData.GashaMaturity != rom.Word(0xc65f) ||
                        _saveData.HasRoomFlag(0, 0x33, 0x80) != ((rom[0xc700 + 0x33] & 0x80) != 0),
                        context + ": digging maturity or room flag differs.");
                    FailIf(!sounds.Requests.Where(id => id is SoundId.SndDig or SoundId.SndClink or SoundId.SndSolvePuzzle)
                        .SequenceEqual(rom.Sounds.Where(id => id is SoundId.SndDig or SoundId.SndClink or SoundId.SndSolvePuzzle)),
                        context + ": dig/clink/solve sound order differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls,
                        context + ": digging drop RNG differs.");
                    if (drops)
                    {
                        var rewards = _entities.Entities<ItemDropEffect>();
                        int[] parts = Enumerable.Range(0xd0, 16).Select(page => page * 256 + 0xc0)
                            .Where(slot => rom[slot] != 0 && rom[slot + 1] == 1).ToArray();
                        FailIf(rewards.Count != parts.Length, context + $": digging PART$01 deletion differs: runtime={rewards.Count}, native={parts.Length}, Link={_player.PrecisePosition}, nativeLink={rom.Word(0xd00c) / 256f},{rom.Word(0xd00a) / 256f}, grants={_saveData.ReadWramByte(0xc627)}/{rom[0xc627]}, rewards={string.Join(';', rewards.Select(reward => $"{reward.PrecisePosition}/{reward.State}/{reward.CollisionEnabled}"))}.");
                        for (int index = 0; index < rewards.Count; index++)
                        {
                            var reward = rewards[index]; int part = parts[index];
                            FailIf(reward.SubId != rom[part + 2] || (int)reward.State != rom[part + 4] || reward.Counter != rom[part + 6] ||
                                reward.PrecisePosition != new Vector2(rom.Word(part + 0x0c) / 256f, rom.Word(part + 0x0a) / 256f) ||
                                (reward.ZFixed & 0xffff) != rom.Word(part + 0x0e) || (reward.SpeedZ & 0xffff) != rom.Word(part + 0x14) ||
                                reward.CollisionEnabled != ((rom[part + 0x24] & 0x80) != 0), context + $": digging drop runtime={reward.SubId}/{reward.State}/{reward.Counter}/{reward.PrecisePosition}/z{reward.ZFixed}/vz{reward.SpeedZ}/c{reward.CollisionEnabled}, native={rom[part + 2]}/{rom[part + 4]}/{rom[part + 6]}/{rom.Word(part + 0x0c) / 256f},{rom.Word(part + 0x0a) / 256f}/z{rom.Word(part + 0x0e)}/vz{rom.Word(part + 0x14)}/c{rom[part + 0x24]:x2}.");
                            if (reward.SubId == expectedDrop) dropsObserved++;
                        }
                        foreach (int address in new[] { 0xc627, 0xc628, 0xc6aa, 0xc6b0, 0xc6b9, 0xc6ba, 0xc6bb, 0xc6bc, 0xc6bd })
                            FailIf(_saveData.ReadWramByte(address) != rom[address], context + $": reward save byte ${address:x4} differs.");
                    }
                    int[] debris = Enumerable.Range(0xd0, 16).Select(page => page * 256 + 0x40)
                        .Where(slot => rom[slot] != 0 && rom[slot + 1] == 0x0a).ToArray();
                    var chips = _entities.Entities<ShovelDebrisEffect>();
                    FailIf(chips.Count != debris.Length, context + ": shovel debris allocation/deletion differs.");
                    for (int index = 0; index < chips.Count; index++)
                    {
                        var chip = chips[index]; int slot = debris[index];
                        FailIf(chip.PrecisePosition != new Vector2(rom.Word(slot + 0x0c) / 256.0f, rom.Word(slot + 0x0a) / 256.0f) ||
                            (chip.ZFixed & 0xffff) != rom.Word(slot + 0x0e) || (chip.SpeedZ & 0xffff) != rom.Word(slot + 0x14),
                            context + $": shovel debris fixed XYZ/speedZ differs: runtime={chip.PrecisePosition}/{chip.ZFixed}/{chip.SpeedZ}, native={rom.Word(slot + 0x0c) / 256.0f},{rom.Word(slot + 0x0a) / 256.0f}/{rom.Word(slot + 0x0e)}/{rom.Word(slot + 0x14)}.");
                    }
                });
            }
            for (int repeat = 0; repeat < 2; repeat++)
            {
                if (airborne)
                {
                    if (jumpUpdates > 0)
                    {
                        Step(jumpUpdates, feather: true);
                        FailIf(!_player.TopDownAirborne, "Shovel air rejection fixture did not launch through equipped Feather.");
                        int maturityBefore = _saveData.GashaMaturity;
                        Step(1, press: true);
                        FailIf(_player.IsUsingShovel || _shovel.ChildActive || _entities.Entities<ShovelDebrisEffect>().Count != 0,
                            "Fresh Shovel input in ordinary Feather air allocated a parent, child or debris.");
                        _dialogue.ShowMessage("Rejected airborne dig pause.", _player.Position.Y); rom[0xcba0] = 1;
                        Step(6, held: true);
                        _dialogue.Close(); rom[0xcba0] = 0;
                        Step(35, held: true);
                        FailIf(_player.TopDownAirborne || _player.IsUsingShovel || _shovel.ChildActive ||
                            _saveData.GashaMaturity != maturityBefore,
                            "Held Shovel input restarted on landing or altered maturity after air rejection.");
                        Step();
                        Step(1, press: true);
                    }
                    else
                    {
                        Step(1, press: true);
                        Step(-jumpUpdates);
                        // Feather uses a different parent slot. It may start
                        // while ITEM$15's existing animation owns movement.
                        Step(1, feather: true);
                        FailIf(!_player.TopDownAirborne || !_player.IsUsingShovel,
                            "Feather did not preserve an existing Shovel parent across its launch update.");
                    }
                    _dialogue.ShowMessage("Shovel and Feather handoff pause.", _player.Position.Y); rom[0xcba0] = 1;
                    Step(6);
                    _dialogue.Close(); rom[0xcba0] = 0;
                    Step(50);
                    FailIf(_player.TopDownAirborne || _player.IsUsingShovel || _shovel.ChildActive ||
                        _currentRoom.GetMetatile(target) != 0x1c || _saveData.GashaMaturity != 1,
                        "Shovel/Feather handoff missed its independent dirt $01->$1c, +1 maturity or completion.");
                    Step(4);
                    continue;
                }
                Step(1, true, (direction * 8 + 8) & 0x1f);
                if (clearing)
                {
                    Step(clearPhase);
                    bool childExpected = clearPhase is >= 4 and < 8;
                    FailIf(_shovel.ChildActive != childExpected || childExpected && _shovel.ChildCounter != 8 - clearPhase,
                        $"Shovel clearing phase {clearPhase}: independent four-update child boundary differs.");
                    _dialogue.ShowMessage("Clear shovel while item updates are frozen.", _player.Position.Y); rom[0xcba0] = 1;
                    Step(6);
                    _player.ClearNativeItemParents();
                    if (physical)
                    {
                        _entities.ClearPhysicalPlayerItems(); rom.ClearPhysicalItems();
                    }
                    else rom.ClearItemParents();
                    FailIf(_player.IsUsingShovel || _shovel.ChildActive != (childExpected && !physical),
                        "Shovel parent/physical clearing did not preserve the independently owned child appropriately.");
                    Step(3);
                    _dialogue.Close(); rom[0xcba0] = 0;
                    Step(6);
                    FailIf(_shovel.ChildActive, "Shovel collision child did not retire after its remaining eligible updates.");
                    FailIf(_currentRoom.GetMetatile(target) != (clearPhase < 4 ? 0x01 : 0x1c) ||
                        _saveData.GashaMaturity != (clearPhase < 4 ? 0 : 1),
                        "Shovel cancellation changed the source-derived tile attempt or maturity.");
                    Step(4);
                    continue;
                }
                Step(2, angle: (direction * 8 + 8) & 0x1f);
                _dialogue.ShowMessage("Dig pause before tile contact.", _player.Position.Y); rom[0xcba0] = 1;
                Step(6);
                _dialogue.Close(); rom[0xcba0] = 0;
                Step(2);
                _dialogue.ShowMessage("Dig pause after tile contact.", _player.Position.Y); rom[0xcba0] = 1;
                Step(6);
                _dialogue.Close(); rom[0xcba0] = 0;
                Step(20);
                FailIf(_player.IsUsingShovel, "Shovel did not release its parent after the native animation terminator.");
                // Independent source literals: dirt $01->$1c, quest dirt
                // $cb->$d2 and +50/+1 maturity, or unchanged floor/clink.
                bool ordinaryDirt = tile == 0x01 || tile is 0xaf or 0xbf ||
                    tile is >= 0x10 and <= 0x1b or >= 0x20 and <= 0x2b or >= 0x30 and <= 0x3b;
                byte replacement = ordinaryDirt ? (byte)0x1c : tile == 0xcb ? (byte)0xd2 :
                    tile == 0xcc ? (byte)0xd7 : tile == 0xcd ? repeat == 0 ? (byte)0x3a : (byte)0x1c : tile;
                int maturity = ordinaryDirt || tile == 0xcc ? 1 : tile == 0xcb ? 51 : tile == 0xcd ? repeat + 1 : 0;
                FailIf(_currentRoom.GetMetatile(target) != replacement ||
                    _saveData.GashaMaturity != maturity,
                    $"Shovel dir={direction} tile=${tile:x2} repeat={repeat}: source-derived replacement/maturity differs: tile=${_currentRoom.GetMetatile(target):x2}, maturity={_saveData.GashaMaturity}.");
                Step(4);
            }
            FailIf(drops && dropsObserved == 0, "Shovel did not create its independently selected native rupee drop.");
            if (drops)
            {
                int began = update;
                while (_entities.Entities<ItemDropEffect>().Any(reward => reward.State != DropState.Grounded) && update - began < 120) Step(3);
                int rupeesBefore = _inventory.Rupees;
                foreach (var reward in _entities.Entities<ItemDropEffect>().ToArray())
                {
                    began = update;
                    while (!reward.Finished && update - began < 120)
                    {
                        Vector2 delta = reward.PrecisePosition - _player.PrecisePosition;
                        int angle = System.Math.Abs(delta.X) > System.Math.Abs(delta.Y)
                            ? delta.X > 0 ? 8 : 24 : delta.Y > 0 ? 16 : 0;
                        Step(1, angle: angle);
                        FailIf(_currentRoom.IsSolid(_player.Position), "Shovel reward approach entered solid collision geometry.");
                    }
                    FailIf(!reward.Collected, "Shovel reward was not reachable through its room's actual collision geometry.");
                }
                FailIf(_inventory.Rupees <= rupeesBefore, "Collecting the selected shovel rupee drop did not grant money.");
                Step(4);
            }
        }
        GD.Print($"Validated clean-US Shovel A/B/four directions, all overworld digging tiles={allDirtTiles}, actual drops={drops}, parent/physical clearing={clearing}, Feather air rejection/existing-parent handoff={airborne}, fractional Link coordinates, movement lock, maturity/room flags/sounds, dialogue before/after contact and repeat through split/batched gameplay updates.");
    }
}
