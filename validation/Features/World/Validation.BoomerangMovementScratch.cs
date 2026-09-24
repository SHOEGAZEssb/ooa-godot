using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateBoomerangMovementScratch()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xa8);
            _entities.Clear();
            _player.ApplicationUpdateOwned = true;
            _inventory.GiveTreasure(InventoryState.ItemBoomerang, 2);
            _inventory.EquipA(InventoryState.ItemBoomerang);
            _player.WarpTo(new(72.25f, 80.5f));
            _player.Face(Vector2I.Right);
            for (int y = 64; y <= 96; y += 16)
            for (int x = 48; x <= 96; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), 0xa0, 0, 0);
            byte[] expected = [0xa5, 0xa5, 0xa5, 0xa5];
            for (int i = 0; i < 4; i++) _runtimeState.SetWramByte(0xcec0 + i, expected[i]);
            int observations = 0;
            var observer = new ItemPhaseValidationEntity(() =>
            {
                for (int i = 0; i < 4; i++)
                    FailIf(_runtimeState.ReadWramByte(0xcec0 + i) != expected[i],
                        $"ITEM$06 scratch ${0xcec0 + i:x4} differs after the item pass.");
                observations++;
            });
            typeof(RoomEntityManager).GetMethod("RegisterEnemySlot", flags)!.Invoke(_entities, [observer, 0]);
            typeof(RoomEntityManager).GetMethod("AddEntity", flags)!.Invoke(_entities, [observer]);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batch);
            var item = _entities.Entities<BoomerangItem>().Single();
            FailIf(item.State != 1 || item.PrecisePosition != _player.PrecisePosition,
                "Boomerang setup must preserve scratch and the copied fractional position.");
            expected = [0, 0, 0xa0, 1]; // SPEED_1a0 right.
            StepGameplayUpdates(2, Vector2.Zero, batched: batch);
            FailIf(item.PrecisePosition != new Vector2(75.5f, 80.5f),
                "Boomerang movement must publish and apply SPEED_1a0 on each update.");
            item.QueueCollision();
            expected = [0, 0, 0x60, 0xfe]; // Negated SPEED_1a0 on return.
            StepGameplayUpdates(2, Vector2.Zero, batched: batch);
            FailIf(item.State != 3 || item.PrecisePosition != _player.PrecisePosition,
                "Returning states must write their updated angle's vector on both updates.");
            expected = [0xff, 0, 0x60, 0xfe];
            StepGameplayUpdates(2, Vector2.Zero, batched: batch);
            FailIf(item.State != 4 || observations != 7,
                "Catch and position-copy updates must not execute movement writes.");
            _entities.ClearPhysicalPlayerItems();
            StepGameplayUpdates(1, Vector2.Zero, batched: batch);
            FailIf(observations != 8, "Cancelled boomerang must preserve scratch on the next update.");
        }
        ReinitializeGameplayForValidation();
    }
}
