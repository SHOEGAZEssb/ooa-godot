using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSmogNativeDeath()
    {
        var record = new EnemyDatabase().ImportedEnemy(EnemyId.Smog,0);
        var actor = new SmogCharacter();
        int randomCalls = 0, frame = 0;
        int Random() => ++randomCalls;
        actor.InitializeMergedCloud(record,3,3,new(72,72),0,_ => 0);
        void Init() => actor.UpdateMergedInitialization(2,_ => { },(_,_) => { },Random);
        try
        {
            actor.InvincibilityCounter = -3;
            for (int tick = 1; tick <= 5; tick++)
            {
                actor.Health = 1;
                actor.Animation.Advance(8);
                actor.UpdateNativeFrame(true,frame++,Random,Init,() => FailIf(true,"State0 cannot dispatch death."));
                FailIf(actor.Health != 6 || actor.NativeInitialRandom != tick || randomCalls != (tick == 5 ? 6 : tick) ||
                    actor.InvincibilityCounter != -3 || actor.State != (tick == 5 ? 8 : 0) || actor.AnimationFrame != 0,
                    "Frozen state0 Smog must reload health and draw var3d RNG on every merge-delay update, without the normal invincibility tail.");
            }
            FailIf(actor.ProjectileCounter != 40 || actor.ZIndex != NpcCharacter.BehindLinkZIndex,
                "Merge completion must consume a separate timer RNG draw after var3d and select visible$c2 priority.");
            actor.UpdateNativeFrame(true,frame++,Random,() => FailIf(true,"Initialized Smog must freeze."),() => { });
            FailIf(randomCalls != 6 || actor.InvincibilityCounter != -3, "Frozen state8 must skip handler/RNG/invincibility.");
            actor.UpdateNativeFrame(false,frame++,Random,() => actor.UpdateLargeCloud(new(200,72),_ => { },Random),() => { });
            FailIf(actor.InvincibilityCounter != -2 || randomCalls != 6 || actor.Counter1 != 19,
                "Normal Smog pass advances signed invincibility after its handler.");
        }
        finally { actor.Free(); }

        actor = new SmogCharacter(); randomCalls = 0; frame = 0;
        actor.InitializeMergedCloud(record,3,0,new(72,72),0,_ => 0);
        for (int i = 0; i < 5; i++) actor.UpdateNativeFrame(false,frame++,Random,Init,() => { });
        var events = new List<string>();
        int attempts = 0;
        bool allocate = false;
        void Death() => actor.UpdateBossDeath(point =>
            {
                attempts++; events.Add($"explode:{point.X},{point.Y}"); return allocate;
            }, () => events.Add("lock"), () => events.Add("mark"), () => events.Add("music"), sound => events.Add($"sound:{sound:x2}"));
        void Step(bool frozen = false) => actor.UpdateNativeFrame(frozen,frame++,Random,
            () => actor.UpdateLargeCloud(new(200,72),_ => FailIf(true,"Death fixture must not shoot."),Random),Death);
        try
        {
            actor.ApplyLargeSwordCollision(6,4,new(40,72));
            Step();
            FailIf(actor.Health != 0 || actor.IsDead || actor.InvincibilityCounter != 31 || actor.ContactFlags != 4 ||
                actor.Counter1 != 19 || events.Count != 0,
                "Lethal JUST_HIT must run Smog's normal handler once before NO_HEALTH, then clear pending and decrement invincibility.");
            Step();
            FailIf(actor.Counter1 != 119 || actor.Visible || actor.InvincibilityCounter != 30 ||
                !events.SequenceEqual(new[] { "lock","sound:67" }),
                "First NO_HEALTH update must lock Link, play boss death once, decrement120->119 and toggle visibility.");
            for (int i = 0; i < 3; i++) Step(true);
            FailIf(actor.Counter1 != 119 || actor.Visible || actor.InvincibilityCounter != 30,
                "Frozen boss death must hold visibility, countdown and invincibility.");
            for (int deathTick = 2; deathTick <= 119; deathTick++)
            {
                Step();
                FailIf(actor.Counter1 != 120-deathTick || actor.Visible != (deathTick % 2 == 0) || actor.IsDead || attempts != 0,
                    "Boss must flicker on each of119 death updates without allocating its explosion early.");
            }
            bool heldVisibility = actor.Visible;
            Step(); Step();
            FailIf(attempts != 2 || actor.IsDead || actor.Counter1 != 1 || actor.Visible != heldVisibility ||
                events.Count != 4 || randomCalls != 6,
                "Full PART pool must retry every update with counter1=1, without flicker, repeated sound, RNG or marking defeat.");
            allocate = true; Step();
            FailIf(!actor.IsDead || actor.Visible || attempts != 3 ||
                !events.TakeLast(3).SequenceEqual(new[] { "explode:72,72","mark","music" }),
                "Successful PART$04 handoff must allocate, mark the enemy, restore music and delete without decrementing the live enemy count here.");
            int completedEvents = events.Count;
            Step();
            FailIf(events.Count != completedEvents, "Deleted boss must not repeat its death handoff.");
        }
        finally { actor.Free(); }
        GD.Print("Validated Smog native state0 RNG/property reload, frozen-tail rules, lethal JUST_HIT precedence and120-update boss death with allocation retry.");
    }
}
