using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareStalfosPartAdmissionRom()
    {
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batched in new[] { false, true })
        foreach (int free in new[] { 0, 1 })
        {
            var native = PrepareEnemyPartAdmissionRom(0x31, 2, out var enemy);
            var stalfos = (StalfosCharacter)enemy;
            OccupyPartAdmissionSlotsRom(native, free);
            void Stage()
            {
                typeof(StalfosCharacter).GetField("_state", hidden)!.SetValue(stalfos, StalfosState.Firing);
                native[0xd084] = 0x0c;
            }
            int observations = 0;
            void Compare()
            {
                AdvancePartAdmissionRom(native); observations++;
                var rng = _random.CaptureState();
                FailIf(rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95] ||
                    (int)stalfos.State != native[0xd084] || stalfos.Counter1 != native[0xd086] || stalfos.Angle != native[0xd089],
                    $"Stalfos bone admission must preserve the subsequent random route on success and failure, free={free}, update={observations}.");
                int nativeCount = Enumerable.Range(0, 16).Count(slot => native[0xd0c0 + slot * 256] != 0 && native[0xd0c1 + slot * 256] == 0x1c);
                FailIf(_entities.Entities<StalfosBoneProjectile>().Count != nativeCount, "Stalfos bones must respect shared PART capacity.");
                for (int slot = 0; slot < 16; slot++)
                {
                    int at = 0xd0c0 + slot * 256;
                    if (_entities.ResolvePartSlot(slot)?.Node is StalfosBoneProjectile bone)
                        FailIf(native[at + 1] != 0x1c || bone.Angle != native[at + 9] ||
                            bone.Position != new Vector2(native.Word(at + 12) / 256f, native.Word(at + 10) / 256f),
                            "Admitted bone must initialize its native target angle and then move in the shared PART phase.");
                }
            }
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: Compare);
            Stage(); Step(1);
            FailIf(_entities.Entities<StalfosBoneProjectile>().Count != free, "Stalfos's guarded source request must admit zero/one bone.");
            Step(2);
            if (free == 0)
            {
                _entities.RetireEntity(_entities.ResolvePartSlot(5)!);
                for (int offset = 0; offset < 64; offset++) native[0xd5c0 + offset] = 0;
                Step(1);
                FailIf(_entities.Entities<StalfosBoneProjectile>().Count != 0, "Capacity release must not replay a failed bone request.");
                Stage(); Step(1);
                FailIf(native[0xd5c1] != 0x1c, "A subsequent Stalfos firing state must reuse the released PART$d5 slot.");
                Step(2);
            }
            _entities.Clear();
        }
    }
}
