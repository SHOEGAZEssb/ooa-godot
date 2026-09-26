using Godot;
using System;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateApplicationFixedUpdateScheduler()
    {
        const int updateCount = 9;
        var split = RunSchedulerRegression(
            updateCount,
            batched: false);
        var batched = RunSchedulerRegression(
            updateCount,
            batched: true);

        FailIf(
            split != batched,
            "The application scheduler produced different state for N fixed " +
            "host calls and one N-update host call.");
        FailIf(
            batched.HeldAttackUpdates != updateCount ||
            batched.PressedAttackUpdates != 1,
            "The application scheduler did not preserve held input on every " +
            "update and pressed input on its first update only.");

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

        ValidateDebugFastForward();

        GD.Print(
            "Validated application-owned 60 Hz update counts, split/" +
            "batched equivalence, and " +
            "single-owner input edges, including absent gameplay debug actions.");
    }

    private void ValidateDebugFastForward()
    {
        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot)
            .GetField("_applicationUpdates", flags)!.GetValue(this)!;
        using var key = new InputEventKey { PhysicalKeycode = Key.F5, Pressed = true };
        long start = scheduler.UpdateCount;
        base._Process(1.0 / 120.0);
        FailIf(scheduler.UpdateCount != start,
            "Debug fast forward must start disabled and retain partial host time.");
        _Input(key);
        try
        {
            base._Process(1.0 / 60.0);
            FailIf(scheduler.UpdateCount != start + 4 ||
                Math.Abs(scheduler.Remainder - 0.5) > 1e-9 ||
                !_roomDebug.Text.Contains("FF x4", StringComparison.Ordinal),
                "F5 did not run four complete application updates, retain the " +
                "partial update, and display its enabled indicator.");

            key.Echo = true;
            _Input(key);
            key.Echo = false;
            key.Pressed = false;
            _Input(key);
            base._Process(2.0 / 60.0);
            FailIf(scheduler.UpdateCount != start + 12,
                "F5 repeat/release changed fast forward, or a batched host " +
                "frame did not run eight complete updates.");
        }
        finally
        {
            key.Echo = false;
            key.Pressed = true;
            _Input(key);
        }
        base._Process(1.0 / 120.0);
        FailIf(scheduler.UpdateCount != start + 13 ||
            Math.Abs(scheduler.Remainder) > 1e-9 ||
            _roomDebug.Text.Contains("FF x4", StringComparison.Ordinal),
            "The second F5 press did not restore normal speed, preserve the " +
            "pending partial update, and clear the fast-forward indicator.");
    }

    private static (int HeldAttackUpdates, int PressedAttackUpdates) RunSchedulerRegression(
        int updateCount,
        bool batched)
    {
        var scheduler = new ApplicationFixedUpdateScheduler();
        int heldAttackUpdates = 0;
        int pressedAttackUpdates = 0;
        int snapshotIndex = 0;

        void Advance()
        {
            string[] justPressed = snapshotIndex == 0
                ? new[] { "attack" }
                : Array.Empty<string>();
            var snapshot = new ApplicationInputSnapshot(
                new[] { "attack" },
                justPressed,
                Vector2.Zero);
            snapshotIndex++;
            Input.BeginOriginalUpdate(snapshot);
            try
            {
                if (Input.IsActionPressed("attack"))
                    heldAttackUpdates++;
                if (Input.IsActionJustPressed("attack"))
                    pressedAttackUpdates++;
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
        return (heldAttackUpdates, pressedAttackUpdates);
    }
}
