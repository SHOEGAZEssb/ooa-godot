using Godot;
using System.Collections.Generic;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateHudItemPixelsRom()
    {
        var cases = new List<(int Item, int Parameter, int Address, int Value)>();
        foreach (int level in new[] { 1, 2, 3 })
        {
            cases.Add((0x05, level, 0xc6b2, level));
            cases.Add((0x01, level, 0xc6af, level));
        }
        foreach (int level in new[] { 1, 2 })
        {
            cases.Add((0x16, level, 0xc6b8, level));
            cases.Add((0x0a, level, 0xc6b6, level));
        }
        foreach (int item in new[] { 0x00, 0x04, 0x06, 0x15, 0x17, 0x0c })
            cases.Add((item, 1, 0, 0));
        foreach (int count in new[] { 0x00, 0x01, 0x09, 0x10, 0x30 })
        {
            cases.Add((0x03, 0, 0xc6b0, count));
            cases.Add((0x0d, 0, 0xc6b3, count));
        }
        foreach (int item in new[] { 0x19, 0x0f })
        foreach (int seed in new[] { 0, 1, 2, 3, 4 })
        foreach (int count in new[] { 0x00, 0x09, 0x10, 0x99 })
            cases.Add((item, seed, 0xc6b9 + seed, count));
        foreach (int variant in new[] { 0, 1, 2, 3 })
        {
            cases.Add((0x11, variant, 0xc6b7, variant));
            cases.Add((0x0e, variant, 0xc6b5, variant));
        }
        int comparisons = 0;
        foreach (int maximum in new[] { 12, 56, 57, 64 })
        foreach (bool isA in new[] { false, true })
        foreach (var fixture in cases)
        {
            var save = OracleSaveData.CreateStandardGame();
            save.WriteWramByte(0xc6aa, (byte)maximum); save.WriteWramByte(0xc6ab, (byte)maximum);
            var inventory = new InventoryState(_treasures, save);
            if (fixture.Item != 0) inventory.GiveTreasure(fixture.Item,
                fixture.Item is 0x11 or 0x0e or 0x19 or 0x0f ? 0 : fixture.Parameter);
            if (fixture.Item is 0x19 or 0x0f)
                inventory.GiveTreasure(0x20 + fixture.Parameter, 0);
            if (fixture.Item == 0x11 && fixture.Parameter != 0)
                inventory.GiveTreasure(0x24 + fixture.Parameter, 0);
            save.WriteWramByte(0xc6b1, 0x30); // Valid bomb capacity for every count fixture.
            // Native display inputs: levels, selected seed/song/icon and BCD
            // quantities. These are fixture initial conditions, not grants.
            if (fixture.Address != 0) save.WriteWramByte(fixture.Address, (byte)fixture.Value);
            if (fixture.Item == 0x19) save.WriteWramByte(0xc6c4, (byte)fixture.Parameter);
            if (fixture.Item == 0x0f) save.WriteWramByte(0xc6c5, (byte)fixture.Parameter);
            int equippedB = fixture.Item == 0x0c || !isA ? fixture.Item : 0;
            int equippedA = fixture.Item == 0x0c || isA ? fixture.Item : 0;
            save.WriteWramByte(0xc688, (byte)equippedB); save.WriteWramByte(0xc689, (byte)equippedA);
            inventory = new InventoryState(_treasures, save);
            var rom = new MenuRom(save, _currentRoom); rom.LoadHudGraphics(); rom.UpdateHud(0);
            var hud = new Hud(); AddChild(hud);
            try
            {
                hud.Initialize(_treasures, inventory);
                hud.MaxHealthQuarters = maximum; hud.HealthQuarters = maximum;
                hud.EquippedB = equippedB; hud.EquippedA = equippedA;
                hud.Refresh();
                CompareHudPixelsRom(hud, rom,
                    $"HUD pixels item=${fixture.Item:x2} parameter=${fixture.Parameter:x2} value=${fixture.Value:x2} maximum=${maximum:x2} slot={(isA ? 'A' : 'B')}");
                comparisons++;
            }
            finally { hud.Free(); }
        }
        FailIf(comparisons != 592, $"HUD pixel fixture count changed: {comparisons}.");
        GD.Print($"Validated {comparisons} clean-US composed HUD strips from executed graphics/palette/map/item loaders: A/B, all supported icon variants, BCD quantities, Harp labels, two-handed sword and $38/$39 heart compression. LCD/DMA and viewport rendering remain outside this fixture.");
    }

    private void CloseInventoryHudPixelsRom(MenuRom rom, bool batched, string context)
    {
        void Step(int count, int pressed = 0)
        {
            int edge = pressed;
            StepGameplayUpdates(count, Vector2.Zero, MenuRomActions(pressed), MenuRomActions(pressed), batched, () =>
            {
                rom.AdvancePalette();
                int frame = _saveData.ReadWramByte(0xc622);
                rom.Update(edge, pressed, frame); edge = 0;
                if (rom[0xcbcb] == 0) rom.UpdateHud(frame);
                FailIf(_gameplayPause.IsLeased != (rom[0xcbcb] != 0), context + ": closing HUD pause boundary differs.");
                CompareHudPixelsRom(_hud, rom, context + " closing");
            });
        }
        Step(1, 8); Step(22);
        FailIf(_menuLifecycle.IsActive, context + ": closing HUD did not resume gameplay.");
    }

    private void CompareHudPixelsRom(Hud hud, MenuRom rom, string context, bool displayImage = false)
    {
        // Independent fixed-strip composition from native memory. Original
        // routines loaded/decompressed every tile, attribute, palette and OAM
        // command. This is a static CGB layer comparison, not a PPU timing model.
        byte[] backgroundBefore = hud.Background.GetImage().GetData();
        // Inspect the actual upload image. The headless texture backend keeps
        // its initial GetImage snapshot after ImageTexture.Update.
        using Image actual = displayImage ? hud.ComposeDisplayImage() : hud.ComposeImage();
        FailIf(actual.GetWidth() != 160 || actual.GetHeight() != 16 || actual.GetFormat() != Image.Format.Rgba8,
            context + ": HUD comparison requires a 160 by 16 RGBA image.");
        byte[] pixels = actual.GetData();
        FailIf(!System.Linq.Enumerable.SequenceEqual(backgroundBefore, hud.Background.GetImage().GetData()),
            context + ": composing item OAM must preserve the reusable status background.");
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 160; x++)
        {
            int offset = (y / 8) * 32 + x / 8;
            int attribute = rom.HudAttribute(offset), bgTile = rom.Tile(offset);
            int bgX = (attribute & 0x20) == 0 ? x & 7 : 7 - (x & 7);
            int bgY = (attribute & 0x40) == 0 ? y & 7 : 7 - (y & 7);
            int address = bgTile * 16 + bgY * 2;
            int bgShade = (rom.HudBgGfx(address) >> (7 - bgX) & 1) |
                (rom.HudBgGfx(address + 1) >> (7 - bgX) & 1) << 1;
            int expected = rom.HudColor(false, attribute & 7, bgShade);
            for (int sprite = 0; sprite < 4; sprite++)
            {
                int oam = 0xcb00 + sprite * 4;
                int localY = y - (rom[oam] - 16), localX = x - (rom[oam + 1] - 8);
                if ((uint)localY >= 16 || (uint)localX >= 8) continue;
                int flags = rom[oam + 3];
                if ((flags & 0x20) != 0) localX = 7 - localX;
                if ((flags & 0x40) != 0) localY = 15 - localY;
                int gfx = ((rom[oam + 2] & 0xfe) - 0x78) * 16 + localY * 2;
                int shade = (rom.HudItemGfx(gfx) >> (7 - localX) & 1) |
                    (rom.HudItemGfx(gfx + 1) >> (7 - localX) & 1) << 1;
                if (shade == 0) continue;
                if (bgShade == 0 || ((attribute | flags) & 0x80) == 0)
                    expected = rom.HudColor(true, flags & 7, shade);
                break;
            }
            int pixel = (y * 160 + x) * 4;
            int observed = (pixels[pixel] * 31 + 127) / 255 |
                ((pixels[pixel + 1] * 31 + 127) / 255) << 5 |
                ((pixels[pixel + 2] * 31 + 127) / 255) << 10;
            if (observed != expected) FailIf(true, context + $": pixel ({x},{y}) runtime=${observed:x4}, native=${expected:x4}, BG tile=${bgTile:x2}/attr=${attribute:x2}/shade={bgShade}, B=${rom[0xc688]:x2}/treasure=${rom[0xcbea]:x2}/mode=${rom[0xcbee]:x2}/seed=${rom[0xc6c4]:x2}, A=${rom[0xc689]:x2}/treasure=${rom[0xcbef]:x2}/mode=${rom[0xcbf3]:x2}.");
        }
    }
}
