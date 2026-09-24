using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownSomariaHeldDamage()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool lifting in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xa1);
            _player.ApplicationUpdateOwned = true;
            _entities.Clear();
            _inventory.GiveTreasure(InventoryState.ItemSomaria, 1);
            _inventory.GiveTreasure(TreasureDatabase.TreasureBracelet, 1);
            _player.WarpTo(new(120, 56));
            _player.Face(Vector2I.Up);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                _player.WarpTo(new(120, 56));
                _inventory.EquipA(InventoryState.ItemSomaria);
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
                StepGameplayUpdates(24, Vector2.Zero, batched: batched);
                var block = _entities.EntityAdapters<SomariaBlockRoomEntity>().Single().Block;
                _inventory.EquipA(InventoryState.ItemBracelet);
                StepGameplayUpdates(8, Vector2.Up, batched: batched);
                FailIf(_collision.Collides(_player.Position), "Held-damage fixture must approach on actual Crown floor.");
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
                if (!lifting) StepGameplayUpdates(30, Vector2.Zero, ["attack"], batched: batched);
                FailIf(!block.IsHeld || !_player.IsCarryingObject || block.Substate != 1 ||
                    _player.BraceletLiftCollisionsDisabled != lifting,
                    "Normal input must establish the requested lifting/carrying phase.");
                // Inject the collision pass's raw damage result; collision
                // eligibility itself is covered by the live enemy regressions.
                foreach (int health in new[] { 5, 1 })
                {
                    block.QueueEnemyDamage(0xfc, 1, block.Position);
                    StepGameplayUpdates(1, Vector2.Zero);
                    FailIf(block.Health != health || block.DamageToApply != 0 || !block.IsHeld ||
                        !_player.IsCarryingObject, "Nonlethal damage must be consumed once without dropping the block.");
                }
                block.QueueEnemyDamage(0xfc, 1, block.Position);
                var text = _entities.TextActiveSource;
                try
                {
                    _entities.TextActiveSource = () => true;
                    StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                    FailIf(block.Health != 1 || block.DamageToApply != -4 || !block.IsHeld,
                        "Text freeze must preserve lethal pending damage and the current grab.");
                }
                finally { _entities.TextActiveSource = text; }
                StepGameplayUpdates(1, Vector2.Zero);
                FailIf(!block.Finished || block.Health != 0xfd || _player.IsCarryingObject ||
                    _player.BraceletLiftCollisionsDisabled != lifting,
                    "ITEM$18 must drop the grab when health wraps below zero; the earlier parent dispatch keeps its lift mask this update.");
                StepGameplayUpdates(1, Vector2.Zero);
                FailIf(_player.BraceletLiftCollisionsDisabled || _player.BraceletEntityOffset is not null ||
                    _entities.EntityAdapters<SomariaBlockRoomEntity>().Any(),
                    "The next Bracelet parent update must observe the cleared grab and release its remaining lift state.");
            }
        }
        ReinitializeGameplayForValidation();
    }
}
