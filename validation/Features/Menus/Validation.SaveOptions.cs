using Godot;
using System.Reflection;
using System.Threading.Tasks;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private async Task CaptureSaveOptionsScreens()
    {
        string directory = OS.GetEnvironment("OOA_MENU_SCREENSHOT_OUTPUT");
        if (string.IsNullOrEmpty(directory))
            return;
        _inventoryMenu.OpenSaveImmediatelyForValidation();
        for (int i = 0; i < 3; i++)
            _saveQuitScreen.Move(1);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using (Image image = GetViewport().GetTexture().GetImage())
            image.SavePng(System.IO.Path.Combine(directory, "save-menu-full.png"));
        _inventoryMenu.SelectSaveOptionForValidation();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using (Image image = GetViewport().GetTexture().GetImage())
            image.SavePng(System.IO.Path.Combine(directory, "options-menu-full.png"));
        _inventoryMenu.SelectSaveOptionForValidation();
        _saveQuitScreen.Move(1);
        _inventoryMenu.SelectSaveOptionForValidation();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using (Image image = GetViewport().GetTexture().GetImage())
            image.SavePng(System.IO.Path.Combine(directory, "options-menu-toggled-full.png"));
        _inventoryMenu.CloseImmediatelyForValidation();
    }

    private void ValidateSaveOptions()
    {
        ValidateHudPreferencePersistence();
        FailIf(_presentationSettings.HudBottom || _hud.Position.Y != 0,
            "HUD placement must default to the top in an isolated session.");
        LoadValidationRoom(4, 0x09);
        _player.WarpTo(new Vector2(20, 24));
        _player.Face(Vector2I.Left);
        foreach (bool batched in new[] { false, true })
        {
            _debugCollision.SetEnabled(false);
            _gameplayPause.SetRoomOverlayEnabled(true);
            int saves = _saveWriteRequests;
            Vector2 position = _player.Position;
            _inventoryMenu.BeginSaveOpeningForValidation();
            StepGameplayUpdates(22, Vector2.Zero, batched: batched);
            FailIf(!_inventoryMenu.SaveMenuOpen || _saveQuitScreen.Cursor != 0,
                "Options parent did not open through both 11-update fades.");
            for (int i = 0; i < 3; i++)
                StepGameplayUpdates(1, Vector2.Zero, pressed: ["move_down"]);
            FailIf(_saveQuitScreen.Cursor != 3 || _saveQuitScreen.Move(1),
                "The save menu must clamp at its fourth, Options entry.");
            DumpSaveOptionsBackground("save-options-parent");
            ValidateNativeOptionsLettering();
            StepGameplayUpdates(3, Vector2.Zero, held: ["attack"], pressed: ["attack"], batched: batched);
            FailIf(!_saveQuitScreen.OptionsOpen || _debugCollision.CollisionsDisabled ||
                _saveWriteRequests != saves || _saveQuitScreen.DelayCounter != 0,
                "Opening Options leaked its A edge into a toggle or a save.");
            ulong disabled = _saveQuitScreen.BackgroundPixelHash;
            using Image beforeToggle = SaveOptionsBackground();
            FailIf(beforeToggle.GetPixel(70, 60) != Colors.Black ||
                beforeToggle.GetPixel(73, 60) == Colors.Black ||
                beforeToggle.GetPixel(75, 70) != Colors.Black ||
                beforeToggle.GetPixel(111, 60) != Colors.Black ||
                beforeToggle.GetPixel(111, 70) == Colors.Black,
                "Noclip L and OFF F must preserve the native two-pixel stem and complete bar endpoints.");
            DumpSaveOptionsBackground("save-options-default");
            StepGameplayUpdates(3, Vector2.Left, held: ["attack"], pressed: ["attack"], batched: batched);
            using (Image afterToggle = SaveOptionsBackground())
            {
                for (int y = 0; y < 144; y++)
                for (int x = 0; x < 160; x++)
                    if (beforeToggle.GetPixel(x, y) != afterToggle.GetPixel(x, y))
                        FailIf(x < 96 || x >= 128 || y < 56 || y >= 72,
                            "Noclip ON/OFF must change only the value on its single selectable row.");
                FailIf(afterToggle.GetPixel(89, 62) != Colors.Black ||
                    afterToggle.GetPixel(90, 63) != Colors.Black ||
                    afterToggle.GetPixel(89, 67) != Colors.Black ||
                    afterToggle.GetPixel(94, 86) != Colors.Black,
                    "The single-row NOCLIP and ROOM ID labels lost their colon dots.");
            }
            FailIf(!_debugCollision.CollisionsDisabled || _player.Position != position ||
                _player.IsProcessing() || _player.IsPhysicsProcessing() ||
                !_gameplayPause.IsOwnedBy(_inventoryMenu) || _roomDebug.Visible ||
                _saveQuitScreen.BackgroundPixelHash == disabled ||
                _playerWorld.ResolveMovement(position, Vector2.Left, allowWallSlide: true) != Vector2.Left,
                "Options noclip must toggle once, bypass the actual wall, and retain the menu pause.");
            StepGameplayUpdates(1, Vector2.Zero, pressed: ["debug_collision"]);
            FailIf(_debugCollision.CollisionsDisabled || _saveQuitScreen.BackgroundPixelHash != disabled,
                "F2 and Options must share one noclip value and refresh the displayed setting.");
            StepGameplayUpdates(1, Vector2.Zero, pressed: ["move_down"]);
            StepGameplayUpdates(3, Vector2.Zero, pressed: ["attack"], batched: batched);
            FailIf(_gameplayPause.RoomOverlayEnabled || _roomDebug.Visible || _saveQuitScreen.OptionsCursor != 1,
                "Turning off the room overlay must edit the paused restoration state.");
            DumpSaveOptionsBackground("save-options-overlay-off");
            StepGameplayUpdates(1, Vector2.Zero, pressed: ["move_down"]);
            FailIf(_saveQuitScreen.OptionsCursor != 2 || _saveQuitScreen.Move(1),
                "Options must clamp at the third, HUD placement entry.");
            ulong topHud = _saveQuitScreen.BackgroundPixelHash;
            StepGameplayUpdates(3, Vector2.Zero, pressed: ["attack"], batched: batched);
            FailIf(!_presentationSettings.HudBottom || _hud.Position.Y != 0 ||
                topHud == _saveQuitScreen.BackgroundPixelHash,
                "HUD selection must toggle once and retain the full-screen menu layout.");
            DumpSaveOptionsBackground("save-options-hud-bottom");
            StepGameplayUpdates(3, Vector2.Zero, pressed: ["item"], batched: batched);
            FailIf(_saveQuitScreen.OptionsOpen || !_inventoryMenu.SaveMenuOpen ||
                _saveQuitScreen.Cursor != 3 || !_gameplayPause.IsOwnedBy(_inventoryMenu),
                "B must return to Options in the parent without releasing the pause.");
            StepGameplayUpdates(1, Vector2.Zero, pressed: ["item"]);
            StepGameplayUpdates(22, Vector2.Zero, batched: batched);
            FailIf(_inventoryMenu.IsActive || _roomDebug.Visible ||
                !_player.IsProcessing() || !_player.IsPhysicsProcessing() || _saveWriteRequests != saves,
                "Cancel must restore gameplay with the requested overlay state and no save writes.");
            ValidateHudGameplayPlacement(bottom: true);

            _inventoryMenu.OpenSaveImmediatelyForValidation();
            for (int i = 0; i < 3; i++)
                StepGameplayUpdates(1, Vector2.Zero, pressed: ["move_down"]);
            StepGameplayUpdates(1, Vector2.Zero, pressed: ["attack"]);
            StepGameplayUpdates(1, Vector2.Zero, pressed: ["move_down"]);
            StepGameplayUpdates(1, Vector2.Zero, pressed: ["attack"]);
            FailIf(!_gameplayPause.RoomOverlayEnabled || _roomDebug.Visible,
                "Reopening Options must allow enabling the hidden overlay without exposing it in the menu.");
            StepGameplayUpdates(1, Vector2.Zero, pressed: ["move_down"]);
            FailIf(!_presentationSettings.HudBottom,
                "Reopening Options must retain the bottom HUD preference.");
            StepGameplayUpdates(3, Vector2.Zero, pressed: ["attack"], batched: batched);
            StepGameplayUpdates(1, Vector2.Zero, pressed: ["item"]);
            StepGameplayUpdates(1, Vector2.Zero, pressed: ["item"]);
            StepGameplayUpdates(22, Vector2.Zero, batched: batched);
            FailIf(!_roomDebug.Visible || _saveWriteRequests != saves,
                "The enabled overlay was not restored after leaving the reopened menu.");
            ValidateHudGameplayPlacement(bottom: false);
        }
        _saveQuitScreen.Open(gameOver: true);
        _saveQuitScreen.Move(1);
        _saveQuitScreen.Move(1);
        FailIf(_saveQuitScreen.Move(1) || _saveQuitScreen.OptionsOpen,
            "Game over must retain the original three actions.");
        _saveQuitScreen.Close();
        GD.Print("Validated save Options, shared noclip, paused overlay settings, cancellation, repeated use and batched input edges.");
    }

    private void ValidateHudGameplayPlacement(bool bottom)
    {
        int fieldTop = bottom ? 0 : 16;
        FailIf(_hud.Position != new Vector2(0, bottom ? 128 : 0) ||
            _warpFade.Position.Y != fieldTop || _warpFade.Size != new Vector2(160, 128) ||
            _scene.RoomLoadReveal.Position.Y != fieldTop ||
            _roomDebug.Position.Y != fieldTop ||
            _scene.DebugObjectSpawnerScreen.Position.Y != fieldTop,
            "HUD placement must move the complete 160x128 gameplay field and its overlays.");
        Vector2 world = _player.Position;
        Vector2 sourceScreen = _transitions.WorldToGameplayScreen(world);
        // Synchronous scenarios share one rendered frame. Camera2D's cached
        // canvas transform need not have caught up; check its projection inputs.
        Vector2 projected = world - _roomCamera.Position + new Vector2(80, 72);
        FailIf(!projected.IsEqualApprox(sourceScreen + new Vector2(0, fieldTop)),
            "Camera projection must place Link inside the selected gameplay field.");
        _dialogue.ShowGameplayMessage("HUD", 24, 2);
        float gameplayY = _dialogue.Position.Y;
        Vector2 shake = _roomCamera.Offset;
        _roomCamera.Offset = new Vector2(1, -2);
        _scene.ApplyHudPlacement(!bottom, _transitions);
        FailIf(_player.Position != world || _roomCamera.Offset != new Vector2(1, -2) ||
            _transitions.WorldToGameplayScreen(world) != sourceScreen ||
            _dialogue.Position.Y != gameplayY + (bottom ? 16 : -16),
            "HUD placement must shift open gameplay dialogue without changing world or source camera coordinates.");
        _dialogue.ShowMessage("HUD", 24, 2);
        float fullScreenY = _dialogue.Position.Y;
        _scene.ApplyHudPlacement(bottom, _transitions);
        _roomCamera.Offset = shake;
        FailIf(_dialogue.Position.Y != fullScreenY,
            "Full-screen dialogue must retain its imported screen position.");
        _dialogue.Close();
    }

    private static void ValidateHudPreferencePersistence()
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            $"ooa-presentation-{System.Guid.NewGuid():N}.cfg");
        try
        {
            var settings = new PresentationSettings();
            settings.Load(path);
            FailIf(settings.HudBottom, "Missing preferences must default to a top HUD.");
            foreach (bool bottom in new[] { true, false })
            {
                settings.HudBottom = bottom;
                FailIf(settings.Save(path) != Error.Ok, "HUD preference could not be saved.");
                var restored = new PresentationSettings();
                restored.Load(path);
                FailIf(restored.HudBottom != bottom, "HUD preference did not survive a settings reload.");
            }
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }

    private void DumpSaveOptionsBackground(string name)
    {
        FailIf(!_saveQuitScreen.BackgroundIsOpaque, "Save Options background must cover the full viewport.");
        string directory = OS.GetEnvironment("OOA_MENU_AUDIT_OUTPUT");
        if (string.IsNullOrEmpty(directory))
            return;
        using Image image = SaveOptionsBackground();
        image.SavePng(System.IO.Path.Combine(directory, name + ".png"));
    }

    private void ValidateNativeOptionsLettering()
    {
        using Image image = SaveOptionsBackground();
        // The first O in OPTIONS must preserve the native CONTINUE strokes.
        // Wood remains owned by the panel, avoiding seams between letter cells.
        for (int y = 3; y < 16; y++)
        for (int x = 0; x < 8; x++)
            FailIf((image.GetPixel(54 + x, 120 + y) == Colors.Black) !=
                (image.GetPixel(56 + x, 48 + y) == Colors.Black),
                "OPTIONS lost native CONTINUE letter strokes.");
        // Native capitals use a four-pixel-wide top on the O, then six pixels
        // on its next row; I advances only four pixels, not eight.
        FailIf(image.GetPixel(57, 124) != Colors.Black ||
            image.GetPixel(56, 125) != Colors.Black ||
            image.GetPixel(80, 124) != Colors.Black ||
            image.GetPixel(81, 124) != Colors.Black,
            "OPTIONS no longer preserves native O/I stroke positions and proportional spacing.");
    }

    private Image SaveOptionsBackground() =>
        // The headless texture backend can return the same Image wrapper to
        // multiple readers. Own a copy so opacity checks cannot dispose it.
        (Image)((Texture2D)typeof(SaveQuitScreen).GetField("_background",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_saveQuitScreen)!).GetImage().Duplicate();
}
