using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ComparePegasusSideViewRom()
    {
        int fixture = 0;
        foreach (bool primary in new[] { false, true })
        foreach (int jumpUpdates in new[] { 0, 1, 15, 29 })
        foreach (bool batched in RomHostSchedules(fixture++))
        {
            SomariaRom rom = PrepareBombchuGameplayRom(1, primary, sideview: true);
            _inventory.GiveTreasure(TreasureId.SeedSatchel, 1);
            _inventory.GiveTreasure(TreasureId.PegasusSeeds, 0x10); _inventory.SelectSatchelSeeds(2);
            _inventory.GiveTreasure(TreasureId.Feather, 1);
            _inventory.EquipA(primary ? TreasureId.SeedSatchel : TreasureId.Feather);
            _inventory.EquipB(primary ? TreasureId.Feather : TreasureId.SeedSatchel);
            for (int address = 0xc600; address < 0xc800; address++) rom[address] = _saveData.ReadWramByte(address);
            rom[0xd009] = (byte)_player.SideScrollAngle; rom.InitializeLinkWalkingAnimation();
            var seed = _random.CaptureState(); var sounds = _sound.AttachPlayRequestAudit();
            int button = primary ? 1 : 2, feather = button ^ 3, previous = 0, updates = 0;
            void Step(int count = 1, int held = 0, int angle = 0xff)
            {
                int edge = held & ~previous; previous = held;
                StepSomariaMotionRom(rom, count, batched, angle, held, edge, () =>
                {
                    string context = $"Pegasus side-view A={primary}, jump={jumpUpdates}, batch={batched}, update={++updates}";
                    FailIf(_seedSatchel.Pegasus.RawCounter != rom.Word(0xcc6c) || _inventory.PegasusSeeds != rom[0xc6bb] ||
                        _player.IsUsingSeedSatchel || _player.SideScrollAirborne != (rom[0xcc5c] != 0) ||
                        (ushort)_player.SideScrollSpeedZ != rom.Word(0xd014) || _player.SideScrollAngle != rom[0xd009] ||
                        _player.SideScrollSpeedRaw != rom[0xd010],
                        context + ": counter/ammo/parent or full air velocity differs.");
                    ComparePegasusDustRom(rom, true, context);
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls ||
                        !sounds.Requests.SequenceEqual(rom.Sounds), context + ": ordered cues/shared RNG differ.");
                });
            }
            if (jumpUpdates == 0)
            {
                Step(1, button); Step(); Step(1, feather);
                // Declared live expiry boundary, already covered for the full
                // counter duration elsewhere. Air retains its launch speed;
                // the next ordinary ground update reevaluates Pegasus speed.
                _entities.RuntimeState.SetWramByte(WramAddress.wPegasusSeedCounter, 0x11);
                _entities.RuntimeState.SetWramByte(WramAddress.wPegasusSeedCounter + 1, 0);
                rom.Word(0xcc6c, 0x11); Step(9, angle: 8);
                FailIf(_seedSatchel.Pegasus.Active || !_player.SideScrollAirborne,
                    "Pegasus must expire during the existing side-view Feather arc.");
            }
            else
            {
                Step(jumpUpdates, feather);
                FailIf(!_player.SideScrollAirborne, "Side-view Pegasus fixture must use an actual Feather jump.");
                Step(1, button); Step(8, angle: 8);
            }
            _dialogue.ShowMessage("Side-view Pegasus pause.", _player.Position.Y); rom[0xcba0] = 1;
            Step(3); _dialogue.Close(); rom[0xcba0] = 0;
            Step(32);
            FailIf(_player.SideScrollAirborne, "Side-view Pegasus handoff must land on the actual room floor.");
            Step(); Step(1, button); Step(); Step(1, feather); Step(40);
            FailIf(_player.SideScrollAirborne, "Fresh Feather must complete after the Pegasus activation/rejection edge.");
        }
        GD.Print("Compared native side-view Pegasus activation during Feather ascent/apex/descent, retained launch speed through expiry, full air motion/facing, dust, dialogue, landing and repeated jumps through split/batched A/B gameplay.");
    }
}
