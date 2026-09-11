using System;
using System.Reflection;
using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    // Feed explicit host samples to the actual application scheduler and
    // complete gameplay loop. Godot's native just-pressed flags otherwise
    // persist across synchronous validation calls within one host frame.
    private void StepGameplayUpdates(int updates, Vector2 movement,
        string[]? held = null, string[]? pressed = null, bool batched = false)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot)
            .GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot)
            .GetField("_applicationUpdates", flags)!.GetValue(this)!;
        Action advance = typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!
            .CreateDelegate<Action>(this);
        input.CaptureForValidation(held ?? [], pressed ?? [], movement);
        if (batched)
            scheduler.Advance(updates / 60.0, advance);
        else
            for (int i = 0; i < updates; i++) scheduler.Advance(1.0 / 60.0, advance);
    }

}

