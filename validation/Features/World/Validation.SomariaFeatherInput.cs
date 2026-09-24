using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSomariaFeatherInput()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool canePrimary in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xa8);
            _entities.Clear();
            _player.ApplicationUpdateOwned = true;
            _inventory.GiveTreasure(InventoryState.ItemSomaria, 1);
            _inventory.GiveTreasure(TreasureDatabase.TreasureFeather, 0);
            _inventory.EquipA(canePrimary ? InventoryState.ItemSomaria : InventoryState.ItemFeather);
            _inventory.EquipB(canePrimary ? InventoryState.ItemFeather : InventoryState.ItemSomaria);
            for (int y = 8; y < 176; y += 16)
            for (int x = 8; x < 240; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), 0xa0, 0, 0);
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                _player.WarpTo(new(72, 72));
                _player.Face(Vector2I.Down);
                Step();
                StepGameplayUpdates(1, Vector2.Zero, ["attack", "item"], ["attack", "item"]);
                FailIf(!_player.TopDownAirborne || !_player.IsUsingSomaria || _entities.Somaria!.Weapon?.State != 1,
                    "Cane$04 selector3 and Feather$17 selector1 must coexist for either A/B assignment.");
                Step(13);
                FailIf(!_player.TopDownAirborne || _player.TopDownAirZ >= -4 || !_player.IsUsingSomaria ||
                    _entities.Somaria!.Weapon!.State != 1,
                    "Jump and Cane counters advance independently through the pre-creation update.");
                Step();
                var block = _entities.Entities<SomariaBlock>().Single();
                FailIf(_entities.Somaria!.Weapon!.State != 2 || block.State != 1 || block.ZHigh != _player.TopDownAirZ,
                    "Cane update14 allocates the phase-in child with copied jump height before testing solid placement.");
                Step(4);
                FailIf(_player.IsUsingSomaria || !_player.TopDownAirborne,
                    "Cane completion releases its parent without cancelling the jump.");
                Step(5);
                FailIf(!block.Finished || _entities.Entities<SomariaBlock>().Any(),
                    "Phase-in completion rejects the retained airborne height and deletes with a puff.");
                Step(30);
                FailIf(_player.TopDownAirborne || _player.IsUsingSomaria || _entities.Entities<SomariaBlock>().Any(),
                    "Landing must not retry the airborne Cane's failed block creation.");
            }
            _inventory.GiveTreasure(InventoryState.ItemSword, 0);
            _inventory.EquipA(InventoryState.ItemSword);
            _inventory.EquipB(InventoryState.ItemSomaria);
            Step();
            StepGameplayUpdates(1, Vector2.Zero, ["attack", "item"], ["attack", "item"]);
            FailIf(!_player.IsAttacking || _player.IsUsingSomaria,
                "Checking both buttons must retain sword$05 priority$60 over Cane$04 priority$00 in ParentItem2.");
        }
    }
}
