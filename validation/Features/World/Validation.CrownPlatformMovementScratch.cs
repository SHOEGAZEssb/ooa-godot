using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownPlatformMovementScratch()
    {
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(6, 0x95);
            _player.WarpTo(new(16, 16));
            _player.BeginCutsceneControl();
            // Isolate interaction writes from the room's moving enemies.
            _entities.NonInteractionObjectsDisabledSource = () => true;
            var platform = _entities.Entities<MovingSideScrollPlatformRoomEntity>().Single();
            byte[] expected = [0xff, 0xa5, 0xa5, 0xa5];
            void Stage()
            {
                for (int i = 0; i < 4; i++)
                    _runtimeState.SetWramByte(0xcec0 + i, i == 0 ? (byte)0xff : (byte)0xa5);
            }
            int observations = 0;
            var observer = new ItemPhaseValidationEntity(() =>
            {
                for (int i = 0; i < 4; i++)
                    FailIf(_runtimeState.ReadWramByte(0xcec0 + i) != expected[i],
                        $"INTERAC$a1:$0a update {observations}: scratch ${0xcec0 + i:x4} expected ${expected[i]:x2}, got ${_runtimeState.ReadWramByte(0xcec0 + i):x2}.");
                observations++;
                Stage();
            });
            typeof(RoomEntityManager).GetMethod("AddEntity",
                BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_entities, [observer]);
            Stage();
            StepGameplayUpdates(1, Vector2.Zero, batched: batch);
            // Source script0a: SPEED_080, up to $38, down to $88, repeat.
            expected = [0x80, 0xff, 0, 0];
            StepGameplayUpdates(95, Vector2.Zero, batched: batch);
            FailIf(platform.PrecisePosition != new Vector2(136, 56.5f),
                "Platform upward movement must retain its final half-pixel.");
            expected = [0xff, 0xa5, 0xa5, 0xa5];
            StepGameplayUpdates(1, Vector2.Zero, batched: batch);
            FailIf(platform.CommandIndex != 1, "Platform endpoint must select down without moving.");
            expected = [0x80, 0, 0, 0];
            StepGameplayUpdates(159, Vector2.Zero, batched: batch);
            FailIf(platform.PrecisePosition != new Vector2(136, 136),
                "Platform downward movement must reach source endpoint $88.");
            expected = [0xff, 0xa5, 0xa5, 0xa5];
            StepGameplayUpdates(1, Vector2.Zero, batched: batch);
            expected = [0x80, 0xff, 0, 0];
            StepGameplayUpdates(2, Vector2.Zero, batched: batch);
            FailIf(platform.PrecisePosition != new Vector2(136, 135) || observations != 259,
                "Platform repeat must resume source upward movement on the next update.");
        }
        ReinitializeGameplayForValidation();
    }
}
