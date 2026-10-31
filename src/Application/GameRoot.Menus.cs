using Godot;
using System;

namespace Oracles;

public partial class GameRoot
{
    internal GameplaySession? _gameplaySession;
    internal GameplayMenus? _gameplayMenus => _gameplaySession?.Menus;
    internal GameplayPauseController _gameplayPause => _gameplayMenus!.Pause;
    internal OracleMenuLifecycle _menuLifecycle => _gameplayMenus!.Lifecycle;
    internal MapMenuController _mapMenu => _gameplayMenus!.Map;
    internal InventoryMenuController _inventoryMenu => _gameplayMenus!.Inventory;
    internal DebugFlagMenuController _debugFlagMenu => _gameplayMenus!.Flags;
    internal DebugCollisionController _debugCollision => _gameplayMenus!.Collision;
    public bool MapMenuOpen => _gameplayMenus?.Map.IsActive ?? false;
    public bool InventoryMenuOpen => _gameplayMenus?.Inventory.IsActive ?? false;
    public bool DebugFlagMenuOpen => _gameplayMenus?.Flags.IsActive ?? false;

    private void BindGameplayMenus(GameplayMenus menus, Action release, Action<OracleSaveData> restore)
    {
        _releaseActiveGameplay = release;
        _restoreCheckpointGameplay = restore;
        _gameplayPause.SetRoomOverlayEnabled(_presentationSettings.RoomOverlayEnabled);
        menus.Inventory.ConfigureOptions(
            option => option switch
            {
                0 => menus.Collision.CollisionsDisabled,
                1 => menus.Pause.RoomOverlayEnabled,
                2 => _presentationSettings.HudBottom,
                _ => throw new ArgumentOutOfRangeException(nameof(option))
            },
            (option, enabled) =>
            {
                if (option == 0) menus.Collision.SetEnabled(enabled);
                else if (option == 1)
                {
                    _presentationSettings.RoomOverlayEnabled = enabled;
                    menus.Pause.SetRoomOverlayEnabled(enabled);
                }
                else if (option == 2) _presentationSettings.HudBottom = enabled;
                else throw new ArgumentOutOfRangeException(nameof(option));
                if (option is 1 or 2 && _persistSaveData &&
                    _presentationSettings.Save() is var error && error != Error.Ok)
                    GD.PushWarning($"Could not save presentation preference: {error}.");
            });
    }
}
