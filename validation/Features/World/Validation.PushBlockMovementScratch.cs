using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePushBlockMovementScratch()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0x9e);
            _player.ApplicationUpdateOwned = true;
            _player.WarpTo(new(120, 120));
            for (int update = 0; !_pushBlocks.Active && update < 80; update++)
                StepGameplayUpdates(1, Vector2.Up);
            FailIf(!_pushBlocks.Active || _entities.Entities<PushBlockController>().Count != 1,
                "Crown's source statues must start through an actual floor approach and push.");
            var partner = _entities.Entities<PushBlockController>().Single();
            void Stage()
            {
                for (int i = 0; i < 4; i++) _runtimeState.SetWramByte(0xcec0 + i, 0xa5);
            }
            Stage();
            int observations = 0;
            var observer = new ItemPhaseValidationEntity(() =>
            {
                FailIf(_runtimeState.ReadWramByte(WramAddress.wTmpcec0) != 0x80 ||
                    _runtimeState.ReadWramByte(0xcec1) != 0xff ||
                    _runtimeState.ReadWramByte(0xcec2) != 0 ||
                    _runtimeState.ReadWramByte(0xcec3) != 0,
                    "INTERAC$14 must publish the source upward SPEED_080 vector during the interaction pass.");
                observations++;
            });
            typeof(RoomEntityManager).GetMethod("AddEntity", flags)!.Invoke(_entities, [observer]);
            StepGameplayUpdates(2, Vector2.Zero, batched: batch);
            FailIf(observations != 2, "Both moving push-block updates must run.");
            // Each synchronized child owns its movement dispatch and must
            // keep writing even when the reserved parent object is cancelled.
            _pushBlocks.Cancel();
            Stage();
            StepGameplayUpdates(29, Vector2.Zero, batched: batch);
            FailIf(observations != 31 || partner.Active || _entities.Entities<PushBlockController>().Count != 0,
                "The synchronized child must publish velocity through movement update32 independently of the reserved block.");
        }
        ReinitializeGameplayForValidation();
    }
}
