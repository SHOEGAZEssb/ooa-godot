using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateArrowPartAdmissionRom()
    {
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (int id in new[] { 0x0c, 0x20, 0x21, 0x22 })
        foreach (bool batched in new[] { false, true })
        foreach (int free in new[] { 0, 1 })
        {
            var native = PrepareEnemyPartAdmissionRom(id, 0, out var enemy);
            OccupyPartAdmissionSlotsRom(native, free);
            Type fields = enemy is MaskedMoblinCharacter ? typeof(MaskedMoblinCharacter) : typeof(ArrowMoblinCharacter);
            void StageThrow(int cycles)
            {
                fields.GetField("_state", hidden)!.SetValue(enemy,
                    enemy is MaskedMoblinCharacter ? (object)MoblinState.Turning : ArrowMoblinState.Turning);
                fields.GetField("_counter", hidden)!.SetValue(enemy, 2);
                fields.GetField("_moveCycles", hidden)!.SetValue(enemy, cycles);
                native[0xd084] = 9; native[0xd086] = 2; native[0xd0b0] = (byte)cycles;
                native[0xd089] = (byte)(int)fields.GetField("_angle", hidden)!.GetValue(enemy)!;
                native[0xd088] = (byte)(native[0xd089] >> 3);
                // The first turn update consumes no RNG. Predict the source
                // direction gate independently of the imported behavior data.
                var prediction = new OracleRandom(); prediction.RestoreState(_random.CaptureState());
                // Uninitialized filler PART$53 draws once each before the
                // terminal turn update; later staged turns have no such draws.
                if (!((BlueEnergyBeadRoomEntity)_entities.ResolvePartSlot(0)!).Initialized)
                    for (int index = 0; index < 16 - free; index++) prediction.Next();
                int angle;
                if (id == 0x21)
                    angle = (prediction.Next().Value & 3) == 0 ? 8 : prediction.NextCardinalAngle();
                else angle = prediction.NextCardinalAngle();
                _player.WarpTo(enemy.Position + OracleObjectMath.StrictCardinalVector(angle) * 32);
                native[0xd00b] = (byte)_player.Position.Y; native[0xd00d] = (byte)_player.Position.X;
            }
            int observations = 0;
            void Compare()
            {
                AdvancePartAdmissionRom(native); observations++;
                int state = enemy is MaskedMoblinCharacter ?
                    ((MoblinState)fields.GetField("_state", hidden)!.GetValue(enemy)! == MoblinState.Turning ? 9 : 8) :
                    (int)((ArrowMoblinCharacter)enemy).State;
                int counter = (int)fields.GetField("_counter", hidden)!.GetValue(enemy)!;
                int cycles = (int)fields.GetField("_moveCycles", hidden)!.GetValue(enemy)!;
                int angle = (int)fields.GetField("_angle", hidden)!.GetValue(enemy)!;
                FailIf(state != native[0xd084] || counter != native[0xd086] || (cycles & 255) != native[0xd0b0] || angle != native[0xd089],
                    $"ENEMY${id:x2} arrow failure must preserve native route/counter/alternation, free={free}, update={observations}.");
                var rng = _random.CaptureState();
                FailIf(rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95], "Arrow admission must preserve native shared RNG order.");
                int nativeCount = Enumerable.Range(0, 16).Count(slot => native[0xd0c0 + slot * 256] != 0 && native[0xd0c1 + slot * 256] == 0x1a);
                FailIf(_entities.Entities<EnemyArrowProjectile>().Count != nativeCount,
                    $"ENEMY${id:x2} PART$1a capacity differs: free={free}, runtime={_entities.Entities<EnemyArrowProjectile>().Count}, ROM={nativeCount}.");
                for (int slot = 0; slot < 16; slot++)
                {
                    int at = 0xd0c0 + slot * 256; var actor = _entities.ResolvePartSlot(slot);
                    FailIf((actor is null) != (native[at] == 0), "Arrow must occupy the native first-free physical PART slot.");
                    if (actor?.Node is EnemyArrowProjectile arrow)
                        FailIf(native[at + 1] != 0x1a || arrow.NativeState != native[at + 4] || arrow.Angle != native[at + 9] ||
                            arrow.Position != new Vector2(native.Word(at + 12) / 256f, native.Word(at + 10) / 256f),
                            $"PART$1a native initialization/motion differs for ENEMY${id:x2}, update={observations}: runtime={arrow.Position}, ROM={native.Word(at + 12) / 256f}/{native.Word(at + 10) / 256f}.");
                }
            }
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: Compare);
            StageThrow(0); Step(1);
            FailIf(_entities.Entities<EnemyArrowProjectile>().Count != 0, "Arrow cannot precede its terminal turn update.");
            Step(1);
            FailIf(_entities.Entities<EnemyArrowProjectile>().Count != free, "An eligible turn must admit exactly one arrow when capacity exists.");
            Step(2);
            if (free == 0)
            {
                _entities.RetireEntity(_entities.ResolvePartSlot(5)!);
                for (int offset = 0; offset < 64; offset++) native[0xd5c0 + offset] = 0;
                Step(1);
                FailIf(_entities.Entities<EnemyArrowProjectile>().Count != 0, "Releasing capacity must not replay a failed arrow.");
                StageThrow(1); Step(2); // Even route must still suppress shooting.
                FailIf(_entities.Entities<EnemyArrowProjectile>().Count != 0, "Failed allocation must retain the every-other-turn arrow gate.");
                StageThrow(2); Step(2);
                FailIf(native[0xd5c1] != 0x1a, "The later eligible arrow must reuse the released PART$d5 slot.");
                Step(2);
            }
            _entities.Clear();
            FailIf(_entities.Entities<EnemyArrowProjectile>().Count != 0, "Room cancellation must retire admitted arrows.");
        }
        CompareStalfosPartAdmissionRom();
        ReinitializeGameplayForValidation();
        GD.Print("Validated clean-US shared arrow admission for ENEMY$0c/$20/$21/$22, failure state/RNG, alternation and release/repeat.");
    }
}
