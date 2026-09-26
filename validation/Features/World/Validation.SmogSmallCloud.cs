using Godot;
using System.Collections.Generic;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSmogSmallCloud()
    {
        var record = new EnemyDatabase().ImportedEnemy(EnemyId.Smog,0);
        foreach (int subid in new[] { 2, 0x82 })
        {
            var actor = new SmogCharacter();
            var shots = new List<Vector2>();
            int begins = 0, random = 0, decrements = 0;
            actor.InitializeSmallCloud(record, subid, 3, new(72,72), 0, _ => 0);
            void Step() => actor.UpdateSmallCloud(0, () => begins++, shots.Add, _ => { }, () => decrements++, () => { random++; return 0; });
            try
            {
                Step();
                FailIf(begins != 1 || random != 1 || actor.CollisionMode != 7 || actor.CollisionBounds.Size != new Vector2(8,8) ||
                    actor.ProjectileCounter != 50 || actor.Position != new Vector2(72,72) || actor.AnimationFrame != 0,
                    "Small Smog initialization must begin boss, set radius4/mode07, draw timer50 and return before movement/animation.");
                for (int tick = 1; tick <= 95; tick++)
                {
                    Vector2 before = actor.Position;
                    Step();
                    if (tick == 49) FailIf(actor.AnimationIndex != 0 || actor.ProjectileCounter != 1, "Smog timer must remain1 before update50.");
                    if (tick == 50) FailIf(actor.AnimationIndex != 1 || actor.AnimationFrame != 0 || actor.ProjectileCounter != 0,
                        "Smog begins its firing animation on timer1->0, without advancing the newly selected frame.");
                    if (tick < 95) FailIf(shots.Count != 0 || random != 1, "Small Smog must wait30+15 animation updates before firing or drawing another RNG byte.");
                    else FailIf(shots.Count != 1 || shots[0] != before || actor.AnimationIndex != 0 || actor.ProjectileCounter != 49 || random != 2,
                        "Small Smog update95 must reset idle/timer, spawn at its pre-movement position and decrement the new timer on the same update.");
                }
                FailIf(begins != 1 || decrements != 0 || actor.IsDead, "Ordinary cloud updates must not repeat boss start or retire the enemy.");
            }
            finally { actor.Free(); }
        }
        // Animation continues during sword pauses; shooting and room reset wait
        // behind counter2, including when a firing parameter becomes ready.
        foreach (bool reset in new[] { false, true })
        {
            var actor = new SmogCharacter();
            int shots = 0, decrements = 0, puffs = 0, random = 0;
            actor.InitializeSmallCloud(record, 2, 3, new(72,72), 0, _ => 1);
            void Step(int roomFlags = 0) => actor.UpdateSmallCloud(roomFlags, () => { }, _ => shots++, _ => puffs++,
                () => decrements++, () => { random++; return 0; });
            try
            {
                Step();
                for (int i = 0; i < 75; i++) Step();
                actor.PublishCollision(0x84);
                var stopped = actor.NativePosition;
                for (int i = 1; i <= 29; i++)
                {
                    Step(reset ? 0x40 : 0);
                    FailIf(actor.Counter2 != 30-i || actor.NativePosition != stopped || shots != 0 || decrements != 0 ||
                        actor.ProjectileCounter != 0 || random != 1 || actor.ContactFlags != 4 || actor.IsDead,
                        "Small cloud must retain its position/timer during29 paused updates and clear only the pending collision bit.");
                }
                FailIf(actor.AnimationParameter != 1, "Firing animation must advance to its held parameter while movement/shooting are paused.");
                Step(reset ? 0x40 : 0);
                FailIf(actor.Counter2 != 0 || actor.IsDead != reset || shots != (reset ? 0 : 1) ||
                    decrements != (reset ? 1 : 0) || puffs != (reset ? 1 : 0) || random != (reset ? 1 : 2),
                    "Pause completion must check reset before animation/shooting; otherwise its ready parameter fires immediately.");
            }
            finally { actor.Free(); }
        }
        foreach (int flags in new[] { 0x03,0x04,0x09,0x80,0x83,0x84,0x85,0x86,0x87,0x88,0x89,0x8a })
        {
            var actor = new SmogCharacter();
            actor.InitializeSmallCloud(record,2,0,new(72,72),0,_ => 1);
            try
            {
                actor.UpdateSmallCloud(0, () => { }, _ => { }, _ => { }, () => { }, () => 0);
                actor.PublishCollision(flags);
                actor.UpdateSmallCloud(0x40, () => { }, _ => { }, _ => { }, () => { }, () => 0);
                bool pause = flags is >= 0x84 and <= 0x89;
                FailIf(actor.IsDead == pause || actor.Counter2 != (pause ? 29 : 0),
                    $"Smog sword pause gate requires pending bit and collision04-09; flags${flags:x2} changed reset ordering.");
            }
            finally { actor.Free(); }
        }
        GD.Print("Validated isolated small Smog initialization, animation-driven firing order, sword pause gates and reset/ready-shot precedence.");
    }
}
