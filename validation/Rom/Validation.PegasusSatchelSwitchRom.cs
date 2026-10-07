using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ComparePegasusSatchelSwitchRom()
    {
        foreach (bool primary in new[] { false, true })
        foreach (bool batched in new[] { false, true })
        {
            SomariaRom rom = PrepareSomariaMotionRom(2, primary: primary);
            _inventory.GiveTreasure(TreasureId.SeedSatchel, 1);
            _inventory.GiveTreasure(TreasureId.EmberSeeds, 0x10);
            _inventory.GiveTreasure(TreasureId.PegasusSeeds, 0x10);
            _inventory.SelectSatchelSeeds(0);
            EquipSomariaMotionItem(rom, TreasureId.SeedSatchel, primary);
            for (int address = 0xc600; address < 0xc800; address++) rom[address] = _saveData.ReadWramByte(address);
            rom.InitializeLinkWalkingAnimation();
            var seed = _random.CaptureState(); var sounds = _sound.AttachPlayRequestAudit();
            int button = primary ? 1 : 2, previous = 0, updates = 0;
            void Step(int count = 1, int held = 0)
            {
                int edge = held & ~previous; previous = held;
                StepSomariaMotionRom(rom, count, batched, held: held, pressed: edge, afterUpdate: () =>
                {
                    string context = $"Pegasus/Satchel selector A={primary}, batch={batched}, update={++updates}";
                    bool parent = rom[0xd300] != 0 && rom[0xd301] == 0x19 || rom[0xd400] != 0 && rom[0xd401] == 0x19;
                    FailIf(_player.IsUsingSeedSatchel != parent || _seedSatchel.Pegasus.RawCounter != rom.Word(0xcc6c) ||
                        _inventory.EmberSeeds != rom[0xc6b9] || _inventory.PegasusSeeds != rom[0xc6bb],
                        context + ": parent/timer/BCD counts differ.");
                    int children = Enumerable.Range(0xd7, 5).Count(page => rom[page << 8] != 0 && rom[(page << 8) + 1] == 0x20);
                    FailIf(_entities.Entities<EmberSeedEffect>().Count != children, context + ": retained physical Ember differs.");
                    ComparePegasusDustRom(rom, false, context);
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls ||
                        !sounds.Requests.SequenceEqual(rom.Sounds), context + ": ordered cues/shared RNG differ.");
                });
            }
            void Select(int selection)
            {
                // Declared picker result; the original global selector belongs
                // to both buttons and does not replace a retained ITEM$19 parent.
                _inventory.SelectSatchelSeeds(selection); rom[0xc6c4] = (byte)selection;
            }
            Step(1, button); Step();
            FailIf(!_player.IsUsingSeedSatchel || _entities.Entities<EmberSeedEffect>().Count != 1,
                "Initial Ember must retain its selector2 Satchel parent and physical child.");
            Select(2); int pegasus = _inventory.PegasusSeeds;
            Step(1, button); Step(8, button);
            FailIf(_seedSatchel.Pegasus.Active || _inventory.PegasusSeeds != pegasus || _player.IsUsingSeedSatchel,
                "chooseParentItemSlot selector2 must reject a second ITEM$19 regardless of its changed seed selection.");
            Step(); Step(1, button);
            FailIf((_seedSatchel.Pegasus.RawCounter & 0x7fff) != 0x03c0 || _entities.Entities<EmberSeedEffect>().Count != 1,
                "After parent release, Pegasus must activate beside the retained physical Ember.");
            Step(8);
            _dialogue.ShowMessage("Pegasus selection pause.", _player.Position.Y); rom[0xcba0] = 1;
            Step(3); _dialogue.Close(); rom[0xcba0] = 0; Step(8);
            Select(0); int ember = _inventory.EmberSeeds;
            Step(1, button);
            FailIf(_inventory.EmberSeeds != ember || !_seedSatchel.Pegasus.Active,
                "Retained ITEM$20 cap must reject another Ember without consuming seeds or replacing Pegasus.");
            Step(); int remaining = 120;
            while (_entities.Entities<EmberSeedEffect>().Count != 0 && remaining-- > 0) Step();
            FailIf(_entities.Entities<EmberSeedEffect>().Count != 0, "Ember must release its dynamic slot on its native lifetime boundary.");
            int counter = _seedSatchel.Pegasus.RawCounter & 0x7fff;
            Step(1, button);
            FailIf(_entities.Entities<EmberSeedEffect>().Count != 1 ||
                (_seedSatchel.Pegasus.RawCounter & 0x7fff) != counter - 2,
                "A fresh Ember must use the released slot while retaining the existing Pegasus decrement.");
            Step(8);
        }
        GD.Print("Compared native Satchel selector changes with a retained parent/Ember child, delayed Pegasus activation, active-timer Ember cap rejection, slot release/fresh throw, text, full Link/room, dust/cues/RNG and split/batched A/B gameplay.");
    }
}
