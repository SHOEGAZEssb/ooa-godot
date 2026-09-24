using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownGelItemTiming()
    {
        foreach (bool batched in new[] { false, true })
        for (int repeat = 0; repeat < 2; repeat++)
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0x9d);
            _player.ApplicationUpdateOwned = true;
            _entities.Clear();
            _inventory.GiveTreasure(InventoryState.ItemSomaria, 1);
            _inventory.EquipA(InventoryState.ItemSomaria);
            _player.WarpTo(new(136, 128));
            _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position), "Gel input fixture requires actual Crown floor.");
            var gel = _entities.Spawn<GelCharacter>(new GelSpawn(_player.Position));
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(!gel.LinkContactPending || gel.IsAttached || gel.Counter1 != 0x60 ||
                _entities.PlayerSwordDisabled, "collisionEffect38 must defer stateC and the wccd8 writer.");
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
            var parent = _entities.Somaria!.Parent;
            FailIf(parent is null || !parent.Active || !gel.IsAttached || gel.Counter2 != 120 ||
                _entities.PlayerSwordDisabled, "Link can start Cane before the later Gel stateC initializes its latch.");
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(!_entities.PlayerSwordDisabled || gel.Counter2 != 119,
                "Only attached stateD publishes the next Link update's item restriction.");
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
            FailIf(!ReferenceEquals(parent, _entities.Somaria.Parent),
                "Published wccd8 must suppress a new Cane press while its existing parent advances.");
            var text = _entities.TextActiveSource;
            try
            {
                _entities.TextActiveSource = () => true;
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                FailIf(_entities.PlayerSwordDisabled || _entities.PlayerMovementDisabled,
                    "updateSpecialObjects clears last-update signals after Link even when Gel dispatch is frozen.");
            }
            finally { _entities.TextActiveSource = text; }
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
            FailIf(ReferenceEquals(parent, _entities.Somaria.Parent) || !_entities.PlayerSwordDisabled,
                "After the signal-free frozen update, Link can restart Cane before Gel republishes its restriction.");
        }
        ReinitializeGameplayForValidation();
    }
}
