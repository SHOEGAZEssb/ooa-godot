using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateBackgroundPaletteState()
    {
        LoadValidationRoom(5, 0x0b);
        OracleRoomData room = _rooms.CurrentRoom;
        var palette1Pixel = new Vector2I(0, 0);

        FailIf(
            BackgroundPaletteState.PaletteCount != 8 ||
            BackgroundPaletteState.ColorsPerPalette != 4 ||
            (room.GetBackgroundAttributeForValidation(0, 0) & 0x07) != 1,
            "Room 5:0b did not retain its source BG palette-1 attribute.");

        Color common0Shade1 = room.ResolveBackgroundPaletteColor(0, 1);
        Color common0Shade2 = room.ResolveBackgroundPaletteColor(0, 2);
        Color tileset2BeforeFade = room.ResolveBackgroundPaletteColor(2, 1);

        _dialogue.ShowGameplayMessage("Normal.", _player.Position.Y);
        Color ordinaryPalette1Shade0 = GbcPaletteColor(0x1f, 0x0e, 0x04);
        Color ordinaryPalette1Shade1 = GbcPaletteColor(0x04, 0x15, 0x1f);
        Color ordinaryPalette1Shade2 = GbcPaletteColor(0x08, 0x1f, 0x00);
        Color ordinaryRenderedShade0 =
            GbcRenderedColor(0x1f, 0x0e, 0x04);
        FailIf(
            !room.ResolveBackgroundPaletteColor(1, 0).IsEqualApprox(
                ordinaryPalette1Shade0) ||
            !room.ResolveBackgroundPaletteColor(1, 1).IsEqualApprox(
                ordinaryPalette1Shade1) ||
            !room.ResolveBackgroundPaletteColor(1, 2).IsEqualApprox(
                ordinaryPalette1Shade2),
            $"PALH_0e BG slot 1 mismatch: " +
            $"{room.ResolveBackgroundPaletteColor(1, 0)}, " +
            $"{room.ResolveBackgroundPaletteColor(1, 1)}, " +
            $"{room.ResolveBackgroundPaletteColor(1, 2)}.");
        FailIf(
            !_dialogue.ResolvedTextColorForValidation(0).IsEqualApprox(
                common0Shade2) ||
            !_dialogue.ResolvedTextColorForValidation(2).IsEqualApprox(
                ordinaryPalette1Shade0) ||
            !_dialogue.ResolvedTextColorForValidation(4).IsEqualApprox(
                ordinaryPalette1Shade2),
            $"PALH_0e dialogue color mismatch: " +
            $"{_dialogue.ResolvedTextColorForValidation(0)}, " +
            $"{_dialogue.ResolvedTextColorForValidation(2)}, " +
            $"{_dialogue.ResolvedTextColorForValidation(4)}.");
        FailIf(
            !room.GetRenderedPixelForValidation(palette1Pixel).IsEqualApprox(
                ordinaryRenderedShade0),
            $"PALH_0e did not rerender room 5:0b's palette-1 tile: " +
            $"{room.GetRenderedPixelForValidation(palette1Pixel)}.");

        room.SetTemporaryBackgroundPaletteOffset(-16);
        FailIf(
            !room.ResolveBackgroundPaletteColor(0, 1).IsEqualApprox(
                common0Shade1) ||
            !room.ResolveBackgroundPaletteColor(1, 0).IsEqualApprox(
                ordinaryPalette1Shade0) ||
            room.ResolveBackgroundPaletteColor(2, 1).IsEqualApprox(
                tileset2BeforeFade),
            "The room-darkening palette thread changed BG slot 0/1 or failed " +
            "to change tileset slot 2.");
        room.SetTemporaryBackgroundPaletteOffset(0);

        _dialogue.Close();
        FailIf(
            _player.ZIndex != Player.NormalZIndex ||
            !room.ResolveBackgroundPaletteColor(1, 0).IsEqualApprox(
                ordinaryPalette1Shade0) ||
            !room.GetRenderedPixelForValidation(palette1Pixel).IsEqualApprox(
                ordinaryRenderedShade0),
            "Closing ordinary text did not retain PALH_0e in hardware BG slot 1.");

        _dialogue.ShowGameplayMessageWithFlags(
            "Alternate.", _player.Position.Y, textboxFlags: 0x04);
        Color alternatePalette1Shade0 = GbcPaletteColor(0x1d, 0x01, 0x03);
        Color alternatePalette1Shade2 = GbcPaletteColor(0x1f, 0x1a, 0x11);
        Color alternateRenderedShade0 =
            GbcRenderedColor(0x1d, 0x01, 0x03);
        FailIf(
            _player.ZIndex != Player.AlternateTextboxPaletteZIndex ||
            !room.ResolveBackgroundPaletteColor(1, 0).IsEqualApprox(
                alternatePalette1Shade0) ||
            !_dialogue.ResolvedTextColorForValidation(0).IsEqualApprox(
                alternatePalette1Shade2) ||
            !_dialogue.ResolvedTextColorForValidation(2).IsEqualApprox(
                alternatePalette1Shade0) ||
            !_dialogue.ResolvedTextColorForValidation(4).IsEqualApprox(
                alternatePalette1Shade2) ||
            !room.GetRenderedPixelForValidation(palette1Pixel).IsEqualApprox(
                alternateRenderedShade0),
            "TEXTBOXFLAG_ALTPALETTE1 did not load PALH_0d, rerender the " +
            "palette-1 room tile, and queue Link above other world objects.");

        _dialogue.Close();
        FailIf(
            _player.ZIndex != Player.NormalZIndex ||
            !room.ResolveBackgroundPaletteColor(1, 0).IsEqualApprox(
                alternatePalette1Shade0) ||
            !room.GetRenderedPixelForValidation(palette1Pixel).IsEqualApprox(
                alternateRenderedShade0),
            "Closing alternate text did not restore Link priority while " +
            "retaining PALH_0d in hardware BG slot 1.");

        _dialogue.ShowGameplayMessage("Normal again.", _player.Position.Y);
        _dialogue.Close();
        FailIf(
            !room.ResolveBackgroundPaletteColor(1, 0).IsEqualApprox(
                ordinaryPalette1Shade0) ||
            !room.GetRenderedPixelForValidation(palette1Pixel).IsEqualApprox(
                ordinaryRenderedShade0),
            "The next ordinary textbox did not replace retained PALH_0d with " +
            "PALH_0e at the original initTextbox boundary.");

        GD.Print(
            "Validated eight live BG palette slots, PALH_0e/PALH_0d slot-1 " +
            "writes over room 5:0b, slots-2-7-only darkening, retained " +
            "post-textbox state, and Link's alternate-textbox draw priority.");
    }

    private static Color GbcPaletteColor(int red, int green, int blue) =>
        new(red / 31.0f, green / 31.0f, blue / 31.0f);

    private static Color GbcRenderedColor(int red, int green, int blue) =>
        Color.Color8(
            (byte)Mathf.FloorToInt(red * 255.0f / 31.0f),
            (byte)Mathf.FloorToInt(green * 255.0f / 31.0f),
            (byte)Mathf.FloorToInt(blue * 255.0f / 31.0f));
}
