
namespace Oracles;

/// <summary>Drives the real application loop with explicit host samples.</summary>
internal sealed class ApplicationValidationFixture(GameRoot root, Action? updateObserver = null)
{
    internal long UpdateCount => root.ApplicationUpdates.UpdateCount;
    internal double Remainder => root.ApplicationUpdates.Remainder;

    internal void Capture(string[] held, string[] pressed, Vector2 movement)
    {
        // Synthetic movement supplies the same D-pad bits as a physical host.
        var keys = new System.Collections.Generic.List<string>(held);
        if (movement.X > 0) keys.Add("move_right");
        if (movement.X < 0) keys.Add("move_left");
        if (movement.Y > 0) keys.Add("move_down");
        if (movement.Y < 0) keys.Add("move_up");
        root.ApplicationInput.CaptureSample(keys, pressed, movement);
    }

    internal int Advance(double elapsed, Action? afterUpdate = null)
    {
        if (afterUpdate is null && updateObserver is null)
            return root.ApplicationUpdates.Advance(elapsed, root.AdvanceApplicationUpdate);
        return root.ApplicationUpdates.Advance(elapsed, () =>
        {
            root.AdvanceApplicationUpdate();
            updateObserver?.Invoke();
            afterUpdate?.Invoke();
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
        OracleSaveData save = OracleSaveData.CreateStandardGame(OracleGame.Ages);
        // Retail gameplay follows file naming. Individual name scenarios may
        // override this valid one-to-five-character name after initialization.
        save.SetLinkName("Link");
        root.InitializeTransientSession(save);
    }
}
