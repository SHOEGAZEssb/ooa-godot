namespace Oracles;

internal partial class SeasonsOpeningSession
{
    internal VasuShopEvent Vasu { get; private set; } = null!;
    internal RingMenuController RingMenu { get; private set; } = null!;
    internal SecretEntryController RingSecrets { get; private set; } = null!;

    private void InitializeVasu()
    {
        Vasu = new(Rooms, Entities, Inventory, Link, Dialogue, (id, message, position, choice) =>
        {
            LastText = id;
            var screen = DialogueScreenContext.Gameplay(Link.Position.Y, -Transitions.WorldToGameplayScreen(Vector2.Zero).Y);
            if (choice) Dialogue.ShowChoiceMessage(message, screen, textPosition: position, sourceTextId: id);
            else Dialogue.ShowMessage(message, screen, position, sourceTextId: id);
        });
        RingMenu = new(RingMenuScreen, Dialogue, Menus!.Lifecycle, Inventory, Save,
            Entities.Treasures, Vasu.Database, Sound.PlaySound,
            () => StatusBar.LoadCommonGraphics(Room.TilesetFlags));
        RingSecrets = new(InterfaceLayer, Menus.Lifecycle, Save, Inventory, Sound.PlaySound);
        Vasu.SetRingMenuOpener(RingMenu.Open);
        Vasu.SetRingMenuCancellation(RingMenu.CancelOwner);
        Vasu.SetRingSecretMenuOpener(RingSecrets.OpenRingSecret);
        Vasu.SetRingSecretMenuCancellation(RingSecrets.CancelOwner);
        Entities.RoomInitializing += CancelVasuOnRoomInitialization;
    }

    private void CancelVasuOnRoomInitialization(int group, OracleRoomData room)
    {
        if (Vasu.HasState) Dialogue.Close();
        Vasu.Cancel();
    }

    private void ReleaseVasu()
    {
        if (Vasu is null) return;
        Entities.RoomInitializing -= CancelVasuOnRoomInitialization;
        Vasu.Cancel();
        RingMenu.CancelOwner();
        RingSecrets.CancelOwner();
    }

    bool IGameplayForegroundPhases.AdvanceAdditionalMenus(double delta)
    {
        bool secretOwnedFrame = RingSecrets.IsActive;
        RingSecrets.Update(delta);
        if (secretOwnedFrame || RingSecrets.IsActive) return true;
        bool ringOwnedFrame = RingMenu.IsActive;
        RingMenu.Update(delta);
        if (!ringOwnedFrame && !RingMenu.IsActive) return false;
        // runRingMenu includes HUD updates for appraisal and consumes the close callback's update.
        if (RingMenuScreen.Visible && RingMenuScreen.Mode == RingMenuMode.Appraisal) StatusBar.Update(delta);
        return true;
    }
}
