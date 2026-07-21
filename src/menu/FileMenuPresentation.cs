using Godot;

namespace oracleofages;

/// <summary>Shared file-menu layout assembly and decorative OAM list.</summary>
internal static class FileMenuPresentation
{
    private static readonly int[,] DecorationSprites =
    {
        {0x23,0x0a,0x20,5},{0x23,0x12,0x22,5},{0x33,0x06,0x20,5},
        {0x33,0x0e,0x22,5},{0x0f,0x07,0x26,5},{0x3b,0x16,0x20,0x25},
        {0x3b,0x0e,0x22,0x25},{0x17,0x0a,0x24,0x25},{0x21,0x96,0x20,5},
        {0x21,0x9e,0x22,5},{0x17,0x9b,0x26,0x65},{0x14,0x9d,0x24,5},
        {0x31,0xa2,0x20,0x25},{0x31,0x9a,0x22,0x25},{0x39,0x92,0x20,5},
        {0x39,0x9a,0x22,5}
    };

    public static (byte[] Map, byte[] Flags) BuildLayout(
        string middleMap,
        string middleFlags,
        string bottomMap,
        string bottomFlags,
        int bottomLength = 96)
    {
        byte[] map = new byte[576];
        byte[] flags = new byte[576];
        OracleGraphicsData.Overlay(map, OracleGraphicsData.ReadBytes(
            "res://assets/oracle/menu/map_file_menu_top.bin", 160), 0);
        OracleGraphicsData.Overlay(flags, OracleGraphicsData.ReadBytes(
            "res://assets/oracle/menu/flags_file_menu_top.bin", 160), 0);
        OracleGraphicsData.Overlay(map, OracleGraphicsData.ReadBytes(
            $"res://assets/oracle/menu/{middleMap}", 320), 0xa0);
        OracleGraphicsData.Overlay(flags, OracleGraphicsData.ReadBytes(
            $"res://assets/oracle/menu/{middleFlags}", 320), 0xa0);
        // The save-menu data includes a fourth, off-screen row; only the
        // first three rows beginning at tilemap offset $1e0 are visible.
        OracleGraphicsData.Overlay(map, OracleGraphicsData.ReadBytes(
            $"res://assets/oracle/menu/{bottomMap}", bottomLength), 0x1e0, 96);
        OracleGraphicsData.Overlay(flags, OracleGraphicsData.ReadBytes(
            $"res://assets/oracle/menu/{bottomFlags}", bottomLength), 0x1e0, 96);
        return (map, flags);
    }

    public static void DrawDecorations(
        Node2D canvas,
        Image source,
        Color[,] palette)
    {
        for (int index = 0; index < DecorationSprites.GetLength(0); index++)
        {
            int attributes = DecorationSprites[index, 3];
            OracleTileRenderer.DrawOamTile(
                canvas,
                source,
                0x20,
                DecorationSprites[index, 2],
                attributes & 7,
                new Vector2(
                    DecorationSprites[index, 1] - 8,
                    DecorationSprites[index, 0] - 16),
                (attributes & 0x20) != 0,
                (attributes & 0x40) != 0,
                palette,
                inverted: false);
        }
    }
}
