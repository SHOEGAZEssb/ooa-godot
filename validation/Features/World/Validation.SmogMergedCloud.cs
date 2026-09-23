using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSmogMergedCloud()
    {
        var record = new EnemyDatabase().ImportedEnemy(0x7c,0);
        foreach (int subid in new[] { 3, 0x83 })
        foreach (int enemies in new[] { 0, 1, 2, 3, 4 })
        {
            var actor = new SmogCharacter();
            var events = new List<string>();
            actor.InitializeMergedCloud(record, subid, 3, new(72,72), 0, _ => 0);
            void Init(int count) => actor.UpdateMergedInitialization(count,
                value => events.Add($"interaction-counter:{value}"),
                (position, tile) => events.Add($"tile:{position:x2}:{tile:x2}"),
                () => { events.Add("rng"); return 0; });
            try
            {
                for (int tick = 1; tick <= 4; tick++)
                {
                    Init(enemies == 2 ? 3 : 2); // Count before expiration must not decide the form.
                    FailIf(actor.State != 0 || actor.Counter2 != 5-tick || actor.Visible || actor.CollisionEnabled ||
                        actor.Position != new Vector2(72,72) || events.Count != 0,
                        "Merged Smog must remain hidden/noncolliding through four countdown updates without choosing a form, writing tiles or consuming RNG.");
                }
                Init(enemies);
                bool large = enemies == 2;
                string[] expected = large ? ["tile:11:a3", "rng"] : ["interaction-counter:60", "rng"];
                FailIf(actor.State != 8 || actor.Counter2 != 0 || !actor.Visible || !actor.CollisionEnabled ||
                    actor.SubId != (large ? 4 : subid) || actor.CollisionMode != (large ? 0x4d : 7) ||
                    actor.CollisionBounds.Size != (large ? new Vector2(20,20) : new Vector2(12,12)) ||
                    actor.Speed != (large ? 0x0a : 0x14) || actor.AnimationIndex != (large ? 4 : 2) ||
                    actor.ProjectileCounter != (large ? 20 : 50) || !events.SequenceEqual(expected),
                    "Fifth merge update must select large only for count2, otherwise write the same-page interaction counter without pausing the enemy.");
                if (!large)
                {
                    var shots = new List<Vector2>();
                    int puffs = 0;
                    for (int tick = 1; tick <= 95; tick++)
                    {
                        Vector2 before = actor.Position;
                        actor.UpdateMediumCloud(0, shots.Add, _ => puffs++, () => FailIf(true,"Medium fixture must stay alive."),
                            () => { events.Add("rng"); return 0; });
                        if (tick == 1) FailIf(actor.NativePosition.YFixed != 0x4780 ||
                            actor.NativePosition.XFixed != (subid == 3 ? 0x4880 : 0x4780) || actor.ProjectileCounter != 49,
                            "Medium Smog must move immediately after initialization; the $47 write must not become a60-update enemy pause.");
                        if (tick == 20) FailIf(actor.NativePosition.YFixed != 0x4780 ||
                            actor.NativePosition.XFixed != (subid == 3 ? 0x5200 : 0x3e00) || puffs != 0,
                            "Medium missing-wall path must continue straight instead of the small-cloud respawn.");
                        if (tick == 50) FailIf(actor.AnimationIndex != 3 || actor.ProjectileCounter != 0,
                            "Medium timer1->0 must select animation3.");
                        if (tick < 95) FailIf(shots.Count != 0, "Medium firing must wait30+15 animation updates.");
                        else FailIf(shots.Count != 1 || shots[0] != before || actor.AnimationIndex != 2 || actor.ProjectileCounter != 49 ||
                            events.Count != 3, "Medium firing must restore animation2, draw once and spawn before moving.");
                    }
                }
            }
            finally { actor.Free(); }
        }
        GD.Print("Validated isolated Smog five-update merge delay, exact count2 promotion, same-page counter write and medium movement/firing.");
    }
}
