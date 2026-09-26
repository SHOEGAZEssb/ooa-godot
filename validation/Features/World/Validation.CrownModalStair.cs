using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownModalStair()
    {
        // bank0.updateMenus returns the post-update wOpenedMenuType. When
        // bank2.menuStateFadeIntoGame clears it, cutscene01 continues through
        // updateAllObjects and func_60e9 on that same original update.
        foreach (bool map in new[] { false, true })
        foreach (bool batched in new[] { false, true })
        for (int repeat = 0; repeat < 2; repeat++)
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xa1);
            _player.ApplicationUpdateOwned = true;
            _entities.Clear();
            _saveData.SetGlobalFlag(GlobalFlag.IntroDone);
            _runtimeState.SetWramByte(WramAddress.wWarpsDisabled, 1);
            _player.WarpTo(new(120, 40));
            FailIf(_collision.Collides(_player.Position) || _currentRoom.GetMetatile(new(120, 24)) != 0x44,
                "Menu/stair fixture must approach the actual Crown4:a1/$17 stair.");
            StepGameplayUpdates(15, Vector2.Up, batched: batched);
            FailIf(IsTransitioning || _player.Position != new Vector2(120, 25),
                "Independent warp-disable owner must allow normal movement onto the stair.");
            string open = map ? "map" : "inventory";
            string close = map ? "item" : "inventory";
            bool MenuActive() => map ? _mapMenu.IsActive : _inventoryMenu.IsActive;
            int sounds = _sound.PlayRequestsFor(SoundId.SndEnterCave);
            StepGameplayUpdates(1, Vector2.Zero, [open], [open]);
            FailIf(!MenuActive() || IsTransitioning, "Menu input must acquire ownership before stair selection.");
            _runtimeState.SetWramByte(WramAddress.wWarpsDisabled, 0);
            StepGameplayUpdates(22, Vector2.Zero, batched: batched);
            FailIf(!MenuActive() || IsTransitioning || _player.Position != new Vector2(120, 25),
                "Opening fades and menu ownership must freeze Link and the pending stair.");
            StepGameplayUpdates(1, Vector2.Zero, [close], [close]);
            StepGameplayUpdates(21, Vector2.Zero, batched: batched);
            FailIf(!MenuActive() || IsTransitioning ||
                _sound.PlayRequestsFor(SoundId.SndEnterCave) != sounds,
                "The unfinished return fade must retain menu ownership without selecting the stair.");
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(MenuActive() || !IsTransitioning ||
                _sound.PlayRequestsFor(SoundId.SndEnterCave) != sounds + 1,
                $"Closing {(map ? "Map" : "Inventory")} must resume gameplay and select the stair on its completion update.");
        }
        ReinitializeGameplayForValidation();
    }
}
