using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareRingFieldPixelsRom(MenuRom rom, RingMenuMode mode, ref string? previous,
        ref int[]? previousExpectedPixels, string context, bool useVram = false)
    {
        // Native LCD register-state/interrupt mapping, not the generated ring
        // maps: List uses rows $10/$11 at the top; both begin their window at
        // y=$10. Signed BG addressing starts at $48 (List) or $58 (Appraisal).
        // Optional native LCD-off DMA publishes both retained VRAM maps for
        // window/BG scroll composition. Text and palette fades remain outside.
        int startY = mode == RingMenuMode.List ? 0 : 16;
        var snapshot = new List<byte>();
        if (useVram)
        {
            for (int address = 0x9800; address < 0xa000; address++)
            { snapshot.Add(rom.MapGfx(0, address)); snapshot.Add(rom.MapGfx(1, address)); }
            for (int address = 0xc485; address < 0xc491; address++) snapshot.Add(rom[address]);
        }
        else
        {
            for (int y = startY / 8; y < 11; y++)
            for (int x = 0; x < 20; x++)
            {
                int row = y < 2 ? y + 16 : y - 2;
                snapshot.Add(rom.MapTile(x, row));
                snapshot.Add(rom.MapAttribute(x, row));
            }
        }
        int oamCount = Math.Min(40, rom[0xff9f] / 4);
        for (int offset = 0; offset < oamCount * 4; offset++) snapshot.Add(rom[0xcb00 + offset]);
        string identity = Convert.ToBase64String(snapshot.ToArray());
        bool reuseExpected = previous == identity && previousExpectedPixels is not null;
        previous = identity;
        using Image actual = _ringMenuScreen.ComposeImage();
        int[] expectedPixels = previousExpectedPixels ??= new int[160 * 88];
        if (!reuseExpected)
        {
            for (int y = startY; y < 88; y++)
            {
                var sprites = new List<int>();
                for (int sprite = 0; sprite < oamCount && sprites.Count < 10; sprite++)
                    if ((uint)(y - (rom[0xcb00 + sprite * 4] - 16)) < 16) sprites.Add(sprite);
                for (int x = 0; x < 160; x++)
                {
                    int mapY = y < 16 ? y / 8 + 16 : y / 8 - 2;
                    int attribute = rom.MapAttribute(x / 8, mapY), tile = rom.MapTile(x / 8, mapY);
                    int pixelX = x & 7, pixelY = y & 7;
                    bool signedTiles = y >= (mode == RingMenuMode.List ? 72 : 88);
                    if (useVram)
                    {
                        // VBlank retains GfxRegs1 for the top strip/window
                        // header. Its LYC dispatch then selects GfxRegs2;
                        // the ring-menu IRQ switches to fixed signed BG $9800.
                        int registers = y <= rom[0xc48a] ? 0xc485 : 0xc48b;
                        int lcdc = signedTiles ? 0x87 : rom[registers];
                        int scx = signedTiles ? 0 : rom[registers + 2];
                        int scy = rom[registers + 1];
                        int wy = rom[registers + 3], wx = rom[registers + 4] - 7;
                        bool window = (lcdc & 0x20) != 0 && y >= wy && x >= wx;
                        int sourceX = window ? x - wx : (x + scx) & 0xff;
                        int sourceY = window ? y - wy : (y + scy) & 0xff;
                        int mapAddress = (window ? (lcdc & 0x40) != 0 : (lcdc & 0x08) != 0)
                            ? 0x9c00 : 0x9800;
                        mapAddress += (sourceY / 8) * 32 + sourceX / 8;
                        tile = rom.MapGfx(0, mapAddress); attribute = rom.MapGfx(1, mapAddress);
                        pixelX = sourceX & 7; pixelY = sourceY & 7;
                        signedTiles = (lcdc & 0x10) == 0;
                    }
                    int localX = (attribute & 0x20) == 0 ? pixelX : 7 - pixelX;
                    int localY = (attribute & 0x40) == 0 ? pixelY : 7 - pixelY;
                    int address = (signedTiles
                        ? 0x9000 + (sbyte)tile * 16 : 0x8000 + tile * 16) + localY * 2;
                    int bank = (attribute >> 3) & 1;
                    int shade = (rom.MapGfx(bank, address) >> (7 - localX) & 1) |
                        (rom.MapGfx(bank, address + 1) >> (7 - localX) & 1) << 1;
                    int expected = rom.HudColor(false, attribute & 7, shade);
                    foreach (int sprite in sprites)
                    {
                        int oam = 0xcb00 + sprite * 4;
                        int spriteX = x - (rom[oam + 1] - 8), spriteY = y - (rom[oam] - 16);
                        if ((uint)spriteX >= 8) continue;
                        int flags = rom[oam + 3];
                        if ((flags & 0x20) != 0) spriteX = 7 - spriteX;
                        if ((flags & 0x40) != 0) spriteY = 15 - spriteY;
                        int spriteAddress = 0x8000 + (rom[oam + 2] & 0xfe) * 16 + spriteY * 2;
                        int spriteBank = (flags >> 3) & 1;
                        int spriteShade = (rom.MapGfx(spriteBank, spriteAddress) >> (7 - spriteX) & 1) |
                            (rom.MapGfx(spriteBank, spriteAddress + 1) >> (7 - spriteX) & 1) << 1;
                        if (spriteShade == 0) continue;
                        if (shade == 0 || ((attribute | flags) & 0x80) == 0)
                            expected = rom.HudColor(true, flags & 7, spriteShade);
                        break;
                    }
                    expectedPixels[y * 160 + x] = expected;
                }
            }
        }
        // Reuse only native expectations. Inspect the actual upload image on
        // every update, including changes with unchanged native map/OAM.
        byte[] pixels = actual.GetData();
        for (int y = startY; y < 88; y++)
        for (int x = 0; x < 160; x++)
        {
            int offset = (y * 160 + x) * 4;
            int observed = (pixels[offset] * 31 + 127) / 255 |
                ((pixels[offset + 1] * 31 + 127) / 255) << 5 |
                ((pixels[offset + 2] * 31 + 127) / 255) << 10;
            if (pixels[offset + 3] != 255 || observed != expectedPixels[y * 160 + x]) FailIf(true,
                context + $": pixel ({x},{y}) runtime=${observed:x4}, ROM=${expectedPixels[y * 160 + x]:x4}.");
        }
    }
}
