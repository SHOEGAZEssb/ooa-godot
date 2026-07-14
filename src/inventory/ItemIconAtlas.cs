using Godot;

namespace oracleofages;

public static class ItemIconAtlas
{
    private const string StandardSpritePalettePath = "res://assets/oracle/inventory/palette_sprites.bin";

    public static int ShadeFromPng(Color sourceColor, out bool transparent)
    {
        int shade = sourceColor.R < 0.1f ? 0
            : sourceColor.R < 0.5f ? 1
            : sourceColor.R < 0.9f ? 2
            : 3;
        transparent = shade == 0;
        return shade;
    }

    public static bool Select(
        int sprite,
        Image itemIcons1,
        Image itemIcons2,
        Image itemIcons3,
        out Image source,
        out int cell)
    {
        // GFXH_INVENTORY ($08) loads these three 0x200-byte sheets
        // contiguously at bank-1 VRAM $8000/$8200/$8400. Display indices
        // $80-$af therefore select 48 consecutive 8x16 cells.
        return SelectRawCell(sprite is >= 0x80 and <= 0xaf ? sprite - 0x80 : -1,
            itemIcons1, itemIcons2, itemIcons3, out source, out cell);
    }

    private static bool SelectRawCell(
        int rawCell,
        Image itemIcons1,
        Image itemIcons2,
        Image itemIcons3,
        out Image source,
        out int cell)
    {
        if (rawCell is >= 0 and < 16)
        {
            source = itemIcons1;
            cell = rawCell;
            return true;
        }
        if (rawCell is >= 16 and < 32)
        {
            source = itemIcons2;
            cell = rawCell - 16;
            return true;
        }
        if (rawCell is >= 32 and < 48)
        {
            source = itemIcons3;
            cell = rawCell - 32;
            return true;
        }
        source = itemIcons1;
        cell = 0;
        return false;
    }

    public static Color[,] LoadStandardSpritePalettes()
    {
        byte[] bytes = FileAccess.GetFileAsBytes(StandardSpritePalettePath);
        if (bytes.Length != 6 * 4 * 3)
            throw new System.InvalidOperationException(
                $"{StandardSpritePalettePath} should contain 72 bytes, got {bytes.Length}.");

        var palettes = new Color[6, 4];
        for (int palette = 0; palette < 6; palette++)
        for (int shade = 0; shade < 4; shade++)
        {
            int offset = (palette * 4 + shade) * 3;
            palettes[palette, shade] = new Color(
                bytes[offset] / 31.0f,
                bytes[offset + 1] / 31.0f,
                bytes[offset + 2] / 31.0f);
        }
        return palettes;
    }
}
