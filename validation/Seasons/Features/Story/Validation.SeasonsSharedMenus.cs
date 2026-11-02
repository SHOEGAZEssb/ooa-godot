
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsSharedMenus(FrontendValidationRoot root, ApplicationValidationFixture application,
        SeasonsOpeningSession session, bool batched)
    {
        GameplayMenus menus = session.Menus ?? throw new InvalidOperationException("The production Seasons entry omitted shared gameplay menus.");
        FailIf(!ReferenceEquals(root._gameplayMenus, menus) || !ReferenceEquals(root._inventoryMenu, menus.Inventory) ||
            !ReferenceEquals(root._mapMenu, menus.Map) || !ReferenceEquals(root._gameplayPause, menus.Pause),
            "The Seasons application and content module must use the same authoritative menu and pause owners.");
        for (int repeat = 0; repeat < 2; repeat++)
        {
            foreach (string action in new[] { "inventory", "map" })
            {
                Vector2 position = session.Link.PrecisePosition;
                Press(action);
                FailIf(!menus.Pause.IsLeased, $"Seasons {action} must acquire the shared pause on the input update.");
                for (int update = 0; update < 40 && !(action == "inventory" ? menus.Inventory.IsOpen : menus.Map.IsOpen); update++)
                    application.Step(1, Vector2.Zero);
                FailIf(!(action == "inventory" ? menus.Inventory.IsOpen : menus.Map.IsOpen),
                    $"Seasons {action} did not reach its shared open state through the production dispatcher.");
                int rng = session.Random.Calls;
                application.Step(4, Vector2.Right, batched: batched);
                FailIf(session.Link.PrecisePosition != position || session.Random.Calls != rng,
                    $"Seasons {action} must freeze world movement and object RNG in split and batched updates.");
                using (Image image = action == "inventory" ? session.InventoryScreen.ComposeImage() : session.MapScreen.ComposeImage())
                    FailIf(image.GetWidth() != 160 || image.GetHeight() != 144,
                        "Production Seasons menus must render native assets in the shared screen-space viewport.");
                Press(action);
                for (int update = 0; update < 40 && menus.Lifecycle.IsActive; update++) application.Step(1, Vector2.Zero);
                FailIf(menus.Lifecycle.IsActive || menus.Pause.IsLeased || session.InventoryScreen.Visible || session.MapScreen.Visible,
                    $"Seasons {action} did not release its shared modal lease on closing/re-entry.");
            }
        }
        Vector2 beforeFlags = session.Link.PrecisePosition;
        Press("debug_flags");
        FailIf(!menus.Flags.IsActive || !session.DebugFlagScreen.Visible || !menus.Pause.IsLeased,
            "Seasons F1 must use the common flag screen, native global-flag catalog and pause lease.");
        application.Step(4, Vector2.Right, batched: batched);
        FailIf(session.Link.PrecisePosition != beforeFlags, "The Seasons flag editor must freeze the real player update.");
        session.DebugFlagScreen.SelectTreasureForValidation("TREASURE_OBJECT_SEED_SATCHEL_00");
        Press("attack");
        FailIf(session.Inventory.SeedSatchelLevel != 1 || session.Inventory.EmberSeeds != 0x20 ||
            !session.Inventory.HasTreasure(0x19) || session.Save.ReadField("wSeedSatchelLevel") != 1,
            "Seasons F1 inventory editing must execute the native shared grant body and publish the selected file's real item/ammunition bytes.");
        Press("attack");
        FailIf(session.Inventory.HasTreasure(0x19), "Repeating the F1 item action must remove its ownership flag and inventory slot.");
        Press("debug_flags");
        FailIf(menus.Flags.IsActive || menus.Pause.IsLeased, "Closing Seasons F1 must restore the common pause state.");
        Press("debug_collision");
        FailIf(!menus.Collision.CollisionsDisabled, "Seasons F2 must toggle the shared collision-debug owner.");
        Press("debug_collision");
        FailIf(menus.Collision.CollisionsDisabled, "Seasons F2 must toggle off repeatedly.");
        session.Inventory.ApplyDamage(4);
        root._Input(new InputEventKey { PhysicalKeycode = Key.F6, Pressed = true });
        FailIf(session.Inventory.HealthQuarters != session.Inventory.MaxHealthQuarters,
            "Seasons F6 must refill health through the common application input handler.");

        void Press(string action) => application.Step(1, Vector2.Zero, [action], [action]);
    }
}
