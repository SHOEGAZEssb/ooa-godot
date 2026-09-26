using Godot;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateFallingHoleMovementScratch()
    {
        foreach (bool batch in new[] { false, true })
        foreach (bool centered in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xa1);
            _entities.Clear();
            _player.ApplicationUpdateOwned = true;
            _player.WarpTo(new(120, 40));
            void Stage()
            {
                for (int i = 0; i < 4; i++) _runtimeState.SetWramByte(0xcec0 + i, 0xa5);
                _runtimeState.SetWramByte(WramAddress.wTmpcec0, 0xff);
            }
            Stage();
            var effect = _entities.Spawn<FallingDownHoleEffect>(
                new FallingDownHoleSpawn(new(centered ? 88.25f : 86, 88)));
            FailIf(_runtimeState.ReadWramByte(0xcec1) != 0xa5,
                "INTERAC$0f initialization must not call objectApplySpeed.");
            int observations = 0;
            var observer = new ItemPhaseValidationEntity(() =>
            {
                // fallDownHole.s compares position high bytes before moving.
                // SPEED_060 reaches x=$58.40 on update six and stops there.
                byte[] expected = !centered && observations < 6
                    ? [0, 0, 0x60, 0] : [0xff, 0xa5, 0xa5, 0xa5];
                for (int i = 0; i < 4; i++)
                    FailIf(_runtimeState.ReadWramByte(0xcec0 + i) != expected[i],
                        $"INTERAC$0f scratch ${0xcec0 + i:x4}, update {observations + 1}.");
                observations++;
                Stage();
            });
            typeof(RoomEntityManager).GetMethod("AddEntity",
                BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_entities, [observer]);
            StepGameplayUpdates(32, Vector2.Zero, batched: batch);
            FailIf(effect.PrecisePosition != new Vector2(88.25f, 88) || effect.Finished ||
                (effect.CurrentParameter & 0x80) == 0,
                "INTERAC$0f must center by high byte and reach its terminal frame after 8+12+12 updates.");
            StepGameplayUpdates(2, Vector2.Zero, batched: batch);
            FailIf(!effect.Finished || observations != 34,
                "INTERAC$0f terminal deletion and later updates must preserve movement scratch.");
        }
        ReinitializeGameplayForValidation();
    }
}
