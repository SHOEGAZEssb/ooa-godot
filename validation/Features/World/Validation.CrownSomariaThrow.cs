using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownSomariaThrow()
    {
        foreach (bool batched in new[] { false, true })
        for (int repeat = 0; repeat < 2; repeat++)
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xa1);
            _player.ApplicationUpdateOwned = true;
            var target = _entities.Entities<KeeseCharacter>().First();
            foreach (var enemy in _entities.Entities<KeeseCharacter>())
            {
                enemy.Position = new(24, 24);
                enemy.InvincibilityCounter = 127;
            }
            _inventory.GiveTreasure(InventoryState.ItemSomaria, 1);
            _inventory.GiveTreasure(TreasureDatabase.TreasureBracelet, 1);
            _inventory.EquipA(InventoryState.ItemSomaria);
            _player.WarpTo(new(120, 56));
            _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position), "Throw fixture must start on actual Crown floor.");
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
            StepGameplayUpdates(24, Vector2.Zero, batched: batched);
            var block = _entities.EntityAdapters<SomariaBlockRoomEntity>().Single().Block;
            _inventory.EquipA(InventoryState.ItemBracelet);
            StepGameplayUpdates(8, Vector2.Up, batched: batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
            StepGameplayUpdates(30, Vector2.Zero, ["attack"], batched: batched);
            FailIf(!block.IsHeld || !_player.IsCarryingObject || _player.BraceletLiftCollisionsDisabled,
                "Normal Cane/Bracelet inputs must finish lifting the block.");
            _player.Face(Vector2I.Down);
            StepGameplayUpdates(1, Vector2.Zero);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
            FailIf(block.IsHeld || block.State != 2 || block.Substate != 2 || (block.Collision & 0x7f) != 0x15,
                "Throwing ITEM$18 must retain Somaria collision$15 rather than generic thrown-object$16.");
            target.Health = 9;
            int updates = 0;
            while (!block.Finished && target.Health == 9 && updates++ < 80)
            {
                // Stage the room's mobile Keese on the descending block's
                // path, after it is low enough for the native Z overlap.
                if (block.ZHigh >= -7)
                {
                    target.Position = block.Position + Vector2.Down * 2;
                    target.InvincibilityCounter = 0;
                }
                StepGameplayUpdates(1, Vector2.Zero);
            }
            FailIf(block.Finished || target.Health != 5 || target.InvincibilityCounter != 21 ||
                target.KnockbackCounter != 11 || block.Health != 9 || block.DamageToApply != -4,
                "A normal Somaria throw must use effect$2f and leave raw incoming damage queued in flight.");
            var text = _entities.TextActiveSource;
            try
            {
                _entities.TextActiveSource = () => true;
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                FailIf(block.Finished || block.Health != 9 || block.DamageToApply != -4,
                    "Text freeze must retain the thrown block and pending damage.");
            }
            finally { _entities.TextActiveSource = text; }
            for (int i = 0; !block.Finished && i < 80; i++)
            {
                StepGameplayUpdates(1, Vector2.Zero);
                FailIf(block.Health != 9 || block.DamageToApply != -4 || target.Health != 5,
                    "Throw substates omit itemUpdateDamageToApply: queued damage must remain unconsumed through landing.");
            }
            FailIf(!block.Finished, "The thrown block must finish its normal landing lifecycle.");
        }
        ReinitializeGameplayForValidation();
    }
}
