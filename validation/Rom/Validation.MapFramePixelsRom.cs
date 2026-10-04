using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMapFramePixelsRom() => ValidateMapMenuRom(false, compareOam: true, comparePixels: true, compareFramePixels: true);
    private void ValidateInteriorMapFramePixelsRom() => ValidateMapMenuRom(true, compareOam: true, comparePixels: true, compareFramePixels: true);
    private void ValidateLargeInteriorMapFramePixelsRom() => ValidateMapMenuRom(true, true, true, comparePixels: true, compareFramePixels: true);
    private void ValidateRetainedDungeonMapFramePixelsRom() => RunDungeonMapRetainedMinimapRom(true, comparePixels: true, compareFramePixels: true);
    private void ValidateGaleMapFramePixelsRom() => RunGaleMenuNavigationRom(true, true);
    private void ValidateDungeonMapFramePixelsRom() => RunDungeonMapInitializationRom(false, true, comparePixels: true, compareFramePixels: true);
    private void ValidateAlternateDungeonMapFramePixelsRom() => RunDungeonMapInitializationRom(true, true, comparePixels: true, compareFramePixels: true);

    private void CompareMapFramePixelsRom(MenuRom rom, ref Texture2D? previousBackground,
        ref byte[]? previousCommands, string context)
    {
        var background = (Texture2D)typeof(MapScreen).GetField("_background",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_mapScreen)!;
        int count = Math.Min(40, rom[0xff9f] / 4);
        byte[] commands = Enumerable.Range(0, count * 4).Select(offset => rom[0xcb00 + offset]).ToArray();
        if (ReferenceEquals(background, previousBackground) && previousCommands is not null &&
            commands.SequenceEqual(previousCommands)) return;
        previousBackground = background; previousCommands = commands;
        int[] expected = new int[160 * 144];
        byte[] bgShades = new byte[expected.Length], bgAttributes = new byte[expected.Length];
        for (int row = 0; row < 18; row++)
        for (int column = 0; column < 20; column++)
        {
            int tile = rom.MapTile(column, row), flags = rom.MapAttribute(column, row);
            int source = 0x9000 + unchecked((sbyte)tile) * 16, bank = (flags >> 3) & 1;
            for (int y = 0; y < 8; y++)
            {
                int sourceY = (flags & 0x40) == 0 ? y : 7 - y;
                int low = rom.MapGfx(bank, source + sourceY * 2), high = rom.MapGfx(bank, source + sourceY * 2 + 1);
                for (int x = 0; x < 8; x++)
                {
                    int sourceX = (flags & 0x20) == 0 ? x : 7 - x;
                    int shade = (low >> (7 - sourceX) & 1) | (high >> (7 - sourceX) & 1) << 1;
                    int pixel = (row * 8 + y) * 160 + column * 8 + x;
                    expected[pixel] = rom.HudColor(false, flags & 7, shade);
                    bgShades[pixel] = (byte)shade; bgAttributes[pixel] = (byte)flags;
                }
            }
        }
        // Independently select the first ten native OAM entries by Y, then
        // resolve the first opaque OBJ pixel before its BG priority check.
        // Offscreen X and transparent pixels still consume scanline slots.
        bool[] occupied = new bool[expected.Length];
        for (int y = 0; y < 144; y++)
        {
            int selected = 0;
            for (int sprite = 0; sprite < count; sprite++)
            {
                int offset = sprite * 4, localY = y - (commands[offset] - 16);
                if ((uint)localY >= 16) continue;
                if (++selected > 10) break;
                int flags = commands[offset + 3];
                int sourceY = (flags & 0x40) == 0 ? localY : 15 - localY;
                int source = 0x8000 + (commands[offset + 2] & 0xfe) * 16 + sourceY * 2;
                int bank = (flags >> 3) & 1;
                int low = rom.MapGfx(bank, source), high = rom.MapGfx(bank, source + 1);
                for (int localX = 0; localX < 8; localX++)
                {
                    int x = commands[offset + 1] - 8 + localX;
                    if ((uint)x >= 160) continue;
                    int pixel = y * 160 + x;
                    if (occupied[pixel]) continue;
                    int sourceX = (flags & 0x20) == 0 ? localX : 7 - localX;
                    int shade = (low >> (7 - sourceX) & 1) | (high >> (7 - sourceX) & 1) << 1;
                    if (shade == 0) continue;
                    occupied[pixel] = true;
                    if (bgShades[pixel] == 0 || ((flags | bgAttributes[pixel]) & 0x80) == 0)
                        expected[pixel] = rom.HudColor(true, flags & 7, shade);
                }
            }
        }
        // This is the image _Draw uploads and presents. Native expectations
        // contain no imported atlas, runtime command builder or palette data.
        using Image frame = _mapScreen.ComposeImage();
        byte[] actual = frame.GetData();
        for (int pixel = 0; pixel < expected.Length; pixel++)
        {
            int offset = pixel * 4;
            int observed = (actual[offset] * 31 + 127) / 255 |
                ((actual[offset + 1] * 31 + 127) / 255) << 5 |
                ((actual[offset + 2] * 31 + 127) / 255) << 10;
            if (observed != expected[pixel] || actual[offset + 3] != 255) FailIf(true,
                context + $": composed pixel ({pixel % 160},{pixel / 160}) runtime=${observed:x4}, native=${expected[pixel]:x4}, OAM={count}.");
        }
    }
}
