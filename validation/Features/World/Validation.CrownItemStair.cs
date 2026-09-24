using Godot;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownItemStair()
    {
        foreach (int item in new[] { InventoryState.ItemSomaria, InventoryState.ItemShooter, InventoryState.ItemSwitchHook })
        foreach (bool primary in new[] { false, true })
        foreach (bool alreadyActive in new[] { false, true })
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xa1);
            _player.ApplicationUpdateOwned = true;
            _entities.Clear();
            _inventory.GiveTreasure(item, 1);
            _inventory.GiveTreasure(0x20, 0x20);
            _inventory.SelectShooterSeeds(0);
            _inventory.EquipA(primary ? item : InventoryState.ItemNone);
            _inventory.EquipB(primary ? InventoryState.ItemNone : item);
            _runtimeState.SetWramByte(OracleRuntimeState.WarpsDisabledAddress, 1);
            _player.WarpTo(new(120, 40));
            FailIf(_collision.Collides(_player.Position), "Item/stair approach must begin on actual Crown floor.");
            StepGameplayUpdates(15, Vector2.Up, batched: batched);
            FailIf(IsTransitioning || _player.Position != new Vector2(120, 25),
                "The native warp lock must allow movement onto the stair without taking it.");
            string button = primary ? "attack" : "item";
            _player.Face(Vector2I.Down); // Keep a flying hook over the actual approach floor.
            bool ItemActive() => item == InventoryState.ItemSomaria ? _player.IsUsingSomaria :
                item == InventoryState.ItemShooter ? _player.IsUsingSeedShooter : _player.IsUsingSwitchHook;
            if (alreadyActive)
            {
                StepGameplayUpdates(1, Vector2.Zero, [button], [button]);
                StepGameplayUpdates(3, Vector2.Zero, [button], batched: batched);
                FailIf(!ItemActive() || IsTransitioning, "The item must remain active while the separate warp lock is held.");
            }
            bool observed = false;
            var observer = new ItemPhaseValidationEntity(() =>
            {
                observed = true;
                FailIf(!ItemActive(), "The item must run before the post-object stair check.");
            });
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(RoomEntityManager).GetMethod("RegisterEnemySlot", flags)!.Invoke(_entities, [observer, 0]);
            typeof(RoomEntityManager).GetMethod("AddEntity", flags)!.Invoke(_entities, [observer]);
            _runtimeState.SetWramByte(OracleRuntimeState.WarpsDisabledAddress, 0);
            int sounds = _sound.PlayRequestsFor(OracleSoundEngine.SndEnterCave);
            StepGameplayUpdates(1, Vector2.Zero, [button], alreadyActive ? [] : [button]);
            FailIf(!observed || !IsTransitioning ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndEnterCave) != sounds + 1,
                $"ITEM${item:x2} {(alreadyActive ? "active" : "initial")} update must permit the post-object Crown stair check on button {button}.");
        }
        ReinitializeGameplayForValidation();
    }
}
