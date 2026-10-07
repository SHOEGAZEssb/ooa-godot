using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSomariaSatchelInput() => CompareSomariaItemOverlapRom(feather: false);
    private void ValidateSomariaFeatherInput() => CompareSomariaItemOverlapRom(feather: true);

    private void CompareSomariaItemOverlapRom(bool feather)
    {
        var seedFrame = typeof(EmberSeedEffect).GetField("_frameCounter", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (bool primary in new[] { false, true })
        foreach (bool batched in new[] { false, true })
        {
            SomariaRom rom = PrepareSomariaMotionRom(2, primary: primary);
            int other = feather ? TreasureId.Feather : TreasureId.SeedSatchel;
            _inventory.GiveTreasure(other, 1);
            if (!feather)
            {
                _inventory.GiveTreasure(TreasureId.EmberSeeds, 5);
                _inventory.SelectSatchelSeeds(0);
            }
            if (primary) _inventory.EquipB(other); else _inventory.EquipA(other);
            for (int address = 0xc600; address < 0xc800; address++) rom[address] = _saveData.ReadWramByte(address);
            rom.InitializeLinkWalkingAnimation();
            var seed = _random.CaptureState(); var sounds = _sound.AttachPlayRequestAudit();
            int update = 0;
            void Compare()
            {
                string context = $"Cane/ITEM${other:x2} A-Cane={primary}, batch={batched}, update={++update}";
                var cane = _entities.Somaria!;
                bool parent = rom[0xd200] != 0 && rom[0xd201] == 4;
                bool weapon = rom[0xd600] != 0 && rom[0xd601] == 4;
                FailIf(cane.Active != parent || (cane.Weapon != null) != weapon,
                    context + ": Cane parent/reserved weapon differs.");
                if (parent)
                    FailIf(cane.Parent!.Parameter != rom[0xd221] || SomariaPrivate<int>(cane.Parent, "_counter") != rom[0xd220],
                        context + ": Cane animation clock/parameter differs.");
                if (weapon) FailIf(cane.Weapon!.State != rom[0xd604], context + ": Cane creation state differs.");
                FailIf(_player.TopDownAirborne != (rom[0xcc5c] != 0) ||
                    (ushort)_player.ItemCreationZFixed != rom.Word(0xd00e) ||
                    (ushort)_player.TopDownAirSpeedZ != rom.Word(0xd014), context + ": Link jump/gravity differs.");
                bool satchel = Enumerable.Range(0xd2, 4).Any(page => rom[page << 8] != 0 && rom[(page << 8) + 1] == 0x19);
                FailIf(_player.IsUsingSeedSatchel != satchel || _inventory.EmberSeeds != rom[0xc6b9],
                    context + ": Satchel lifetime/BCD count differs.");
                int[] nativeSeeds = Enumerable.Range(0xd7, 5).Select(page => page << 8)
                    .Where(slot => rom[slot] != 0 && rom[slot + 1] == 0x20).ToArray();
                var children = _entities.Entities<EmberSeedEffect>();
                FailIf(children.Count != nativeSeeds.Length, context + ": Ember allocation/deletion differs.");
                for (int index = 0; index < children.Count; index++)
                {
                    var child = children[index]; int slot = nativeSeeds[index];
                    int state = child.State == EmberState.Flying ? 1 : 3;
                    FailIf(state != rom[slot + 4] || child.PrecisePosition !=
                            new Vector2(rom.Word(slot + 0xc) / 256f, rom.Word(slot + 0xa) / 256f) ||
                        (ushort)child.ZFixed != rom.Word(slot + 0xe) || (ushort)child.SpeedZ != rom.Word(slot + 0x14) ||
                        child.NativeAngle != rom[slot + 9] || child.Visible != ((rom[slot + 0x1a] & 0x80) != 0) ||
                        child.CollisionEnabled != ((rom[slot + 0x24] & 0x80) != 0) ||
                        (int)seedFrame.GetValue(child)! != rom[slot + 0x20] || state > 1 && child.FlameCounter != rom[slot + 6],
                        context + ": Ember full motion/animation/effect differs.");
                }
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls ||
                    !sounds.Requests.SequenceEqual(rom.Sounds), context + ": full sound/shared RNG differs.");
            }
            void Step(int count = 1, int held = 0, int pressed = 0) =>
                StepSomariaMotionRom(rom, count, batched, held: held, pressed: pressed, afterUpdate: Compare);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                _entities.ClearPhysicalPlayerItems(); rom.ClearPhysicalItems(); Step();
                int count = SeedCount();
                // ages/itemUsageTables.s allocates Cane selector3 separately
                // from Feather selector1/Satchel selector2, before either runs.
                Step(1, 3, 3);
                FailIf(!CaneActive() || (feather ? !_player.TopDownAirborne :
                    !_player.IsUsingSeedSatchel || SeedCount() != count - 1),
                    "A/B allocation must retain independent Cane and Feather/Satchel owners.");
                Step(13);
                FailIf(rom.Blocks.Length != 0 || !CaneActive() || _entities.Somaria!.Weapon!.State != 1,
                    "Cane must wait for source animation parameter$06 before creating ITEM$18.");
                _dialogue.ShowMessage("Cane overlap pause.", _player.Position.Y); rom[0xcba0] = 1;
                Step(3); _dialogue.Close(); rom[0xcba0] = 0;
                Step();
                FailIf(rom.Blocks.Length != 1 || _entities.Somaria!.Weapon!.State != 2 ||
                    feather && rom[rom.Blocks[0] + 0xf] != rom[0xd00f],
                    "Cane parameter$06 must create one block with the current high Z.");
                Step(4);
                if (feather)
                {
                    FailIf(CaneActive() || !_player.TopDownAirborne, "Cane completion must retain Feather air ownership.");
                    Step(5);
                    FailIf(rom.Blocks.Length != 0, "Source phase-in must reject the copied airborne height.");
                    Step(30);
                    FailIf(_player.TopDownAirborne || rom.Blocks.Length != 0, "Landing must not retry the rejected block.");
                }
                else
                {
                    Step(8);
                    FailIf(CaneActive() || _player.IsUsingSeedSatchel || SeedCount() != count - 1,
                        "Both source parents must finish independently after a single seed consumption.");
                    int remaining = 120;
                    while (_entities.Entities<EmberSeedEffect>().Count != 0 && remaining-- > 0) Step();
                    FailIf(_entities.Entities<EmberSeedEffect>().Count != 0, "Overlapping Ember must finish its native flame lifetime.");
                }
            }
            if (feather)
            {
                _inventory.GiveTreasure(TreasureId.Sword, 1);
                _inventory.EquipA(TreasureId.Sword); _inventory.EquipB(TreasureId.CaneOfSomaria);
                for (int address = 0xc600; address < 0xc800; address++) rom[address] = _saveData.ReadWramByte(address);
                Step(); Step(1, 3, 3);
                FailIf(!_player.IsAttacking || CaneActive() || rom[0xd201] != 5,
                    "Source Sword priority$60 must beat simultaneous Cane priority$00.");
                Step(40);
            }
            bool CaneActive() => _entities.Somaria!.Active;
            int SeedCount() => (_inventory.EmberSeeds >> 4) * 10 + (_inventory.EmberSeeds & 15);
        }
        GD.Print($"Compared native Cane/{(feather ? "Feather" : "Ember Satchel")} simultaneous A/B allocation, exact creation/completion, child lifetime, full Link/room, dialogue, cues/RNG and repeat in split/batched gameplay.");
    }
}
