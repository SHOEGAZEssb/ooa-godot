using Godot;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSwordBeamScratch()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Vector2[] offsets = [new(-4, -11), new(12, 0), new(3, 10), new(-13, 0)];
        Vector2[] directions = [Vector2.Up, Vector2.Right, Vector2.Down, Vector2.Left];
        byte[][] velocities = [[0, 0xfd, 0, 0], [0, 0, 0, 3], [0, 3, 0, 0], [0, 0, 0, 0xfd]];
        foreach (bool batch in new[] { false, true })
        for (int direction = 0; direction < 4; direction++)
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xa1);
            _entities.Clear();
            _player.ApplicationUpdateOwned = true;
            _player.WarpTo(new(120, 40));
            Vector2? start = null;
            for (int y = 24; y < 104 && start is null; y += 8)
            for (int x = 24; x < 136; x += 8)
            {
                Vector2 point = new(x, y);
                bool clear = true;
                for (int step = 0; step <= 3; step++)
                    clear &= !_currentRoom.IsSolid(point + directions[direction] * step * 3);
                if (clear) { start = point; break; }
            }
            FailIf(start is null, "Crown4:a1 must provide a clear sword-beam path.");
            var beam = _entities.Spawn<SwordBeamEffect>(new SwordBeamSpawn(start!.Value - offsets[direction], direction));
            byte[] expected = [0xa5, 0xa5, 0xa5, 0xa5];
            for (int i = 0; i < 4; i++) _runtimeState.SetWramByte(0xcec0 + i, expected[i]);
            int observations = 0;
            var observer = new ItemPhaseValidationEntity(() =>
            {
                for (int i = 0; i < 4; i++)
                    FailIf(_runtimeState.ReadWramByte(0xcec0 + i) != expected[i],
                        $"ITEM$27 direction {direction:x2} scratch ${0xcec0 + i:x4} differs before the enemy pass.");
                observations++;
            });
            typeof(RoomEntityManager).GetMethod("RegisterEnemySlot", flags)!.Invoke(_entities, [observer, 0]);
            typeof(RoomEntityManager).GetMethod("AddEntity", flags)!.Invoke(_entities, [observer]);
            StepGameplayUpdates(1, Vector2.Zero, batched: batch);
            FailIf(!beam.Initialized || beam.Position != start.Value, "Sword-beam setup must not move or publish velocity.");
            expected = velocities[direction];
            StepGameplayUpdates(2, Vector2.Zero, batched: batch);
            FailIf(beam.Finished || beam.Position != start.Value + directions[direction] * 6,
                "Each moving sword-beam update must execute source SPEED_300.");
            beam.QueueNativeCollision();
            expected = [0xff, 0xa5, 0xa5, 0xa5];
            for (int i = 0; i < 4; i++) _runtimeState.SetWramByte(0xcec0 + i, expected[i]);
            StepGameplayUpdates(2, Vector2.Zero, batched: batch);
            FailIf(!beam.Finished || observations != 5,
                "Pending object collision must delete the beam before movement; deleted beams must preserve scratch.");
        }
        ReinitializeGameplayForValidation();
    }
}
