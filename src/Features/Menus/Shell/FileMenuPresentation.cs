using Godot;

namespace Oracles;

/// <summary>Shared file-menu layout assembly and decorative OAM list.</summary>
internal static class FileMenuPresentation
{
    internal static System.Collections.Generic.Dictionary<string, System.Collections.Generic.IReadOnlyList<MenuOamPart>> LoadOam(string assetRoot)
    {
        var layouts = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.IReadOnlyList<MenuOamPart>>(System.StringComparer.Ordinal);
        MenuPresentationDatabase.LoadOamLayouts(assetRoot + "menu/file_oam.tsv", "file menu OAM", layouts,
            new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.Ordinal) {
                ["decorations"] = 16, ["acorn-cursor"] = 1, ["text-speed-cursor"] = 1,
                ["name-character-cursor"] = 2, ["name-lower-option-cursor"] = 2,
                ["name-entry-cursor"] = 1, ["secret-entry-cursor"] = 1, ["save-quit-acorn"] = 1, ["hero-file"] = 2 });
        return layouts;
    }
    public static (byte[] Map, byte[] Flags) BuildLayout(
        string middleMap,
        string middleFlags,
        string bottomMap,
        string bottomFlags,
        string assetRoot,
        int bottomLength = 96)
    {
        byte[] map = new byte[576];
        byte[] flags = new byte[576];
        OracleGraphicsData.Overlay(map, OracleGraphicsData.ReadBytes(
            assetRoot + "menu/map_file_menu_top.bin", 160), 0);
        OracleGraphicsData.Overlay(flags, OracleGraphicsData.ReadBytes(
            assetRoot + "menu/flags_file_menu_top.bin", 160), 0);
        OracleGraphicsData.Overlay(map, OracleGraphicsData.ReadBytes(
            assetRoot + $"menu/{middleMap}", 320), 0xa0);
        OracleGraphicsData.Overlay(flags, OracleGraphicsData.ReadBytes(
            assetRoot + $"menu/{middleFlags}", 320), 0xa0);
        // The save-menu data includes a fourth, off-screen row; only the
        // first three rows beginning at tilemap offset $1e0 are visible.
        OracleGraphicsData.Overlay(map, OracleGraphicsData.ReadBytes(
            assetRoot + $"menu/{bottomMap}", bottomLength), 0x1e0, 96);
        OracleGraphicsData.Overlay(flags, OracleGraphicsData.ReadBytes(
            assetRoot + $"menu/{bottomFlags}", bottomLength), 0x1e0, 96);
        return (map, flags);
    }

    public static void DrawDecorations(
        Node2D canvas,
        Image source,
        Color[,] palette,
        System.Collections.Generic.IReadOnlyList<MenuOamPart> parts)
    {
        foreach (MenuOamPart part in parts)
        {
            OracleTileRenderer.DrawOamTile(
                canvas,
                source,
                0x20,
                part.Tile,
                part.Attributes & 7,
                OamScreenPosition(part),
                (part.Attributes & 0x20) != 0,
                (part.Attributes & 0x40) != 0,
                palette,
                inverted: false);
        }
    }

    public static Vector2 OamScreenPosition(
        MenuOamPart part,
        int yOffset = 0,
        int xOffset = 0) =>
        new(
            ((part.X + xOffset) & 0xff) - 8,
            ((part.Y + yOffset) & 0xff) - 16);
}
