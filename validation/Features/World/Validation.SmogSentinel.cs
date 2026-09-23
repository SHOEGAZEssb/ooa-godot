using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSmogSentinel()
    {
        var record = new EnemyDatabase().ImportedEnemy(0x7c,0);
        foreach (int subid in new[] { 5,6 })
        foreach (int returnedA in new[] { 1,0,8,16,24 })
        {
            var actor = new SmogCharacter();
            actor.InitializeRoomSentinel(record,subid,new(120,88));
            actor.Counter1 = 27; actor.InvincibilityCounter = -2;
            var events = new List<string>();
            int randomCalls = 0;
            void Step(bool frozen) => actor.UpdateNativeFrame(frozen,0,() => ++randomCalls,
                () => actor.UpdateRoomSentinel(() => { events.Add("entry"); return returnedA; },
                    point => events.Add($"puff:{point.X},{point.Y}"),() => events.Add("decrement")),
                () => events.Add("death"));
            Step(true);
            FailIf(actor.State != 8 || actor.Visible || actor.CollisionEnabled || actor.Speed != returnedA ||
                actor.Counter1 != 27 || actor.InvincibilityCounter != -2 || randomCalls != 1 ||
                actor.ProjectileCounter != 0 || !events.SequenceEqual(new[] { "entry" }),
                $"Smog subid${subid:x2} must initialize while frozen, retain returned A as speed, hide/disable collision, and consume only generic enemy RNG.");
            Step(true); Step(true);
            FailIf(actor.IsDead || events.Count != 1 || actor.InvincibilityCounter != -2,
                "Initialized sentinel/deletion form must hold during frozen enemy passes.");
            Step(false);
            if (subid == 6)
                FailIf(!actor.IsDead || !events.SequenceEqual(new[] { "entry","puff:120,88","decrement" }),
                    "Uninitialized subid$06 must initialize first, then puff/decrement/delete on its next eligible state8 update.");
            else
                FailIf(actor.IsDead || actor.Counter1 != 27 || events.Count != 1 || actor.InvincibilityCounter != -1,
                    "Subid$05 state8 must remain counted/inert while the generic invincibility tail still runs.");
            Step(false); Step(false);
            FailIf(events.Count != (subid == 6 ? 3 : 1) || randomCalls != 1,
                "Smog sentinel must not repeat room initialization or deletion effects.");
            actor.Free();
        }

        // INTERAC$33's bit1 filter can mark a still-initializing merged cloud6.
        // Its native state remains0, so it takes room initialization, not deletion.
        var merged = new SmogCharacter();
        merged.InitializeMergedCloud(record,3,0,new(72,72),1,_ => 0);
        merged.SetMergeSubId(6);
        int initializationCalls = 0, deleted = 0, rng = 0;
        void Update() => merged.UpdateNativeFrame(false,0,() => ++rng,
            () => merged.UpdateRoomSentinel(() => { initializationCalls++; return 16; },_ => {},() => deleted++),() => {});
        Update();
        FailIf(merged.State != 8 || merged.IsDead || merged.Counter2 != 5 || initializationCalls != 1 || deleted != 0 || rng != 1,
            "Changing a merged state0 cloud to subid$06 must preserve its state/counter until native room initialization dispatch.");
        Update();
        FailIf(!merged.IsDead || deleted != 1 || initializationCalls != 1,
            "Reclassified state0 cloud must delete only after its room initialization update.");
        merged.Free();
        GD.Print("Validated Smog invisible room sentinel and uninitialized deletion form, frozen eligibility, returned speed/RNG and ordered lifetime effects.");
    }
}
