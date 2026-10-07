using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ComparePegasusPunchRom()
    {
        foreach (int ring in new[] { 0x0b, 0x3d })
        foreach (bool primary in new[] { false, true })
        foreach (bool batched in new[] { false, true })
        {
            SomariaRom rom = PrepareSomariaMotionRom(2, primary: primary);
            _inventory.GiveTreasure(TreasureId.SeedSatchel, 1);
            _inventory.GiveTreasure(TreasureId.PegasusSeeds, 0x10); _inventory.SelectSatchelSeeds(2);
            _inventory.GiveTreasure(TreasureId.RingBox, 1); _inventory.GrantAppraisedRingForDebug(ring);
            FailIf(!_inventory.SetRingBoxSlotFromList(0, ring) || !_inventory.EquipRingAt(0),
                $"Could not equip Pegasus punch ring${ring:x2}.");
            EquipSomariaMotionItem(rom, TreasureId.SeedSatchel, primary);
            for (int address = 0xc600; address < 0xc800; address++) rom[address] = _saveData.ReadWramByte(address);
            rom.InitializeLinkWalkingAnimation();
            var seed = _random.CaptureState(); var sounds = _sound.AttachPlayRequestAudit();
            int button = primary ? 1 : 2, other = button ^ 3, previous = 0, updates = 0;
            void Step(int count = 1, int held = 0)
            {
                int edge = held & ~previous; previous = held;
                StepSomariaMotionRom(rom, count, batched, held: held, pressed: edge, afterUpdate: () =>
                {
                    string context = $"Pegasus/punch ring${ring:x2}, A={primary}, batch={batched}, update={++updates}";
                    bool punch = rom[0xd200] != 0 && rom[0xd201] == 2;
                    FailIf(_player.IsUsingPunch != punch || punch && _player.IsUsingExpertPunch != (ring == 0x0b) ||
                        _seedSatchel.Pegasus.RawCounter != rom.Word(0xcc6c) || _inventory.PegasusSeeds != rom[0xc6bb],
                        context + $": punch={_player.IsUsingPunch}/{punch}, frame={_player.PunchFrame}, parent clock=${rom[0xd220]:x2}/${rom[0xd221]:x2}, " +
                        $"timer=${_seedSatchel.Pegasus.RawCounter:x4}/${rom.Word(0xcc6c):x4}, ammo=${_inventory.PegasusSeeds:x2}/${rom[0xc6bb]:x2}.");
                    ComparePegasusDustRom(rom, false, context);
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls ||
                        !sounds.Requests.SequenceEqual(rom.Sounds), context + ": ordered cues/shared RNG differ.");
                });
            }
            void Equip(bool satchel)
            {
                // Declared inventory result. checkItemUsed requires BOTH bytes
                // empty before substituting ITEM_PUNCH for an empty button.
                if (satchel)
                {
                    if (primary) _inventory.EquipA(TreasureId.SeedSatchel);
                    else _inventory.EquipB(TreasureId.SeedSatchel);
                }
                else
                {
                    foreach (bool isA in new[] { true, false })
                        if ((isA ? _inventory.EquippedA : _inventory.EquippedB) != 0)
                            _inventory.SwapStorageSlotWithButton(Enumerable.Range(0, InventoryState.InventoryCapacity)
                                .First(index => _inventory.StorageItemAt(index) == 0), isA);
                }
                rom[0xc689] = (byte)_inventory.EquippedA; rom[0xc688] = (byte)_inventory.EquippedB;
            }
            Step(1, button); Step(4); Step(1, other);
            FailIf(_player.IsUsingPunch, "An empty other button must not punch while Satchel remains equipped.");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Equip(false); Step(); Step(1, button); Step(2, button);
                FailIf(!_player.IsUsingPunch, "Fist/Expert punch must initialize with both equipment bytes empty.");
                Equip(true); Step(); Step(1, button);
                FailIf(!_player.IsUsingPunch, "Rejected active Pegasus input must retain the existing punch parent.");
                _dialogue.ShowMessage("Pegasus punch pause.", _player.Position.Y); rom[0xcba0] = 1;
                Step(3); _dialogue.Close(); rom[0xcba0] = 0; Step(16);
                FailIf(_player.IsUsingPunch || !_seedSatchel.Pegasus.Active,
                    "Punch completion must leave Pegasus's independently decremented timer active.");
            }
        }
        GD.Print("Compared native Pegasus with Fist/Expert punches: both-empty eligibility, retained parent across Satchel re-equipment/rejected activation, exact completion, text/dust/full Link/cues/RNG and repeated split/batched A/B gameplay.");
    }
}
