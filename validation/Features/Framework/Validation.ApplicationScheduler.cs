using Godot;
using System;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private static void ValidateApplicationFixedUpdateScheduler()
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

        GD.Print(
            "Validated application-owned 60 Hz update counts, split/" +
            "batched equivalence, and " +
            "single-owner input edges, including absent gameplay debug actions.");
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
