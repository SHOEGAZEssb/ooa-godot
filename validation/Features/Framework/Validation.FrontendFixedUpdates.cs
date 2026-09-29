using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateFrontendFixedUpdates()
    {
        using var split = new FrontendValidationRoot();
        using var batch = new FrontendValidationRoot();
        AddChild(split);
        AddChild(batch);
        try
        {
            foreach (var root in new[] { split, batch }) root.Initialize();
            // Exercise the host entry point: logo initialization must share
            // the same fixed scheduler as gameplay and advance sound once.
            for (int update = 0; update < 241; update++) split.AdvanceHost(1.0 / 60.0);
            batch.AdvanceHost(241.0 / 60.0);
            FailIf(split._frontendIntro!.Stage != batch._frontendIntro!.Stage ||
                split._frontendIntro.State != batch._frontendIntro.State ||
                split._random.Calls != batch._random.Calls ||
                split._sound.Apu.Clocks != batch._sound.Apu.Clocks,
                "Cold frontend host batching changed controller, RNG or sound progression.");
            foreach (var root in new[] { split, batch })
            {
                bool batched = root == batch;
                root.Step(1, batched, "inventory");
                root.Step(9, batched);
                root.Step(1, batched, "inventory");
                root.Step(32, batched);
                FailIf(root._mainMenu is null || root._mainMenuScreen!.CurrentPage != Page.FileSelect ||
                    root._mainMenuScreen.WhiteFadeOffset != 0 || !root._mainMenuScreen.Visible,
                    "Title handoff did not show file select with its restored palette.");

                for (int attempt = 0; attempt < 2; attempt++)
                {
                    root.Step(1, batched, "inventory");
                    FailIf(root._mainMenu!.CurrentPage != Page.NewFileOptions ||
                        root._mainMenuScreen!.CurrentPage != Page.FileSelect,
                        "Selecting an empty file bypassed its initialization update.");
                    root.Step(1, batched, "attack", "move_down");
                    FailIf(root._mainMenuScreen.CurrentPage != Page.NewFileOptions || root._mainMenu.Cursor != 0,
                        "File initialization accepted input or failed to publish its page.");
                    root.Step(1, batched, "map");
                    root.Step(1, batched);
                    FailIf(root._mainMenuScreen.CurrentPage != Page.FileSelect,
                        "Cancelling new-file options failed to restore file select.");
                }
            }
            FailIf(split._random.Calls != batch._random.Calls || split._sound.Apu.Clocks != batch._sound.Apu.Clocks,
                "Frontend input, initialization or re-entry changed when host updates were batched.");
        }
        finally { split.Free(); batch.Free(); }
        GD.Print("Validated fixed frontend host updates, title handoff, menu initialization/input gates and re-entry.");
    }
}
