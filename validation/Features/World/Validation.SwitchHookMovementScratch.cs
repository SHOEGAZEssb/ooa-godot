using Godot;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSwitchHookMovementScratch()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batch in new[] { false, true })
        foreach (int level in new[] { 1, 2 })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xa8);
            _entities.Clear();
            _player.ApplicationUpdateOwned = true;
            _inventory.GiveTreasure(TreasureDatabase.TreasureSwitchHook, level);
            _inventory.EquipA(InventoryState.ItemSwitchHook);
            _player.WarpTo(new(120, 80));
            _player.Face(Vector2I.Up);
            for (int y = 48; y <= 96; y += 16)
            for (int x = 96; x <= 128; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), 0xa0, 0, 0);
            byte[] expected = [0xa5, 0xa5, 0xa5, 0xa5];
            void Stage()
            {
                for (int i = 0; i < 4; i++) _runtimeState.SetWramByte(0xcec0 + i, expected[i]);
            }
            Stage();
            int observations = 0;
            var observer = new ItemPhaseValidationEntity(() =>
            {
                for (int i = 0; i < 4; i++)
                    FailIf(_runtimeState.ReadWramByte(0xcec0 + i) != expected[i],
                        $"ITEM$0a level {level}: scratch ${0xcec0 + i:x4} differs after item movement.");
                observations++;
            });
            typeof(RoomEntityManager).GetMethod("RegisterEnemySlot", flags)!.Invoke(_entities, [observer, 0]);
            typeof(RoomEntityManager).GetMethod("AddEntity", flags)!.Invoke(_entities, [observer]);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batch);
            var hook = _entities.SwitchHook!.Item!;
            FailIf(hook.State != 1 || hook.Position != new Vector2(120, 81),
                "Hook setup must apply source (+0,+1) offset without executing movement.");
            int speed = level == 1 ? 2 : 3;
            expected = [0, (byte)(256 - speed), 0, 0];
            StepGameplayUpdates(2, Vector2.Zero, batched: batch);
            FailIf(hook.Position != new Vector2(120, 81 - speed * 2),
                "Extending hook must execute source SPEED_200/SPEED_300 on each update.");
            hook.NotifyObjectCollision();
            expected = [0xff, 0xa5, 0xa5, 0xa5];
            Stage();
            StepGameplayUpdates(1, Vector2.Zero, batched: batch);
            FailIf(hook.State != 2 || hook.Substate != 0,
                "Object-hit retraction transition must return without a movement write.");
            expected = [0, (byte)speed, 0, 0];
            StepGameplayUpdates(1, Vector2.Zero, batched: batch);
            FailIf(hook.Substate != 1, "Returning hook must move before its catch-range check.");
            expected = [0xff, (byte)speed, 0, 0];
            StepGameplayUpdates(2, Vector2.Zero, batched: batch);
            FailIf(observations != 7 || hook.Finished,
                "Retracted position-copy updates must preserve movement scratch through the three-update delay.");
        }
        ReinitializeGameplayForValidation();
    }
}
