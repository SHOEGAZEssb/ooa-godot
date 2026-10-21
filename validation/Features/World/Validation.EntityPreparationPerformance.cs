using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateEntityPreparationPerformance()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var manager = typeof(RoomEntityManager);
        var live = (List<IRoomEntity>)manager.GetField("_activeEntities", flags)!.GetValue(_entities)!;
        var enemies = (Dictionary<IRoomEntity, int>)manager.GetField("_enemySlots", flags)!.GetValue(_entities)!;
        var parts = (Dictionary<IRoomEntity, int>)manager.GetField("_partSlots", flags)!.GetValue(_entities)!;
        var interactions = (Dictionary<IRoomEntity, int>)manager.GetField("_interactionSlots", flags)!.GetValue(_entities)!;
        Action<bool> BooleanPass(string name) => manager.GetMethod(name, flags)!.CreateDelegate<Action<bool>>(_entities);
        Action Pass(string name) => manager.GetMethod(name, flags)!.CreateDelegate<Action>(_entities);
        var seeds = BooleanPass("ResolveSeedCollisions");
        var projectiles = BooleanPass("ResolvePlayerProjectileCollisions");
        var bombs = Pass("ResolveBombExplosionCollisions");
        var catches = Pass("ResolveBossBombCatches");
        var transition = manager.GetMethod("PrepareIncomingEntitiesForScreenTransition", flags)!
            .CreateDelegate<Action<Player?>>(_entities);
        var owned = new List<IRoomEntity>();
        T Own<T>(T entity) where T : IRoomEntity { owned.Add(entity); live.Add(entity); return entity; }
        void Reset()
        {
            live.Clear(); enemies.Clear(); parts.Clear(); interactions.Clear();
            foreach (var entity in owned) entity.Node.Free();
            owned.Clear();
        }
        _entities.Clear();
        int rng = _random.Calls;
        try
        {
            // Each source item has an independent target scan. A callback may
            // remove a target or create a later item, without invalidating the
            // current scan or retroactively entering the outer item snapshot.
            Own(new CollisionPerformanceProbe(1, true));
            Own(new CollisionPerformanceProbe(2, true));
            var a = Own(new CollisionPerformanceProbe(0));
            var b = Own(new CollisionPerformanceProbe(0));
            var trace = new List<string>();
            bool changed = false;
            a.Hit = position =>
            {
                trace.Add($"{position.X}:A");
                if (changed) return;
                changed = true;
                seeds(true); // Nested scans must not overwrite either outer snapshot.
                live.Remove(b);
                var c = Own(new CollisionPerformanceProbe(3, true));
                c.Hit = source => trace.Add($"{source.X}:C");
            };
            b.Hit = position => trace.Add($"{position.X}:B");
            seeds(false);
            FailIf(string.Join(",", trace) != "1:A,1:B,2:A,2:C",
                "Collision snapshots lost current-target retention, fresh per-item targets or later-item deferral.");
            trace.Clear();
            TransitionPerformanceProbe? removed = null;
            seeds(false);
            FailIf(string.Join(",", trace) != "1:A,1:C,2:A,2:C,3:A,3:C",
                "A subsequent seed pass failed to observe changed item/target membership.");
            Reset();

            TransitionPerformanceProbe Add(string name, int phase, int slot, Action? action = null)
            {
                var node = phase == 1 ? (Node2D)new OctorokRockProjectile() : new Node2D();
                var probe = Own(new TransitionPerformanceProbe(node, () => { trace.Add(name); action?.Invoke(); }));
                if (phase == 0) enemies.Add(probe, slot);
                else if (phase == 1) parts.Add(probe, slot);
                else if (slot >= 0) interactions.Add(probe, slot);
                return probe;
            }
            trace.Clear();
            // bank0.updateEnemies / updateInteractions visit later live slots
            // first; children allocated below the cursor wait for the next pass.
            Add("E", 0, 5, () => { Add("EL", 0, 1); Add("EH", 0, 6); });
            Add("P", 1, 4);
            Add("I", 2, 5, () => { live.Remove(removed!); Add("PL", 1, 0); Add("IL", 2, 2); Add("IH", 2, 6); });
            removed = Add("REMOVED", 2, -1);
            Add("F", 2, -1);
            transition(null);
            FailIf(string.Join(",", trace) != "E,EH,P,I,IH,F,EL,PL,IL",
                "Transition preparation changed native phase order, child eligibility or fallback ordering.");
            Reset();

            for (int index = 0; index < 40; index++) Own(new CollisionPerformanceProbe(index, index < 8));
            void CollisionBatch() { seeds(true); seeds(false); projectiles(false); bombs(); catches(); }
            for (int warm = 0; warm < 128; warm++) CollisionBatch();
            long before = GC.GetAllocatedBytesForCurrentThread();
            var timer = Stopwatch.StartNew();
            for (int repeat = 0; repeat < 1024; repeat++) CollisionBatch();
            timer.Stop();
            GD.Print($"ENTITY_COLLISIONS batches=1024 entities=40 items=8 elapsed_ms={timer.Elapsed.TotalMilliseconds:F3} allocated_bytes={GC.GetAllocatedBytesForCurrentThread() - before}");
            Reset();

            for (int index = 63; index >= 0; index--)
            {
                int phase = index < 16 ? 0 : index < 32 ? 1 : 2;
                var node = phase == 1 ? (Node2D)new OctorokRockProjectile() : new Node2D();
                var probe = Own(new TransitionPerformanceProbe(node));
                if (phase == 0) enemies.Add(probe, index);
                else if (phase == 1) parts.Add(probe, index - 16);
                else if (index < 46) interactions.Add(probe, index - 30);
            }
            for (int warm = 0; warm < 16; warm++) transition(null);
            before = GC.GetAllocatedBytesForCurrentThread();
            timer.Restart();
            for (int repeat = 0; repeat < 256; repeat++) transition(null);
            timer.Stop();
            GD.Print($"ENTITY_TRANSITIONS passes=256 entities=64 elapsed_ms={timer.Elapsed.TotalMilliseconds:F3} allocated_bytes={GC.GetAllocatedBytesForCurrentThread() - before}");
            FailIf(_random.Calls != rng, "Collision/transition preparation probes consumed gameplay RNG.");
        }
        finally { Reset(); }
    }
}
