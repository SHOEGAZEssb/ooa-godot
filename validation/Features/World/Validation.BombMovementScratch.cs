using Godot;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateBombMovementScratch()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batch in new[] { false, true })
        foreach (bool blocked in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xa1);
            _entities.Clear();
            _player.ApplicationUpdateOwned = true;
            var data = new BombDatabase().Data;
            Vector2? start = null;
            for (int y = 40; y < 128 && start is null; y++)
            for (int x = 24; x < 216; x++)
            {
                Vector2 point = new(x, y);
                Vector2 edge = point + new Vector2(3, 0);
                if (_collision.Collides(point - new Vector2(9, 0)) || _currentRoom.IsSolid(point)) continue;
                bool wall = _currentRoom.IsSolid(edge) && !data.CanPassSolidTile(_currentRoom, edge);
                if (wall != blocked || !blocked && _currentRoom.IsSolid(point + new Vector2(5, 0))) continue;
                start = point;
                break;
            }
            FailIf(start is null, "Crown4:a1 must provide the bomb throw geometry.");
            _player.WarpTo(start!.Value - new Vector2(9, 0));
            _player.Face(Vector2I.Right);
            var bomb = _entities.Spawn<BombEffect>(new BombSpawn(_player, data, 4, _ => { }));
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
                        $"ITEM$03 blocked={blocked} scratch ${0xcec0 + i:x4} differs in the item pass.");
                observations++;
            });
            typeof(RoomEntityManager).GetMethod("RegisterEnemySlot", flags)!.Invoke(_entities, [observer, 0]);
            typeof(RoomEntityManager).GetMethod("AddEntity", flags)!.Invoke(_entities, [observer]);
            StepGameplayUpdates(1, Vector2.Zero, batched: batch);
            bomb.Throw(_player, new(8, -8), Vector2I.Right, -240, 0x3c);
            expected = blocked ? [0, 0, 0, 0] : [0, 0, 0x80, 1];
            StepGameplayUpdates(1, Vector2.Zero, batched: batch);
            FailIf(observations != 2 || bomb.PrecisePosition != start.Value + (blocked ? Vector2.Zero : new Vector2(1.5f, 0)),
                $"Bomb throw must apply SPEED_180 only when its edge is clear: blocked={blocked}, observations={observations}, start={start.Value}, actual={bomb.PrecisePosition}.");
            if (blocked)
            {
                expected = [0xff, 0xa5, 0xa5, 0xa5];
                Stage();
            }
            StepGameplayUpdates(2, Vector2.Zero, batched: batch);
            FailIf(observations != 4 || blocked && bomb.ThrowDirection != Vector2I.Zero,
                "Stopped bombs must return before later velocity writes; moving bombs must write on each update.");
            if (!blocked)
            {
                bomb.Discard();
                bomb = _entities.Spawn<BombEffect>(new BombSpawn(_player, data, 4, _ => { }));
                expected = [0xa5, 0xa5, 0xa5, 0xa5];
                Stage();
                StepGameplayUpdates(1, Vector2.Zero, batched: batch);
                // Source bounceSpeedReductionMapping maps SPEED_020 ($05)
                // to SPEED_000 without clearing the angle.
                bomb.Throw(_player, new(8, -8), Vector2I.Right, -240, 0x05);
                expected = [0, 0, 0x20, 0];
                int updates = 0;
                while (bomb.SpeedRaw != 0 && updates++ < 40)
                    StepGameplayUpdates(1, Vector2.Zero, batched: batch);
                FailIf(bomb.State != BombState.Thrown || bomb.SpeedRaw != 0 ||
                    bomb.ThrowDirection != Vector2I.Right,
                    "First bounce to SPEED_000 must retain the active throw angle.");
                Vector2 stopped = bomb.PrecisePosition;
                for (int i = 0; i < 4; i++) _runtimeState.SetWramByte(0xcec0 + i, 0xa5);
                expected = [0, 0, 0, 0];
                int before = observations;
                StepGameplayUpdates(2, Vector2.Zero, batched: batch);
                FailIf(bomb.PrecisePosition != stopped || observations != before + 2,
                    "Zero-speed airborne bomb updates must publish zero without lateral movement.");
            }
        }
        ReinitializeGameplayForValidation();
    }
}
