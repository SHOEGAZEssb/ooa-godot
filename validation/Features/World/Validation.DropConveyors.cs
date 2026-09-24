using Godot;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateDropConveyors()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Vector2[] directions = [Vector2.Up, Vector2.Right, Vector2.Down, Vector2.Left];
        byte[][] velocities = [[0x80, 0xff, 0, 0], [0, 0, 0x80, 0], [0x80, 0, 0, 0], [0, 0, 0x80, 0xff]];
        foreach (bool batch in new[] { false, true })
        for (int direction = 0; direction < 4; direction++)
        foreach (int gate in new[] { 0, 1, 2 })
        {
            if (gate == 2 && direction != 1) continue;
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xa1);
            _entities.Clear();
            _player.ApplicationUpdateOwned = true;
            _player.WarpTo(new(120, 40));
            // Controlled terrain fixture: source dungeon conveyor tiles $54-$57.
            for (int y = 64; y <= 112; y += 16)
            for (int x = 96; x <= 144; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), 0xa0, 0, 0);
            Vector2 start = new(127, 88);
            _currentRoom.SetPositionTileAndCollision(start, (byte)(0x54 + direction), 0, 0);
            if (gate == 2) _currentRoom.SetPositionTileAndCollision(new(136, 88), 0x01, 0xff, 0);
            var drop = _entities.Spawn<ItemDropEffect>(new ItemDropSpawn(ItemDropDatabase.OneRupee, start));
            StepGameplayUpdates(1, Vector2.Zero, batched: batch);
            typeof(ItemDropEffect).GetField("_state", flags)!.SetValue(drop, DropState.Grounded);
            typeof(ItemDropEffect).GetField("_counter", flags)!.SetValue(drop, 240);
            typeof(ItemDropEffect).GetField("_zFixed", flags)!.SetValue(drop, gate == 1 ? -256 : 0);
            byte[] expected = gate == 0 ? velocities[direction] : [0xff, 0xa5, 0xa5, 0xa5];
            for (int i = 0; i < 4; i++) _runtimeState.SetWramByte(0xcec0 + i, i == 0 ? (byte)0xff : (byte)0xa5);
            int observations = 0;
            var observer = new ItemPhaseValidationEntity(() =>
            {
                for (int i = 0; i < 4; i++)
                    FailIf(_runtimeState.ReadWramByte(0xcec0 + i) != expected[i],
                        $"Drop conveyor direction={direction}, gate={gate}, observation={observations}: scratch ${0xcec0 + i:x4} expected ${expected[i]:x2}, got ${_runtimeState.ReadWramByte(0xcec0 + i):x2}.");
                observations++;
            });
            typeof(RoomEntityManager).GetMethod("AddEntity", flags)!.Invoke(_entities, [observer]);
            StepGameplayUpdates(2, Vector2.Zero, batched: batch);
            FailIf(observations != 2 || drop.PrecisePosition != start + (gate == 0 ? directions[direction] : Vector2.Zero),
                "Drop conveyors must move at SPEED_080 only after height and forward-collision gates.");
            FailIf(drop.Angle != 0 || drop.Speed != 0,
                "objectApplyGivenSpeed must preserve the drop's own angle and speed.");
            if (gate == 0 && direction == 1)
            {
                _currentRoom.SetPositionTileAndCollision(drop.Position + new Vector2(0, 5), 0xf3, 0, 0);
                // The removed PART creates INTERAC$0f before the interaction
                // pass. It moves right toward x=$88 at SPEED_060.
                expected = [0, 0, 0x60, 0];
                for (int i = 0; i < 4; i++) _runtimeState.SetWramByte(0xcec0 + i, 0xa5);
                StepGameplayUpdates(2, Vector2.Zero, batched: batch);
                FailIf(!drop.Finished || drop.FinishedHazard != HazardType.Hole || observations != 4,
                    "Grounded drops must be removed by a hole before another conveyor write.");
            }
        }
        ReinitializeGameplayForValidation();
    }
}
