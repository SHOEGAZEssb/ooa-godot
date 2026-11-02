
namespace Oracles;

public static class ItemIconAtlas
{




    internal static ulong DecodedCellHash(Image source, int cell)
    {
        ulong hash = 14695981039346656037UL;
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 8; x++)
        {
            int shade = OracleGraphicsData.ShadeFromPng(source.GetPixel(cell * 8 + x, y), out _);
            hash ^= (byte)shade;
            hash *= 1099511628211UL;
        }
        return hash;
    }

    // bank2.s:loadEquippedItemSpriteData uses different seed-icon boundaries.
    internal static int EquippedLeftPalette(OracleGame game, int sprite, int palette) =>
        sprite < (game switch {
            OracleGame.Ages => 0x84, OracleGame.Seasons => 0x86,
            _ => throw new System.ArgumentOutOfRangeException(nameof(game))
        }) ? ((palette - 3) | 1) & 7 : palette & 7;

    // loadItemIconGfx uses the smaller song pair only for equipped A/B items.
    // Inventory OAM and the song picker continue to address the large pair.
    internal static int EquippedSprite(OracleGame game, int sprite) => game switch
    {
        OracleGame.Ages or OracleGame.Seasons => game == OracleGame.Ages && sprite >= 0xa3 ? sprite + 2
            : sprite is > 0 and < 0x80 ? sprite + 0x80 : sprite,
        _ => throw new System.ArgumentOutOfRangeException(nameof(game))
    };

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

    public static Color[,] LoadStandardSpritePalettes(string assetRoot)
    {
        string path = assetRoot + "inventory/palette_sprites.bin";
        byte[] bytes = OracleAssetCache.ReadBytes(path);
        if (bytes.Length != 6 * 4 * 3)
            throw new System.InvalidOperationException(
                $"{path} should contain 72 bytes, got {bytes.Length}.");

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
