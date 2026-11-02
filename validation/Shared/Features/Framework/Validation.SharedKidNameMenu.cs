
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedKidNameMenu(GameRoot root, bool batched)
    {
        var engine = root._gameplaySession!;
        var save = engine.Rooms.SaveData;
        var memory = engine.Rooms.RuntimeState;
        var menu = engine.Menus.KidName;
        var player = root._scene.Player;
        var app = new ApplicationValidationFixture(root);
        byte[] previousName = new byte[6];
        for (int index = 0; index < 6; index++) previousName[index] = save.ReadField("wKidName", index);
        int previousResult = memory.ReadField("wTextInputResult");
        save.WriteFields("wKidName", new byte[6]);
        save.CommitInventoryChange();
        Vector2 position = player.PrecisePosition;
        FailIf(!menu.Open(), $"{save.Game}: MENU_KIDNAME must use the active session's common lifecycle.");
        Step(22);
        FailIf(menu.Screen is not { NameCursor: 0, NameEntryPosition: 0, EnteredName.Length: 0 } ||
            menu.Screen.AssetRoot != engine.Rooms.AssetRoot || !engine.Menus.Pause.IsLeased,
            $"{save.Game}: native kid-name entry must use the selected pack and zero both cursors.");
        Tap("attack");
        FailIf(menu.Screen.EnteredName != "A", $"{save.Game}: the common file keyboard must enter uppercase A at cursor0.");
        Tap("inventory");
        FailIf(save.ChildName.Length != 0 || menu.Screen.NameLowerChoice != 2,
            $"{save.Game}: first Start must select OK without writing the candidate.");
        Tap("inventory");
        FailIf(save.ChildName.Length != 0, $"{save.Game}: submitting input must defer the native mode2 commit to the next update.");
        Step();
        FailIf(save.ChildName != "A" || memory.ReadField("wTextInputResult") != 0 || save.ChildNamed,
            $"{save.Game}: native mode2 must copy the candidate and return0 without changing family progression.");
        Step(22);
        FailIf(menu.IsActive || engine.Menus.Pause.IsLeased || player.PrecisePosition != position,
            $"{save.Game}: MENU_KIDNAME must freeze gameplay through the closing update and release its common pause.");
        FailIf(!menu.Open(), $"{save.Game}: the shared name menu must reopen after completion.");
        Step(5); menu.Cancel();
        FailIf(menu.IsActive || !engine.Menus.Lifecycle.IsIdle || engine.Menus.Pause.IsLeased || save.ChildName != "A",
            $"{save.Game}: cancelling an opening menu must release ownership without altering the committed candidate.");
        save.WriteFields("wKidName", previousName);
        save.CommitInventoryChange();
        memory.SetField("wTextInputResult", (byte)previousResult);

        void Step(int count = 1) => app.Step(count, Vector2.Zero, batched: batched);
        void Tap(string action) => app.Step(1, Vector2.Zero, [action], [action], batched);
    }
}
