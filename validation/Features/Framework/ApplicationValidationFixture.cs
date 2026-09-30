using Godot;
using System;

namespace oracleofages;

/// <summary>Drives the real application loop with explicit host samples.</summary>
internal sealed class ApplicationValidationFixture(GameRoot root)
{
    internal long UpdateCount => root.ApplicationUpdates.UpdateCount;
    internal double Remainder => root.ApplicationUpdates.Remainder;

    internal void Capture(string[] held, string[] pressed, Vector2 movement) =>
        root.ApplicationInput.CaptureSample(held, pressed, movement);

    internal int Advance(double elapsed, Action? afterUpdate = null)
    {
        if (afterUpdate is null)
            return root.ApplicationUpdates.Advance(elapsed, root.AdvanceApplicationUpdate);
        return root.ApplicationUpdates.Advance(elapsed, () =>
        {
            root.AdvanceApplicationUpdate();
            afterUpdate();
        });
    }

    internal void Step(int updates, Vector2 movement,
        string[]? held = null, string[]? pressed = null, bool batched = false,
        Action? afterUpdate = null)
    {
        Capture(held ?? [], pressed ?? [], movement);
        if (batched)
            Advance(updates / 60.0, afterUpdate);
        else
            for (int update = 0; update < updates; update++)
                Advance(1.0 / 60.0, afterUpdate);
    }

    internal void ResetGameplay()
    {
        OracleSaveData save = OracleSaveData.CreateStandardGame();
        // Retail gameplay follows file naming. Individual name scenarios may
        // override this valid one-to-five-character name after initialization.
        save.SetLinkName("Link");
        root.InitializeTransientSession(save);
    }
}
