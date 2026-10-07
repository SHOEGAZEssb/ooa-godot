using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSomariaParentPriority()
    {
        // ages/itemUsageTables.s: Cane$03, Shovel$13, Bomb$23, Shooter$43,
        // Sword$63 and Hook$73. A and B reserve before parent updates.
        foreach (int other in new[] { TreasureId.Sword, TreasureId.Bombs, TreasureId.Shovel,
            TreasureId.Shooter, TreasureId.SwitchHook })
        foreach (bool primary in new[] { false, true })
        foreach (bool batched in new[] { false, true })
        {
            SomariaRom rom = PrepareSomariaMotionRom(2, primary: primary);
            _inventory.GiveTreasure(other, other == TreasureId.Bombs ? 0x10 : 1);
            _inventory.GiveTreasure(TreasureId.EmberSeeds, 5); _inventory.SelectShooterSeeds(0);
            if (primary) _inventory.EquipB(other); else _inventory.EquipA(other);
            for (int address = 0xc600; address < 0xc800; address++) rom[address] = _saveData.ReadWramByte(address);
            rom.InitializeLinkWalkingAnimation();
            var seed = _random.CaptureState(); var sounds = _sound.AttachPlayRequestAudit();
            int button = primary ? 1 : 2, competing = button ^ 3, updates = 0;
            bool OtherActive() => other switch
            {
                TreasureId.Sword => _player.IsAttacking,
                TreasureId.Bombs => _bomb.Active,
                TreasureId.Shovel => _player.IsUsingShovel,
                TreasureId.Shooter => _player.IsUsingSeedShooter,
                TreasureId.SwitchHook => _player.IsUsingSwitchHook,
                _ => false
            };
            void Compare()
            {
                string context = $"Cane priority vs ITEM${other:x2}, A-Cane={primary}, batch={batched}, update={++updates}";
                var cane = _entities.Somaria!;
                int parent = rom[0xd200] != 0 ? rom[0xd201] : 0;
                FailIf(cane.Active != (parent == 4) || OtherActive() != (parent == other) ||
                    (cane.Weapon != null) != (rom[0xd600] != 0 && rom[0xd601] == 4),
                    context + $": parent/reserved weapon ownership differs; native parent=${parent:x2}.");
                if (cane.Active)
                    FailIf(cane.Parent!.Parameter != rom[0xd221] || SomariaPrivate<int>(cane.Parent, "_counter") != rom[0xd220] ||
                        cane.Weapon!.State != rom[0xd604], context + ": Cane creation clock/state differs.");
                FailIf(_inventory.Bombs != rom[0xc6b0] || _inventory.EmberSeeds != rom[0xc6b9],
                    context + ": BCD counts differ.");
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls ||
                    !sounds.Requests.SequenceEqual(rom.Sounds), context + ": ordered cues/shared RNG differ.");
            }
            void Step(int count = 1, int held = 0, int pressed = 0) =>
                StepSomariaMotionRom(rom, count, batched, held: held, pressed: pressed, afterUpdate: Compare);
            void Clear()
            {
                _entities.ClearPhysicalPlayerItems();
                // Full replacement also resets the player's remaining parent
                // owners. Native clearAllItemsAndPutLinkOnGround owns both.
                _player.WarpTo(_player.PrecisePosition);
                rom.ClearPhysicalItems(); Step();
            }
            foreach (bool replace in new[] { false, true })
            {
                Clear();
                if (replace)
                {
                    Step(1, button, button); Step(12);
                    FailIf(!_entities.Somaria!.Active || rom.Blocks.Length != 0,
                        "Cane must retain its pending creation before higher-priority replacement.");
                }
                Step(1, replace ? competing : 3, replace ? competing : 3);
                FailIf(!OtherActive() || _entities.Somaria!.Active || rom.Blocks.Length != 0,
                    $"ITEM${other:x2} must win over Cane before physical block creation.");
                Step(1, 3, button);
                FailIf(!OtherActive() || _entities.Somaria!.Active,
                    $"Cane priority$00 must not replace the active ITEM${other:x2} owner.");
                Clear(); Step(40); Step(1, button, button); Step(23);
                FailIf(_entities.Somaria!.Active || rom.Blocks.Length != 1,
                    $"Fresh Cane after ITEM${other:x2}, A-Cane={primary}, replace={replace}: active={_entities.Somaria.Active}, native blocks={rom.Blocks.Length}, parent=${rom[0xd201]:x2}, equips=${rom[0xc689]:x2}/${rom[0xc688]:x2}.");
            }
            if (other is TreasureId.Bombs or TreasureId.Shooter)
            {
                Clear();
                // Declared persistent byte: both source state0 handlers reject
                // empty ammunition only after A/B have reserved ParentItem2.
                int address = other == TreasureId.Bombs ? 0xc6b0 : 0xc6b9;
                if (other == TreasureId.Bombs) while (_inventory.TryConsumeBomb()) { }
                else while (_inventory.TryConsumeSelectedShooterSeed(out _)) { }
                rom[address] = 0;
                Step(1, 3, 3);
                FailIf(OtherActive() || _entities.Somaria!.Active || _entities.Somaria.Weapon != null,
                    $"Empty ITEM${other:x2} must still prevent same-update Cane initialization.");
                Step(); Step(1, button, button); Step(23);
                FailIf(rom.Blocks.Length != 1, "A later Cane press must use the empty parent's released slot.");
            }
        }
        GD.Print("Compared native Cane simultaneous/reserved parent priority, pending-creation replacement, rejected lower priority, empty Bomb/Shooter reservation, physical clearing and fresh recast across A/B and split/batched gameplay.");
    }
}
