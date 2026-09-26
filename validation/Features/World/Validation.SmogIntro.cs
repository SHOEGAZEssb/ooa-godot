using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSmogIntro()
    {
        var database = new EnemyDatabase();
        // TX_2f26 has an explicit top position and a normal implicit terminator;
        // TX_2f27 is a separate taunt, not part of the introduction.
        const string introduction = "\\pos(2)It's time for\nour little game!\nI break apart.\nIf you can\nforce me back\ntogether and\nblow me away,\nit ends!\nBut before you\ndo, I shall\ntake a bit of\nyour soul!!!\nNow begin!";
        FailIf(database.SmogIntroText != introduction, "Smog TX_2f26 must preserve its position, source lines and terminating boundary.");
        var record = database.ImportedEnemy(EnemyId.Smog,0);
        FailIf(!record.Sprites.SequenceEqual(new[] { "spr_smog_1", "spr_smog_2" }) || record.TileBase != 0 ||
            record.Palette != 3 || !record.SourceGrayscaleInverted || record.RadiusX != 10 || record.RadiusY != 10 ||
            record.Health != 6 || record.RawDamage != 0xfc,
            "ENEMY$7c must retain graphics$c5/$c6, flags$30 and extra-data$28 ($0a,$0a,$fc,$06).");
        int[][] durations = [[8,8,8,8], [30,15,127], [6,6,6,6], [30,15,127], [6,6,6,6,6,6], [30,15,1,10,127]];
        int[] loops = [0,2,0,2,0,4];
        FailIf(record.Animations.Length != 6, "ENEMY$7c requires all six source animations.");
        for (int i = 0; i < 6; i++)
        {
            var animation = OracleGraphicsCache.GetAnimationDefinition(record.Animations[i]);
            FailIf(animation.LoopStart != loops[i] || !animation.Frames.Select(frame => frame.Duration).SequenceEqual(durations[i]),
                $"Smog animation{i} must preserve source frame durations and interior loop target.");
            for (int frame = 0; frame < animation.Frames.Length; frame++)
            {
                int parameter = i is 1 or 3 && frame == 2 || i == 5 && frame == 4 ? 1 : i == 5 && frame == 2 ? 0x80 : 0;
                FailIf(animation.Frames[frame].Parameter != parameter, $"Smog animation{i} frame{frame} lost its source firing parameter.");
            }
        }
        foreach (int subid in new[] { 0, 1 })
        {
            var actor = new SmogCharacter();
            var events = new List<string>();
            int randomCalls = 0;
            actor.InitializeIntro(record, subid, new(8,88));
            void Step(bool text) => actor.UpdateIntro(text,
                id => events.Add($"text:{id:x4}"),
                (point, child) => events.Add($"spawn:{child}:{point.X},{point.Y}"),
                point => events.Add($"puff:{point.X},{point.Y}"),
                () => events.Add("decrement"),
                () => { randomCalls++; return 0; });
            try
            {
                Step(false);
                int duration = subid == 0 ? 60 : 120;
                FailIf(actor.State != 8 || actor.Counter2 != duration || !actor.Visible || randomCalls != 1 ||
                    actor.ProjectileCounter != (subid == 0 ? 0x3a : 0xd7) || actor.AnimationIndex != (subid == 0 ? 4 : 0),
                    "Smog intro initialization must select animation, consume one RNG draw and return without counting down.");
                FailIf(!events.SequenceEqual(subid == 0 ? new[] { "text:2f26" } : []), "Only the parent intro must request TX_2f26.");
                events.Clear();
                if (subid == 0)
                {
                    for (int tick = 0; tick < 90; tick++) Step(true);
                    FailIf(actor.Counter2 != 60 || actor.AnimationFrame != 0 || events.Count != 0 || randomCalls != 1,
                        "Parent intro waits for text before animation, countdown or RNG.");
                }
                for (int tick = 1; tick < duration; tick++) Step(false);
                FailIf(actor.IsDead || actor.Counter2 != 1 || events.Count != 0,
                    "Smog intro must remain alive until its exact countdown completion update.");
                Step(false);
                string[] wanted = subid == 0 ? ["spawn:1:248,88", "spawn:1:24,88", "puff:8,88", "decrement"] : ["puff:8,88", "decrement"];
                FailIf(!actor.IsDead || !events.SequenceEqual(wanted) || randomCalls != 1,
                    "Smog intro completion must allocate left then right child with byte wrapping, then puff/decrement/delete; children only puff/decrement/delete.");
                Step(false);
                FailIf(!events.SequenceEqual(wanted), "Deleted intro must not repeat completion effects.");
            }
            finally { actor.Free(); }
        }
        GD.Print("Validated Smog six animation streams and isolated intro text wait, exact split/child lifetime, RNG and ordered completion effects.");
    }
}
