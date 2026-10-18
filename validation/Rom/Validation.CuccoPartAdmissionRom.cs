using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCuccoPartAdmissionRom()
    {
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batched in new[] { false, true })
        foreach (int free in new[] { 0, 1 })
        {
            var native = PrepareEnemyPartAdmissionRom(0x36, 0, out var enemy);
            var cucco = (CuccoCharacter)enemy;
            OccupyPartAdmissionSlotsRom(native, free);
            typeof(CuccoCharacter).GetField("_state", hidden)!.SetValue(cucco, CuccoState.Standing);
            typeof(CuccoCharacter).GetField("_hitCount", hidden)!.SetValue(cucco, 0x10);
            typeof(CuccoCharacter).GetField("_revengeCounter", hidden)!.SetValue(cucco, 2);
            native[0xd084] = 8; native[0xd0b0] = 0x10; native[0xd0b3] = 2;
            int elapsed = 0;
            void Compare()
            {
                AdvancePartAdmissionRom(native); elapsed++;
                FailIf(cucco.RevengeCounter != native[0xd0b3],
                    "Cucco's revenge timer must reset to $1e even when PART allocation fails.");
                var rng = _random.CaptureState();
                FailIf(rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95],
                    $"Cucco admission must not consume child RNG on failure, free={free}, update={elapsed}.");
                int nativeCount = Enumerable.Range(0, 16).Count(slot => native[0xd0c0 + slot * 256] != 0 && native[0xd0c1 + slot * 256] == 0x22);
                FailIf(_entities.Entities<CuccoAttackerCharacter>().Count != nativeCount,
                    $"PART$22 capacity differs: free={free}, runtime={_entities.Entities<CuccoAttackerCharacter>().Count}, ROM={nativeCount}.");
                for (int slot = 0; slot < 16; slot++)
                {
                    int at = 0xd0c0 + slot * 256; var actor = _entities.ResolvePartSlot(slot);
                    FailIf((actor is null) != (native[at] == 0), "Cucco attacker must occupy its native physical PART slot.");
                    if (actor?.Node is CuccoAttackerCharacter attacker)
                        FailIf(native[at + 1] != 0x22 || (int)attacker.State != native[at + 4] || attacker.Counter != native[at + 6],
                            "Cucco attacker must initialize once and continue ticking in the native PART walk.");
                }
            }
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: Compare);
            Step(1);
            FailIf(_entities.Entities<CuccoAttackerCharacter>().Count != 0, "Cucco cannot spawn before the revenge timer expires.");
            Step(1);
            FailIf(_entities.Entities<CuccoAttackerCharacter>().Count != free || cucco.RevengeCounter != 30,
                "Cucco admission must create zero/one attacker and preserve the independent $1e spawn cadence.");
            Step(2);
            if (free == 0)
            {
                _entities.RetireEntity(_entities.ResolvePartSlot(5)!);
                for (int offset = 0; offset < 64; offset++) native[0xd5c0 + offset] = 0;
                Step(27);
                FailIf(_entities.Entities<CuccoAttackerCharacter>().Count != 0 || cucco.RevengeCounter != 1,
                    "Capacity release must not retry before the next scheduled Cucco spawn.");
                Step(1);
                FailIf(native[0xd5c1] != 0x22 || _entities.Entities<CuccoAttackerCharacter>().Count != 1 || cucco.RevengeCounter != 30,
                    "The next scheduled Cucco spawn must reuse the released PART$d5 slot.");
                Step(2);
            }
            _entities.Clear();
            FailIf(_entities.Entities<CuccoAttackerCharacter>().Count != 0, "Cancellation must release all admitted Cucco attackers.");
        }
        CompareCuccoAttackerBirthRom();
        ReinitializeGameplayForValidation();
        GD.Print("Validated clean-US Cucco attacker admission, success-only child RNG, source timer reset and scheduled retry after capacity release.");
    }
}
