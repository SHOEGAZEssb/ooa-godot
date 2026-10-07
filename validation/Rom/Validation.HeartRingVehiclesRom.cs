using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private static readonly FieldInfo HeartRingDistanceField = typeof(Player)
        .GetField("_heartRingDistanceFixed", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private void EquipHeartRingVehicle(int ring)
    {
        _inventory.GiveTreasure(TreasureId.RingBox, 1); _inventory.GrantAppraisedRingForDebug(ring);
        FailIf(!_inventory.SetRingBoxSlotFromList(0, ring) || !_inventory.EquipRingAt(0),
            $"Cannot equip vehicle Heart Ring ${ring:x2}.");
        _inventory.ApplyDamage(4);
    }

    private void SeedHeartRingVehicle(SomariaRom rom, int ring)
    {
        // Declared 24-bit boundary: no full 512/768-pixel playthrough needed.
        int value = ((ring == 0x13 ? 2 : 3) << 16) - 1;
        HeartRingDistanceField.SetValue(_player, value);
        rom.Word(0xcc53, value); rom[0xcc55] = (byte)(value >> 16);
    }

    private void CompareHeartRingVehicle(SomariaRom rom, string context)
    {
        int native = rom.Word(0xcc53) | rom[0xcc55] << 16;
        FailIf((int)HeartRingDistanceField.GetValue(_player)! != native || _inventory.HealthQuarters != rom[0xc6aa] ||
            _saveData.ReadWramByte(0xc65f) != rom[0xc65f] || _saveData.ReadWramByte(0xc660) != rom[0xc660] ||
            _inventory.HasTreasure(TreasureId.HeartRefill) != ((rom[0xc69f] & 2) != 0),
            context + $": Heart Ring counter/health/treasure/maturity differs, runtime=${(int)HeartRingDistanceField.GetValue(_player)!:x6}/${_inventory.HealthQuarters:x2}, native=${native:x6}/${rom[0xc6aa]:x2}.");
    }

    private void AssertHeartRingVehicleRetained(SomariaRom rom, int ring, int health)
    {
        FailIf((rom.Word(0xcc53) | rom[0xcc55] << 16) != ((ring == 0x13 ? 2 : 3) << 16) - 1 ||
            _inventory.HealthQuarters != health, "Mounted/boarding/dismount motion must preserve Heart Ring distance and health.");
    }

    private void AssertHeartRingVehicleHeal(int ring, int health)
    {
        FailIf(_inventory.HealthQuarters != Math.Min(_inventory.MaxHealthQuarters, health + (ring == 0x13 ? 2 : 4)),
            $"Heart Ring ${ring:x2} must resume its source refill on fresh accepted ground movement.");
    }

    private void ValidateHeartRingCompanionRom()
    {
        int fixture = 0;
        foreach (int id in new[] { 0x0b, 0x0c, 0x0d })
        foreach (int ring in new[] { 0x13, 0x14 })
        foreach (bool batched in RomHostSchedules(fixture++))
        {
            var (_, mounted) = PrepareMountedCompanionRom(id, 1);
            EquipHeartRingVehicle(ring);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 1, 72, 64)
                { CompanionDispatchEnabled = true };
            // Existing native initialized species/rider boundary. Every later
            // update executes species, real Link dispatch and post-object copy.
            for (int address = 0xd000; address < 0xd040; address++) rom[address] = mounted[address];
            for (int address = 0xd100; address < 0xd140; address++) rom[address] = mounted[address];
            rom[0xcc2c] = 0xd1; rom[0xcc96] = 1; rom[0xccaa] = 0xff;
            rom[0xcc21] = rom[0xcc22] = 40; rom[0xcc23] = 1;
            // Declared retained Link fractions differ from the companion's.
            typeof(Player).GetField("_precisePosition", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(_player, _player.PrecisePosition + new Vector2(0.25f, 0.5f));
            rom[0xd00c] = 0x40; rom[0xd00a] = 0x80;
            var sounds = _sound.AttachPlayRequestAudit();
            int previous = 0, update = 0;
            void Step(int count = 1, int angle = 0xff, int buttons = 0)
            {
                Vector2 movement = angle == 0xff ? Vector2.Zero : OracleObjectMovement.Shared.Direction(angle);
                int held = buttons | (movement.X > 0 ? 0x10 : movement.X < 0 ? 0x20 : 0) |
                    (movement.Y > 0 ? 0x80 : movement.Y < 0 ? 0x40 : 0);
                int edge = held & ~previous; previous = held;
                StepGameplayUpdates(count, movement, MenuRomActions(held), MenuRomActions(edge), batched, () =>
                {
                    rom.UpdateGameplay(edge, held, angle, _entities.FrameCounter - 1); edge = 0;
                    string context = $"Heart Ring ${ring:x2} companion=${id:x2}, batch={batched}, update={++update}";
                    CompareHeartRingVehicle(rom, context);
                    FailIf(_player.CompanionRideActive != (rom[0xcc2c] == 0xd1) ||
                        _player.CompanionJumpActive != (rom[0xcc2c] == 0xd0 && rom[0xcc5c] != 0) ||
                        _player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f) ||
                        (ushort)(_player.CompanionRideActive ? _player.CompanionRideZFixed : _player.ItemCreationZFixed) != rom.Word(0xd00e), context +
                        $": rider motion/air handoff differs: runtime={_player.CompanionRideActive}/{_player.CompanionJumpActive}/{_player.PrecisePosition}/z=${(ushort)_player.ItemCreationZFixed:x4}; native=${rom[0xcc2c]:x2}/${rom[0xcc5c]:x2}/{rom.Word(0xd00c) / 256f},{rom.Word(0xd00a) / 256f}/z=${rom.Word(0xd00e):x4}, companion XY={rom.Word(0xd10c) / 256f},{rom.Word(0xd10a) / 256f}.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls ||
                        !sounds.Requests.Where(cue => cue is not (SoundId.SndText or SoundId.SndGainHeart)).SequenceEqual(rom.Sounds),
                        context + ": movement cues/shared RNG differ (HUD interpolation excluded).");
                });
            }
            Step();
            int health = _inventory.HealthQuarters;
            SeedHeartRingVehicle(rom, ring);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Step(4, 8);
                _dialogue.ShowGameplayMessage("Mounted Heart Ring", 100); rom[0xcba0] = 1;
                Step(3, 24); _dialogue.Close(); rom[0xcba0] = 0;
                Step(4, 24); Step(); AssertHeartRingVehicleRetained(rom, ring, health);
            }
            if (id == 0x0d)
            {
                Step(1, buttons: 1); Step();
                int remaining = 100;
                while ((rom[0xd104] != 5 || rom[0xd105] != 0) && remaining-- > 0) Step();
                FailIf(rom[0xd104] != 5 || rom[0xd105] != 0, "Moosh's native flap must finish before dismount.");
                AssertHeartRingVehicleRetained(rom, ring, health);
            }
            Step(1, buttons: 2); Step(40);
            FailIf(_player.CompanionRideActive || _player.CompanionJumpActive,
                "Heart Ring companion fixture must complete native dismount/landing.");
            AssertHeartRingVehicleRetained(rom, ring, health);
            Step(1, 8); AssertHeartRingVehicleHeal(ring, health);
            Step(); _inventory.ApplyDamage(4); rom[0xc6aa] = (byte)_inventory.HealthQuarters;
            health = _inventory.HealthQuarters; SeedHeartRingVehicle(rom, ring);
            Step(1, 24); AssertHeartRingVehicleHeal(ring, health); Step();
            // State6 requires leaving the real mount radius before return.
            // Approach by movement, retaining the deliberately distinct low XY.
            Step(12, 8); Step();
            for (int approach = 0; !_player.CompanionJumpActive && approach < 24; approach++) Step(1, 24);
            FailIf(!_player.CompanionJumpActive, $"Companion ${id:x2} must be reachable for a fresh mount.");
            health = _inventory.HealthQuarters; SeedHeartRingVehicle(rom, ring);
            Step(40);
            FailIf(!_player.CompanionRideActive, $"Companion ${id:x2} remount must complete its native nudge/arc.");
            AssertHeartRingVehicleRetained(rom, ring, health);
            Step(1, buttons: 2); Step(40); AssertHeartRingVehicleRetained(rom, ring, health);
            Step(1, 8); AssertHeartRingVehicleHeal(ring, health); Step();
        }
        GD.Print("Compared native Heart Ring L1/L2 retained distance on raft, minecart and Ricky/Dimitri/Moosh, text, natural dismount/landing, fresh ground refill/treasure/maturity and repeat through split/batched gameplay.");
    }
}
