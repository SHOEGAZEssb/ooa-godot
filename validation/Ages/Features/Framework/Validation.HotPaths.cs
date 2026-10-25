using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void ValidateHotPaths()
    {
        var snapshot = new ApplicationInputSnapshot(["move_right", "attack"], ["attack"], Vector2.Right);
        void ReadInput()
        {
            FailIf(!Input.IsActionPressed("attack") || !Input.IsActionJustPressed("attack") ||
                Input.IsActionPressed("item") || Input.IsActionJustPressed("item") ||
                Input.GetVector("move_left", "move_right", "move_up", "move_down") != Vector2.Right,
                "Application input snapshot changed while reading it.");
        }
        Input.BeginOriginalUpdate(snapshot);
        try
        {
            var timer = new Stopwatch();
            var allocations = new List<long>(4);
            int zeroAllocationRounds = 0;
            for (int round = 0; round < 4 && zeroAllocationRounds < 2; round++)
            {
                // Tiered JIT compilation or lazy runtime setup can allocate once
                // after a short warmup. Repeated query allocations persist here.
                timer.Restart();
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 10000; i++) ReadInput();
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                timer.Stop();
                allocations.Add(allocated);
                zeroAllocationRounds = allocated == 0 ? zeroAllocationRounds + 1 : 0;
            }
            GD.Print($"Input query batches: {timer.Elapsed.TotalMilliseconds:F3} ms in final 10000 iterations; " +
                $"managed bytes by round: {string.Join(", ", allocations)}.");
            FailIf(zeroAllocationRounds < 2,
                "Reading an immutable application input snapshot must not allocate after warmup.");
        }
        finally { Input.EndOriginalUpdate(); }

        var animations = new OracleAnimationData();
        var groups = (Dictionary<int, Frame[][]>)typeof(OracleAnimationData)
            .GetField("_groups", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(animations)!;
        foreach (var (group, tracks) in groups)
        {
            int period = tracks.Max(track => track.Sum(frame => frame.Duration));
            // All supported groups start with bit7 set, so the first update
            // and the next two explicit forced updates emit source writes.
            // This reference decrements every counter,
            // including the zero boundary; production may skip idle updates.
            foreach (long tick in Enumerable.Range(0, period * 2 + 2).Select(t => (long)t)
                .Concat(new[] { -1L, 1000000000L }))
            {
                var expected = new List<int>();
                foreach (Frame[] track in tracks)
                {
                    int cycle = track.Sum(frame => frame.Duration);
                    int index = 0, counter = track[0].Duration;
                    void Emit()
                    {
                        expected.Add(track[index].HeaderIndex);
                        index = (index + 1) % track.Length;
                        counter = track[index].Duration;
                    }
                    Emit();
                    Emit();
                    Emit();
                    long updates = Math.Max(0, tick);
                    long simulated = Math.Min(updates, cycle) + (updates > cycle ? (updates - cycle) % cycle : 0);
                    for (long update = 0; update < simulated; update++)
                        if (--counter == 0) Emit();
                }
                FailIf(!expected.SequenceEqual(animations.GetActiveHeaders(group, tick)),
                    $"Animation group ${group:x2} changed its ordered DMA writes at tick {tick}.");
            }
        }
        // Independent source pins for group $00: duration-3 waterfall first
        // emits headers 0, 24 and 32, then header 1 exactly three updates later.
        FailIf(!animations.GetActiveHeaders(0, 0).Take(3).SequenceEqual(new[] { 0, 24,32 }) ||
            animations.GetActiveHeaders(0, 2)[3] == 1 || animations.GetActiveHeaders(0, 3)[3] != 1,
            "Animation group $00 lost its initial forced writes or duration-3 zero boundary.");
        var animationTimer = Stopwatch.StartNew();
        for (int iteration = 0; iteration < 1000; iteration++)
        foreach (int group in groups.Keys) animations.GetActiveHeaders(group, 1000000000L + iteration);
        animationTimer.Stop();
        GD.Print($"Animation schedules: {animationTimer.Elapsed.TotalMilliseconds:F3} ms for {1000 * groups.Count} calls; all source counter boundaries preserved.");
    }
}
