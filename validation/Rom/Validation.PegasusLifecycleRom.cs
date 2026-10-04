using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePegasusLifecycleRom() => ValidatePegasusLifecycleRom(false);
    private void ValidatePegasusAirborneRom() => ValidatePegasusLifecycleRom(true);

    private void ValidatePegasusLifecycleRom(bool airborne)
    {
        int hostCase1 = 0;
        foreach (bool primary in new[] { false, true })
        foreach (int ring in new[] { 0xff, 0x11 })
        foreach (byte amount in new byte[] { 0, 1, 0x10, 0x99 })
        foreach (int jumpUpdates in airborne ? new[] { 1, 15, 29, 0 } : new[] { 0 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            // Grounded BCD/capacity variants belong to the lifecycle scenario;
            // air handoffs retain each jump phase and each count independently.
            if (airborne && jumpUpdates != 1 && amount != 1) continue;
            var save = OracleSaveData.CreateStandardGame(); save.SetLinkName("Link");
            save.WriteWramByte(0xc6bb, amount);
            InitializeTransientSession(save);
            LoadValidationRoom(0, 0x33); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.SeedSatchel, 1);
            if (airborne) _inventory.GiveTreasure(TreasureId.Feather, 1);
            _inventory.SelectSatchelSeeds(2);
            _inventory.EquipA(primary ? TreasureId.SeedSatchel : airborne ? TreasureId.Feather : 0);
            _inventory.EquipB(primary ? airborne ? TreasureId.Feather : 0 : TreasureId.SeedSatchel);
            if (ring != 0xff)
            {
                _inventory.GiveTreasure(TreasureId.RingBox, 1);
                _inventory.GrantAppraisedRingForDebug(ring);
                FailIf(!_inventory.SetRingBoxSlotFromList(0, ring) || !_inventory.EquipRingAt(0),
                    "Could not equip Pegasus Ring $11.");
            }
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), 0xa0, 0, 0);
            _player.WarpTo(new(80.25f, 64.5f)); _player.Face(Vector2I.Up);
            var initialRandom = _random.CaptureState();
            var rom = new SomariaRom(_saveData, initialRandom, _currentRoom, 0, 80, 64);
            rom[0xd00a] = 0x80; rom[0xd00c] = 0x40; rom.InitializeLinkGameplay();
            var sounds = _sound.AttachPlayRequestAudit();
            int button = primary ? 1 : 2, update = 0;
            void Step(int count = 1, bool press = false, int angle = 0xff, bool feather = false)
            {
                Vector2 movement = angle == 0xff ? Vector2.Zero : OracleObjectMovement.Shared.Direction(angle);
                int directions = movement.X > 0 ? 0x10 : movement.X < 0 ? 0x20 : 0;
                int itemsHeld = (press ? button : 0) | (feather ? primary ? 2 : 1 : 0);
                int edge = itemsHeld;
                StepGameplayUpdates(count, movement, MenuRomActions(itemsHeld | directions), MenuRomActions(edge), batched, () =>
                {
                    rom.UpdateGameplay(edge, itemsHeld | directions, angle, _entities.FrameCounter); edge = 0;
                    string context = $"Pegasus A={primary} ring=${ring:x2} amount=${amount:x2} jumpUpdates={jumpUpdates} update={++update}";
                    FailIf(_inventory.PegasusSeeds != rom[0xc6bb] || _seedSatchel.Pegasus.RawCounter != rom.Word(0xcc6c) ||
                        _player.IsUsingSeedSatchel || Enumerable.Range(0xd2, 4).Any(page => rom[page << 8] != 0 && rom[(page << 8) + 1] == 0x19),
                        context + ": consumption/timer/parent clear differs.");
                    FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256.0f, rom.Word(0xd00a) / 256.0f) ||
                        CarriedObjectMotion.DirectionIndex(_player.FacingVector) != rom[0xd008],
                        context + ": activation/movement/facing differs.");
                    if (airborne)
                    {
                        FailIf(_player.TopDownAirborne != (rom[0xcc5c] != 0) ||
                            (_player.ItemCreationZFixed & 0xffff) != rom.Word(0xd00e) ||
                            (_player.TopDownAirSpeedZ & 0xffff) != rom.Word(0xd014),
                            context + ": Link full Z/gravity/landing differs.");
                        FailIf(!sounds.Requests.Where(id => id != SoundId.SndText).SequenceEqual(rom.Sounds),
                            context + ": full Feather/Pegasus sound order differs.");
                        var random = _random.CaptureState();
                        FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                            random.Calls - initialRandom.Calls != rom.RandomCalls, context + ": shared RNG differs.");
                    }
                    ComparePegasusDustRom(rom, airborne, context);
                    FailIf(!sounds.Requests.Where(id => id == 0xa3).SequenceEqual(rom.Sounds.Where(id => id == 0xa3)),
                        context + ": running sound order differs.");
                });
            }
            if (airborne && jumpUpdates > 0)
            {
                Step(jumpUpdates, feather: true);
                FailIf(!_player.TopDownAirborne, "Airborne Pegasus fixture did not launch through equipped Feather.");
            }
            Step(1, true, 8);
            FailIf((_seedSatchel.Pegasus.RawCounter & 0x7fff) != (amount == 0 ? 0 : 0x03c0),
                "Pegasus activation did not initialize the native $03c0 counter.");
            if (airborne && jumpUpdates == 0)
            {
                Step(1, angle: 8, feather: true);
                FailIf(!_player.TopDownAirborne, "Existing Pegasus must permit an equipped Feather launch.");
            }
            Step(3, angle: 8); Step(4, angle: 24);
            Step(1, true); // Active timer rejects another consumption.
            _dialogue.ShowMessage("Pegasus pause.", _player.Position.Y); rom[0xcba0] = 1;
            Step(8);
            _dialogue.Close(); rom[0xcba0] = 0;
            Step(4, angle: 8); Step(4, angle: 24);
            int remaining = _seedSatchel.Pegasus.RawCounter & 0x7fff;
            int decrement = ring == 0x11 ? 1 : 2;
            if (remaining != 0)
            {
                int untilExpiry = (remaining + decrement - 1) / decrement;
                Step(untilExpiry - 1);
                FailIf(!_seedSatchel.Pegasus.Active, "Pegasus expired before its native last decrement.");
                Step();
                FailIf(_seedSatchel.Pegasus.Active, "Pegasus survived its native zero-counter update.");
            }
            Step(airborne ? 35 : 4);
            FailIf(airborne && _player.TopDownAirborne, "Pegasus activation retained a Feather jump after native landing.");
            Step(1, true);
            Step(8);
        }
        GD.Print($"Validated clean-US Pegasus A/B, Feather air handoffs={airborne}, empty/one/BCD-borrow/cap counts, active rejection, Pegasus Ring, exact expiry, repeat activation, reserved dust/cloud animation, dialogue and movement/sounds through split/batched application updates.");
    }

    private void ComparePegasusDustRom(SomariaRom rom, bool compareHeight, string context)
    {
        var dust = _entities.Entities<PegasusDustRoomEntity>()
            .Concat(_entities.OutgoingEntities<PegasusDustRoomEntity>()).ToList();
        bool nativeDust = rom[0xdf00] != 0 && rom[0xdf01] == 0x1a;
        FailIf((dust.Count == 1) != nativeDust, context + ": reserved dust allocation/deletion differs.");
        if (nativeDust)
        {
            if (compareHeight)
            {
                int z = (int)typeof(PegasusDustRoomEntity).GetField("_z",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(dust[0])!;
                FailIf((byte)z != rom[0xdf0f], context + ": dust startup copied height differs.");
            }
            FailIf(dust[0].Substate != rom[0xdf05] || dust[0].Position != new Vector2(rom[0xdf0d], rom[0xdf0b]) ||
                dust[0].Visible != ((rom[0xdf1a] & 0x80) != 0) || dust[0].TileBase != rom[0xdf1d] ||
                dust[0].OamFlags != rom[0xdf1c] ||
                !dust[0].Clouds.ToArray().SequenceEqual(Enumerable.Range(0, 8).Select(index => rom[0xdf30 + index])),
                context + $": dust differs: runtime={dust[0].Substate}/{dust[0].Position}/{dust[0].Visible}/tile=${dust[0].TileBase:x2}/flags=${dust[0].OamFlags:x2}, native={rom[0xdf05]}/{rom[0xdf0d]},{rom[0xdf0b]}/{(rom[0xdf1a] & 0x80) != 0}/tile=${rom[0xdf1d]:x2}/flags=${rom[0xdf1c]:x2}.");
        }
    }
}
