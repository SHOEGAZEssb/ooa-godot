using Godot;
using System;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareInventoryFramePixelsRom(MenuRom rom, string context)
    {
        using Image frame = _inventoryScreen.ComposeImage();
        CompareMenuFramePixelsRom(rom, frame, context);
    }

    private void CompareMenuFramePixelsRom(MenuRom rom, Image frame, string context,
        RingMenuMode? ringMode = null, float? additiveFade = null)
    {
        // bank0.s gfx register state $03: VBlank publishes GfxRegs1; the
        // status-bar IRQ selects GfxRegs2; line $75 changes LCDC to $a7.
        // Consume executed native VRAM/OAM, including LCD-off DMA publication.
        // This describes the source scanline mapping, not instruction/PPU timing.
        byte[] pixels = frame.GetData();
        int oamCount = Math.Min(40, rom[0xff9f] / 4);
        Span<int> colors = stackalloc int[64];
        for (int sprite = 0; sprite < 2; sprite++)
        for (int palette = 0; palette < 8; palette++)
        for (int shade = 0; shade < 4; shade++)
            colors[sprite * 32 + palette * 4 + shade] = additiveFade.HasValue ?
                rom.FadingColor(sprite != 0, palette, shade) : rom.HudColor(sprite != 0, palette, shade);
        Span<byte> commands = stackalloc byte[160];
        for (int offset = 0; offset < oamCount * 4; offset++) commands[offset] = rom[0xcb00 + offset];
        Span<int> spriteOam = stackalloc int[160];
        Span<byte> spriteShades = stackalloc byte[160];
        Span<byte> spriteFlags = stackalloc byte[160];
        for (int y = 0; y < 144; y++)
        {
            // Decode each selected OBJ row once. Transparent and offscreen
            // entries still consume one of the first ten scanline slots; an
            // opaque winner blocks later entries even when BG priority hides it.
            spriteOam.Fill(-1);
            int selected = 0;
            for (int sprite = 0; sprite < oamCount; sprite++)
            {
                int offset = sprite * 4, localY = y - (commands[offset] - 16);
                if ((uint)localY >= 16) continue;
                if (++selected > 10) break;
                byte attributes = commands[offset + 3];
                int sourceY = (attributes & 0x40) == 0 ? localY : 15 - localY;
                int address = 0x8000 + (commands[offset + 2] & 0xfe) * 16 + sourceY * 2;
                int bank = attributes >> 3 & 1;
                int low = rom.MapGfx(bank, address), high = rom.MapGfx(bank, address + 1);
                for (int localX = 0; localX < 8; localX++)
                {
                    int x = commands[offset + 1] - 8 + localX;
                    if ((uint)x >= 160 || spriteOam[x] >= 0) continue;
                    int sourceX = (attributes & 0x20) == 0 ? localX : 7 - localX;
                    int shade = (low >> (7 - sourceX) & 1) | (high >> (7 - sourceX) & 1) << 1;
                    if (shade == 0) continue;
                    spriteOam[x] = 0xcb00 + offset;
                    spriteShades[x] = (byte)shade;
                    spriteFlags[x] = attributes;
                }
            }
            int registers = y <= rom[0xc48a] ? 0xc485 : 0xc48b;
            int lcdc = y <= rom[0xc490] ? rom[registers] : 0xa7;
            int scy = rom[registers + 1], scx = rom[registers + 2];
            int wy = rom[registers + 3], wx = rom[registers + 4] - 7;
            if (ringMode.HasValue && y >= (ringMode == RingMenuMode.List ? 72 : 88))
            {
                // lcdInterrupt_ringMenu selects fixed signed BG; the List's
                // second IRQ restores source row 1 for its bottom border.
                lcdc = 0x87; scx = 0;
                if (ringMode == RingMenuMode.List && y >= 136) scy = 0x80;
            }
            int previousMap = -1, previousRow = -1, tile = 0, flags = 0, lowBg = 0, highBg = 0;
            for (int x = 0; x < 160; x++)
            {
                bool window = (lcdc & 0x20) != 0 && y >= wy && x >= wx;
                int sourceX = window ? x - wx : (x + scx) & 0xff;
                int sourceY = window ? y - wy : (y + scy) & 0xff;
                int map = (window ? (lcdc & 0x40) != 0 : (lcdc & 0x08) != 0) ? 0x9c00 : 0x9800;
                map += sourceY / 8 * 32 + sourceX / 8;
                // No native execution occurs during a comparison. BG bytes and
                // palettes can be shared across the pixels of this tile row.
                if (map != previousMap || (sourceY & 7) != previousRow)
                {
                    previousMap = map; previousRow = sourceY & 7;
                    tile = rom.MapGfx(0, map); flags = rom.MapGfx(1, map);
                    int localY = (flags & 0x40) == 0 ? sourceY & 7 : 7 - (sourceY & 7);
                    int source = ((lcdc & 0x10) == 0 ? 0x9000 + (sbyte)tile * 16 : 0x8000 + tile * 16) + localY * 2;
                    int bank = flags >> 3 & 1;
                    lowBg = rom.MapGfx(bank, source); highBg = rom.MapGfx(bank, source + 1);
                }
                int localX = (flags & 0x20) == 0 ? sourceX & 7 : 7 - (sourceX & 7);
                int shade = (lowBg >> (7 - localX) & 1) | (highBg >> (7 - localX) & 1) << 1;
                int expected = colors[(flags & 7) * 4 + shade];
                int winningSprite = spriteOam[x], winningShade = winningSprite < 0 ? 0 : spriteShades[x];
                if (winningSprite >= 0 && (shade == 0 || ((flags | spriteFlags[x]) & 0x80) == 0))
                    expected = colors[32 + (spriteFlags[x] & 7) * 4 + winningShade];
                int offset = (y * 160 + x) * 4;
                int observed = (pixels[offset] * 31 + 127) / 255 |
                    ((pixels[offset + 1] * 31 + 127) / 255) << 5 |
                    ((pixels[offset + 2] * 31 + 127) / 255) << 10;
                if (additiveFade.HasValue)
                {
                    // Apply the configured scene material to the actual composed
                    // image. This verifies CPU composition and additive colors;
                    // it does not execute GPU output or PPU publication timing.
                    observed = 0;
                    for (int channel = 0; channel < 3; channel++)
                        observed |= Mathf.RoundToInt(Mathf.Min(1,
                            pixels[offset + channel] / 255f + additiveFade.Value) * 31) << (channel * 5);
                }
                if (pixels[offset + 3] != 255 || observed != expected) FailIf(true,
                    context + $": pixel ({x},{y}) runtime=${observed:x4}, native=${expected:x4}, tile=${tile:x2}/flags=${flags:x2}, BG shade={shade} base=${rom.HudColor(false, flags & 7, shade):x4}, fade={additiveFade}, menu=${rom[0xcbcd]:x2}/${rom[0xcbce]:x2}, OBJ={(winningSprite < 0 ? "none" : $"${rom[winningSprite + 2]:x2}/${rom[winningSprite + 3]:x2} shade={winningShade}")}.");
            }
        }
    }

    private void CompareRingFramePixelsRom(MenuRom rom, RingMenuMode mode, string context,
        float? additiveFade = null)
    {
        using Image frame = _ringMenuScreen.ComposeImage();
        if (mode == RingMenuMode.Appraisal)
        {
            using Image hud = _hud.ComposeImage();
            frame.BlitRect(hud, new Rect2I(0, 0, 160, 16), Vector2I.Zero);
        }
        using Image panel = _dialogue.ComposeImage();
        frame.BlendRect(panel, new Rect2I(0, 0, panel.GetWidth(), panel.GetHeight()),
            new Vector2I((int)_dialogue.Position.X, (int)_dialogue.Position.Y));
        CompareMenuFramePixelsRom(rom, frame, context, mode, additiveFade);
    }
}
