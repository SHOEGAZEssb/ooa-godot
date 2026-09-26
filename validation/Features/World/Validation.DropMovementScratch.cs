using Godot;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateDropMovementScratch()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batch in new[] { false, true })
        foreach (int mode in new[] { 0, 1, 2 })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xa1);
            _entities.Clear();
            _player.ApplicationUpdateOwned = true;
            _player.WarpTo(new(120, 40));
            Vector2? start = null;
            for (int y = 56; y < 120 && start is null; y += 8)
            for (int x = 32; x < 200; x += 8)
            {
                Vector2 point = new(x, y);
                if (point.DistanceTo(_player.Position) > 24 && !_currentRoom.IsSolid(point) &&
                    !_currentRoom.IsSolid(point + new Vector2(4, 0)) &&
                    !_currentRoom.IsSolid(point + new Vector2(6, 0)))
                { start = point; break; }
            }
            FailIf(start is null, "Crown4:a1 must provide clear item-drop floor.");
            var drop = _entities.Spawn<ItemDropEffect>(new ItemDropSpawn(
                mode == 2 ? ItemDropDatabase.Fairy : ItemDropDatabase.OneRupee,
                start!.Value, Angle: ObjectAngle.Right, DugUp: mode == 1));
            byte[] expected = [0xa5, 0xa5, 0xa5, 0xa5];
            void Stage()
            {
                for (int i = 0; i < 4; i++) _runtimeState.SetWramByte(0xcec0 + i, expected[i]);
            }
            Stage();
            int observations = 0;
            // Unregistered logical interaction observes after the PART pass.
            var observer = new ItemPhaseValidationEntity(() =>
            {
                for (int i = 0; i < 4; i++)
                    FailIf(_runtimeState.ReadWramByte(0xcec0 + i) != expected[i],
                        $"PART$01 mode {mode}: scratch ${0xcec0 + i:x4} differs after movement.");
                observations++;
            });
            typeof(RoomEntityManager).GetMethod("AddEntity", flags)!.Invoke(_entities, [observer]);
            StepGameplayUpdates(1, Vector2.Zero, batched: batch);
            if (mode == 2)
            {
                // Isolate a source-valid fairy velocity after normal RNG setup.
                typeof(ItemDropEffect).GetField("_angle", flags)!.SetValue(drop, 8);
                typeof(ItemDropEffect).GetField("_speed", flags)!.SetValue(drop, 0x28);
            }
            expected = mode switch { 0 => [0, 0, 0, 0], 1 => [0, 0, 0xa0, 0], _ => [0, 0, 0, 1] };
            StepGameplayUpdates(2, Vector2.Zero, batched: batch);
            float distance = mode switch { 0 => 0, 1 => 1.25f, _ => 2 };
            FailIf(observations != 3 || drop.PrecisePosition != start.Value + new Vector2(distance, 0),
                "Bouncing drops must publish source zero/SPEED_0a0/SPEED_100 vectors.");
            if (mode == 2)
            {
                typeof(ItemDropEffect).GetField("_state", flags)!.SetValue(drop, DropState.Grounded);
                typeof(ItemDropEffect).GetField("_counter", flags)!.SetValue(drop, 240);
                typeof(ItemDropEffect).GetField("_fairyMovementCounter", flags)!.SetValue(drop, 1);
                expected = [0xff, 0xa5, 0xa5, 0xa5];
                Stage();
                StepGameplayUpdates(1, Vector2.Zero, batched: batch);
                FailIf(observations != 4 || drop.FairyMovementCounter < 8,
                    "Fairy counter expiry must choose another direction without executing movement.");
            }
        }
        ReinitializeGameplayForValidation();
    }
}
