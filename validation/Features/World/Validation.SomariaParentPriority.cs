using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSomariaParentPriority()
    {
        // parentItemUsage.s checks A then B before updating ParentItem2.
        // itemUsageParameterTable: Cane$04=$03; Shovel$15=$13, Bomb$03=$23,
        // Shooter$0f=$43, Sword$05=$63 and Switch Hook$0a=$73.
        foreach (int other in new[] { TreasureId.Sword, TreasureId.Bombs,
            TreasureId.Shovel, TreasureId.Shooter, TreasureId.SwitchHook })
        foreach (bool canePrimary in new[] { false, true })
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xa8);
            _entities.Clear();
            _player.ApplicationUpdateOwned = true;
            _inventory.GiveTreasure(TreasureId.CaneOfSomaria, 1);
            _inventory.GiveTreasure(other, other == TreasureId.Bombs ? 0x10 : 1);
            _inventory.GiveTreasure(TreasureId.EmberSeeds, 5);
            _inventory.EquipA(canePrimary ? TreasureId.CaneOfSomaria : other);
            _inventory.EquipB(canePrimary ? other : TreasureId.CaneOfSomaria);
            for (int y = 8; y < 176; y += 16)
            for (int x = 8; x < 240; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), 0xa0, 0, 0);
            string caneButton = canePrimary ? "attack" : "item";
            string otherButton = canePrimary ? "item" : "attack";
            bool OtherActive() => other switch
            {
                TreasureId.Sword => _player.IsAttacking,
                TreasureId.Bombs => _bomb.Active,
                TreasureId.Shovel => _player.IsUsingShovel,
                TreasureId.Shooter => _player.IsUsingSeedShooter,
                TreasureId.SwitchHook => _player.IsUsingSwitchHook,
                _ => false
            };
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            foreach (bool replace in new[] { false, true })
            {
                _entities.ClearPhysicalPlayerItems();
                _player.WarpTo(new(72, 72));
                _player.Face(Vector2I.Down);
                Step();
                if (replace)
                {
                    StepGameplayUpdates(1, Vector2.Zero, [caneButton], [caneButton]);
                    Step(12);
                    FailIf(!_player.IsUsingSomaria || _entities.Entities<SomariaBlock>().Any(),
                        "Cane must remain before its creation boundary when replaced.");
                }
                StepGameplayUpdates(1, Vector2.Zero,
                    replace ? [otherButton] : ["attack", "item"],
                    replace ? [otherButton] : ["attack", "item"]);
                FailIf(!OtherActive() || _player.IsUsingSomaria || _entities.Somaria!.Weapon is not null ||
                    _entities.Entities<SomariaBlock>().Any(),
                    $"ParentItem2 item${other:x2} must win over Cane in either button order (replace={replace}).");
                StepGameplayUpdates(1, Vector2.Zero, [otherButton, caneButton], [caneButton]);
                FailIf(!OtherActive() || _player.IsUsingSomaria,
                    $"Cane priority$00 must not replace active item${other:x2}.");
                _entities.ClearPhysicalPlayerItems();
                _player.WarpTo(new(72, 72));
                Step(40);
                StepGameplayUpdates(1, Vector2.Zero, [caneButton], [caneButton]);
                Step(23);
                FailIf(_player.IsUsingSomaria || _entities.Entities<SomariaBlock>().Count() != 1,
                    $"After item${other:x2} clears, a new Cane use must complete exactly one block.");
            }
            if (other is TreasureId.Bombs or TreasureId.Shooter)
            {
                while (_inventory.TryConsumeBomb()) { }
                while (_inventory.TryConsumeSelectedShooterSeed(out _)) { }
                _entities.ClearPhysicalPlayerItems();
                _player.WarpTo(new(72, 72));
                Step();
                StepGameplayUpdates(1, Vector2.Zero, ["attack", "item"], ["attack", "item"]);
                FailIf(OtherActive() || _player.IsUsingSomaria || _entities.Somaria!.Weapon is not null,
                    $"Empty item${other:x2} must reserve ParentItem2 before its state0 clears it; Cane cannot run in that same update.");
                Step();
                StepGameplayUpdates(1, Vector2.Zero, [caneButton], [caneButton]);
                Step(23);
                FailIf(_entities.Entities<SomariaBlock>().Count() != 1,
                    "A later Cane press can use the slot released by the empty item.");
            }
        }
    }
}
