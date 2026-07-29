using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private static void ValidateApplicationFixedUpdateScheduler()
    {
        const int updateCount = 9;
        SchedulerRegressionState split = RunSchedulerRegression(
            updateCount,
            batched: false);
        SchedulerRegressionState batched = RunSchedulerRegression(
            updateCount,
            batched: true);

        FailIf(
            split.Snapshot() != batched.Snapshot(),
            "The application scheduler produced different state for N fixed " +
            "host calls and one N-update host call.");
        FailIf(
            batched.HeldAttackUpdates != updateCount ||
            batched.PressedAttackUpdates != 1 ||
            !batched.PortalStarted ||
            batched.ChildrenSpawned != 1 ||
            batched.ChildrenRemoved != 1 ||
            batched.Room != 0x22 ||
            batched.MenuOpen ||
            batched.Trace !=
                "0:menu-open|1:menu-close|2:link|2:portal-start|" +
                "2:entities|2:events|2:interactions|2:hud|2:animation|2:sound|" +
                "3:link|3:entities|3:child-spawn|3:events|3:interactions|" +
                "3:hud|3:animation|3:sound|4:link|4:entities|4:events|" +
                "4:interactions|4:hud|4:animation|4:sound|5:link|5:entities|" +
                "5:child-death|5:events|5:interactions|5:hud|5:animation|" +
                "5:sound|6:link|6:entities|6:warp-pending|6:warp-dispatch|" +
                "6:events|6:interactions|6:hud|6:animation|6:sound|7:link|" +
                "7:entities|7:events|7:interactions|7:hud|7:animation|" +
                "7:sound|8:link|8:entities|8:events|8:interactions|8:hud|" +
                "8:animation|8:sound",
            "The application update regression did not cross its menu, " +
            "portal, child lifetime, pending warp, HUD/animation, sound, " +
            "and held-versus-pressed boundaries in the documented order.");

        var input = new ApplicationInputBuffer();
        input.CaptureForValidation(
            new[] { "attack" },
            new[] { "attack" },
            Vector2.Zero);
        ApplicationInputSnapshot first = input.ConsumeOriginalUpdate();
        ApplicationInputSnapshot second = input.ConsumeOriginalUpdate();
        FailIf(
            !first.IsPressed("attack") ||
            !first.IsJustPressed("attack") ||
            !second.IsPressed("attack") ||
            second.IsJustPressed("attack"),
            "A host-frame input edge was not restricted to its owning " +
            "original update.");

        if (InputMap.HasAction("debug_collision"))
            InputMap.EraseAction("debug_collision");
        input.Clear();
        input.CaptureHostFrame();
        ApplicationInputSnapshot withoutGameplayDebugAction =
            input.ConsumeOriginalUpdate();
        FailIf(
            withoutGameplayDebugAction.IsPressed("debug_collision") ||
            withoutGameplayDebugAction.IsJustPressed("debug_collision"),
            "The application input buffer did not treat an unregistered " +
            "gameplay-scoped debug action as inactive.");
        _ = new DebugCollisionController();

        GD.Print(
            "Validated application-owned 60 Hz update interleaving, split/" +
            "batched equivalence, menu/portal/child/warp boundaries, and " +
            "single-owner input edges, including absent gameplay debug actions.");
    }

    private static SchedulerRegressionState RunSchedulerRegression(
        int updateCount,
        bool batched)
    {
        var scheduler = new ApplicationFixedUpdateScheduler();
        var state = new SchedulerRegressionState();
        int snapshotIndex = 0;

        void Advance()
        {
            string[] justPressed = snapshotIndex == 0
                ? new[] { "attack", "inventory" }
                : Array.Empty<string>();
            var snapshot = new ApplicationInputSnapshot(
                new[] { "attack" },
                justPressed,
                Vector2.Zero);
            snapshotIndex++;
            Input.BeginOriginalUpdate(snapshot);
            try
            {
                state.Advance();
            }
            finally
            {
                Input.EndOriginalUpdate();
            }
        }

        if (batched)
        {
            scheduler.Advance(
                updateCount * ApplicationFixedUpdateScheduler.UpdateDelta,
                Advance);
        }
        else
        {
            for (int index = 0; index < updateCount; index++)
            {
                scheduler.Advance(
                    ApplicationFixedUpdateScheduler.UpdateDelta,
                    Advance);
            }
        }

        FailIf(
            snapshotIndex != updateCount ||
            scheduler.UpdateCount != updateCount ||
            Math.Abs(scheduler.Remainder) > 1e-9,
            "The application scheduler did not consume the exact fixed-update " +
            "count and remainder.");
        return state;
    }

    private sealed class SchedulerRegressionState
    {
        private readonly List<string> _trace = new();
        private int _update;
        private int _childAge = -1;
        private bool _roomWarpPending;

        internal int HeldAttackUpdates { get; private set; }
        internal int PressedAttackUpdates { get; private set; }
        internal bool PortalStarted { get; private set; }
        internal int ChildrenSpawned { get; private set; }
        internal int ChildrenRemoved { get; private set; }
        internal int Room { get; private set; } = 0x11;
        internal bool MenuOpen { get; private set; }
        internal string Trace => string.Join("|", _trace);

        internal void Advance()
        {
            if (Input.IsActionPressed("attack"))
                HeldAttackUpdates++;
            if (Input.IsActionJustPressed("attack"))
                PressedAttackUpdates++;

            if (Input.IsActionJustPressed("inventory"))
            {
                MenuOpen = true;
                Record("menu-open");
                _update++;
                return;
            }
            if (MenuOpen)
            {
                MenuOpen = false;
                Record("menu-close");
                _update++;
                return;
            }

            Record("link");
            if (_update == 2)
            {
                PortalStarted = true;
                Record("portal-start");
            }

            Record("entities");
            if (_update == 3)
            {
                _childAge = 0;
                ChildrenSpawned++;
                Record("child-spawn");
            }
            else if (_childAge >= 0 && ++_childAge == 2)
            {
                _childAge = -1;
                ChildrenRemoved++;
                Record("child-death");
            }
            if (_update == 6)
            {
                _roomWarpPending = true;
                Record("warp-pending");
            }
            if (_roomWarpPending)
            {
                _roomWarpPending = false;
                Room = 0x22;
                Record("warp-dispatch");
            }

            Record("events");
            Record("interactions");
            Record("hud");
            Record("animation");
            Record("sound");
            _update++;
        }

        internal string Snapshot() =>
            $"{_update}:{HeldAttackUpdates}:{PressedAttackUpdates}:" +
            $"{PortalStarted}:{ChildrenSpawned}:{ChildrenRemoved}:{Room}:" +
            $"{MenuOpen}:{_childAge}:{_roomWarpPending}:{Trace}";

        private void Record(string value) => _trace.Add($"{_update}:{value}");
    }
}
