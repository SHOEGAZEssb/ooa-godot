using Godot;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void CompareCuccoAttackerBirthRom()
    {
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        // Edge selection and speed selection are independent source tables:
        // exercise all four edges, then the eight remaining speed rows.
        foreach (var test in Enumerable.Range(0, 4).Select(edge => (Edge: edge, Hits: 0x10))
            .Concat(Enumerable.Range(1, 8).Select(index => (Edge: 0, Hits: 0x10 + index * 2))))
        {
            var native = PrepareEnemyPartAdmissionRom(0x36, 0, out var enemy);
            typeof(CuccoCharacter).GetField("_hitCount", hidden)!.SetValue(enemy, test.Hits);
            native[0xd0b0] = (byte)test.Hits;
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 4); native[0xcc8a] = 4;
            var seed = _random.CaptureState();
            bool found = false;
            for (int candidate = 0; candidate < 256; candidate++)
            {
                var prediction = new OracleRandom();
                var trial = seed with { Rng1 = (byte)candidate, Rng2 = 1 };
                prediction.RestoreState(trial);
                if (((prediction.Next().Value & 0x30) >> 4) != test.Edge) continue;
                seed = trial; found = true; break;
            }
            FailIf(!found, "Cucco edge fixture must find an original shared-RNG seed for the declared edge.");
            _random.RestoreState(seed); native[0xff94] = seed.Rng1; native[0xff95] = seed.Rng2;
            FailIf(!_entities.TryCreatePart(new CuccoAttackerSpawn(test.Hits)), "Cucco birth fixture requires an empty PART pool.");
            var original = SomariaPrivate<FrontendRom>(native, "_rom");
            original.Call(0x3e8e, 0); // getFreePartSlot.
            native[0xd0c1] = 0x22; native[0xd0d6] = 0x80; native[0xd0d7] = 0xd0;
            void Compare()
            {
                AdvancePartAdmissionRom(native);
                var attacker = _entities.Entities<CuccoAttackerCharacter>().Single();
                var rng = _random.CaptureState();
                FailIf(attacker.Position != new Vector2(native[0xd0cd], native[0xd0cb]) ||
                    attacker.Speed != native[0xd0d0] || (int)attacker.State != native[0xd0c4] || attacker.Counter != native[0xd0c6] ||
                    rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95],
                    $"Cucco PART$22 birth/motion differs: edge={test.Edge}, hits=${test.Hits:x2}, runtime={attacker.Position}, ROM={native[0xd0cd]}/{native[0xd0cb]}.");
            }
            StepGameplayUpdates(3, Vector2.Zero, batched: test.Edge == 0 && test.Hits == 0x10, afterUpdate: Compare);
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0);
            _entities.Clear();
        }
    }
}
