using Godot;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateUnderwaterWaves()
    {
        // Literal source golden for the shared sine table also protects the
        // larger amplitudes used by the arrival and time-warp presentations.
        int[] sourceQuarter =
        [
            0x00, 0x0d, 0x19, 0x26, 0x32, 0x3e, 0x4a, 0x56,
            0x62, 0x6d, 0x79, 0x84, 0x8e, 0x98, 0xa2, 0xac,
            0xb5, 0xbe, 0xc6, 0xce, 0xd5, 0xdc, 0xe2, 0xe7,
            0xed, 0xf1, 0xf5, 0xf8, 0xfb, 0xfd, 0xff, 0xff
        ];
        for (int index = 0; index < sourceQuarter.Length; index++)
            FailIf(ScreenWaveData.Shared.Offset(0xff, index) !=
                (sourceQuarter[index] == 0 ? 0 : sourceQuarter[index] - 1),
                $"Imported screen wave coefficient ${index:x2} differs from bank1.s@sineWave.");

        // Independently traced from bank1.s: multiplication high byte with
        // amplitude $02 turns indices $0b-$34 into +1 and $4b-$74 into -1.
        for (int index = 0; index < 256; index++)
        {
            int phase = index & 0x7f;
            int expected = phase is >= 0x0b and <= 0x34 ? 1
                : phase is >= 0x4b and <= 0x74 ? -1 : 0;
            FailIf(ScreenWaveData.Shared.Offset(2, index) != expected,
                $"Underwater SCX wave lost source quantization at index ${index:x2}.");
        }
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(2, 0x90);
            _entities.Clear();
            _player.WarpTo(new(80, 64));
            _transitions.ResetCamera();
            FailIf(!_roomView.UnderwaterWaveActive,
                "Room 2:$90 must initialize underwater waves at room entry.");
            Vector2 position = _player.Position;
            StepGameplayUpdates(130, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                FailIf(!_roomView.UnderwaterWaveActive ||
                    _roomView.UnderwaterWavePhase != ((_entities.FrameCounter - 0x10) & 0x7f) ||
                    _player.Position != position,
                    "Underwater phase must follow wFrameCounter without moving Link.");
            });

            _dialogue.ShowGameplayMessage("Waves continue during text.", _player.Position.Y);
            int phaseBeforeText = _roomView.UnderwaterWavePhase;
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            FailIf(_roomView.UnderwaterWavePhase != ((phaseBeforeText + 3) & 0x7f),
                "Text-frozen objects must still update underwater SCX.");
            _dialogue.Close();

            _inventoryMenu.OpenImmediatelyForValidation();
            int phaseBeforeMenu = _roomView.UnderwaterWavePhase;
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            FailIf(_roomView.UnderwaterWavePhase != phaseBeforeMenu,
                "updateMenus must return before updating underwater waves.");
            _inventoryMenu.CloseImmediatelyForValidation();
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(_roomView.UnderwaterWavePhase != ((_entities.FrameCounter - 0x10) & 0x7f),
                "Returning from a menu must use the shared phase.");

            // Use the real application object/scroll passes, including the
            // finisher's frozen destination and the following ordinary update.
            _transitions.BeginScroll(_player, Vector2I.Right, 0x91);
            FailIf(_roomView.UnderwaterWaveActive,
                "checkDisableUnderwaterWaves must suppress SCX before scrolling.");
            StepGameplayUpdates(_transitions.ScrollTotalFrames - 1, Vector2.Zero,
                batched: batched, afterUpdate: () =>
                FailIf(_roomView.UnderwaterWaveActive, "SCX waves must stay disabled throughout scrolling."));
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(_transitions.ScrollActive || !_roomView.UnderwaterWaveActive,
                "cutscene00 must reinitialize underwater waves on scroll completion.");
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            LoadValidationRoom(0, 0x33);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(_roomView.UnderwaterWaveActive, "Dry rooms must not retain underwater waves.");
            LoadValidationRoom(2, 0x90);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(!_roomView.UnderwaterWaveActive, "Underwater re-entry must restore waves.");

            _currentRoom.SetPositionTileAndCollision(new(8, 8), 0x00, 0, 0);
            using Image background = _currentRoom.RenderUnderwaterBackground();
            FailIf(background.GetWidth() != 256 || background.GetHeight() != 128,
                "Underwater SCX must wrap on the hardware's 256-pixel tilemap.");
            for (int y = 0; y < 128; y++)
            {
                FailIf(background.GetPixel(160, y) != background.GetPixel(0, y & 15) ||
                    background.GetPixel(255, y) != background.GetPixel(15, y & 15),
                    $"Small-room underwater borders must sample cleared storage metatile $00, row {y}.");
            }
            _currentRoom.SetPositionTileAndCollision(new(80, 64), 0xa0, 0, 0);
            using Image updated = _currentRoom.RenderUnderwaterBackground();
            FailIf(updated.GetPixel(80, 64) != _currentRoom.GetRenderedPixelForValidation(new(80, 64)),
                "Live room graphics must update the underwater background too.");

            LoadValidationRoom(5, 0x2c);
            _entities.Clear();
            _player.WarpTo(new(160, 130));
            _transitions.ResetCamera();
            Vector2 screen = _transitions.WorldToGameplayScreen(_player.Position);
            int cameraY = (int)(_player.Position.Y - screen.Y);
            FailIf(cameraY == 0 || !_roomView.UnderwaterWaveActive,
                "Room 5:$2c must exercise underwater waves with a vertically scrolled camera.");
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            for (int y = 0; y < 128; y++)
                FailIf(ScreenWaveData.Shared.Offset(2, _roomView.UnderwaterWavePhase + cameraY + y) !=
                    ScreenWaveData.Shared.Offset(2, _entities.FrameCounter + cameraY - 0x10 + y),
                    "Large-room scanlines must include source SCY while retaining world coordinates.");
        }
    }
}
