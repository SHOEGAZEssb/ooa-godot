using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareBombchuParentOrderRom()
    {
        foreach (int other in new[] { TreasureId.Boomerang, TreasureId.SeedSatchel, TreasureId.Sword, TreasureId.Harp })
        foreach (bool primary in new[] { false, true })
        foreach (bool batched in new[] { false, true })
        {
            var rom = PrepareSomariaMotionRom(0, primary: true);
            _inventory.GiveTreasure(TreasureId.Bombchus, 0x10);
            _inventory.GiveTreasure(other, other == TreasureId.Boomerang ? 0 : 1);
            _inventory.GiveTreasure(TreasureId.EmberSeeds, 0x10); _inventory.SelectSatchelSeeds(0);
            _inventory.EquipA(primary ? TreasureId.Bombchus : other);
            _inventory.EquipB(primary ? other : TreasureId.Bombchus);
            rom[0xc689] = (byte)_inventory.EquippedA; rom[0xc688] = (byte)_inventory.EquippedB;
            rom[0xc6b3] = 0x10; rom[0xc6b2] = (byte)_inventory.SwordLevel;
            rom[0xc6b9] = 0x10; rom.InitializeLinkWalkingAnimation();
            var pool = SomariaPrivate<DynamicItemSlotPool>(_entities, "_dynamicItems");
            var seed = _random.CaptureState(); var sounds = _sound.AttachPlayRequestAudit();
            int update = 0;
            void Compare()
            {
                string context = $"Bombchu chord ITEM${other:x2}, A={primary}, batch={batched}, update={++update}";
                foreach (var parent in new[] { _entities.BombchuParent, _entities.BoomerangParent })
                {
                    int id = ReferenceEquals(parent, _entities.BombchuParent) ? 0x0d : 0x06;
                    int slot = Enumerable.Range(0xd3, 2).Select(page => page << 8)
                        .FirstOrDefault(slot => rom[slot] != 0 && rom[slot + 1] == id);
                    FailIf(parent.Active != (slot != 0) || parent.Active &&
                        (parent.Slot != ((slot >> 8) & 15) || parent.Counter != rom[slot + 0x20] || parent.Parameter != rom[slot + 0x21]),
                        context + $": ITEM${id:x2} parent allocation/clock differs.");
                }
                var child = pool.LiveOwners().OfType<BombchuRoomEntity>().SingleOrDefault();
                int native = Enumerable.Range(0xd7, 5).Select(page => page << 8)
                    .FirstOrDefault(slot => rom[slot] != 0 && rom[slot + 1] == 0x0d);
                FailIf((child is null) != (native == 0) || child is not null &&
                    (pool.SlotOf(child) != native >> 8 || child.Node.Position != new Vector2(rom[native + 0xd], rom[native + 0xb])),
                    context + $": physical allocation order or motion runtime=${(child is null ? 0 : pool.SlotOf(child)):x2}/{child?.Node.Position}, " +
                    $"native=${native:x4}/({rom[native + 0xd]},{rom[native + 0xb]}) differs.");
                FailIf(_player.IsUsingHarp != (rom[0xd500] != 0) || _player.IsAttacking != (rom[0xd200] != 0 && rom[0xd201] == 5) ||
                    _inventory.Bombchus != rom[0xc6b3], context + ": competing parent/ammo differs.");
                if (_player.IsUsingSeedSatchel)
                {
                    int slot = SomariaPrivate<int>(_player, "_seedSatchelParentSlot") << 8 | 0xd000;
                    FailIf(rom[slot] == 0 || rom[slot + 1] != 0x19, context + ": Satchel must use the source-selected lower slot.");
                }
                CompareSomariaMotionRom(rom, context);
                var rng = _random.CaptureState();
                FailIf(rng.Rng1 != rom[0xff94] || rng.Rng2 != rom[0xff95] || rng.Calls - seed.Calls != rom.RandomCalls ||
                    !sounds.Requests.SequenceEqual(rom.Sounds), context + ": ordered cue/RNG differs.");
            }
            StepSomariaMotionRom(rom, 1, batched, 0xff, 3, 3, Compare);
            StepSomariaMotionRom(rom, 20, batched, 0xff, 0, 0, Compare);
            _entities.ClearPhysicalPlayerItems(); rom.ClearPhysicalItems();
        }
        GD.Print("Compared simultaneous A/B Bombchu with Boomerang, Satchel, Sword and Harp: source parent slots/clocks, child allocation order, movement and cue/RNG order through split/batched gameplay.");
    }
}
