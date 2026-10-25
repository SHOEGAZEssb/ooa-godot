using Godot;

namespace Oracles;

internal static class GameSelectTitle
{
    internal static Texture2D Build(OracleGame game)
    {
        string folder = game == OracleGame.Ages
            ? AgesGameContent.AssetRoot + "menu/"
            : SeasonsPreviewContent.PreviewRoot;
        Color[,] palettes = OracleGraphicsData.LoadPalette(folder + "palette_title_bg.bin");
        // Isolate the name artwork from PALH_03's backdrop and animated flames.
        // Subtitle letter shadows use shade $00 in BG palettes $04/$06.
        for (int palette = 0; palette < 8; palette++)
            if (palette is not (4 or 6)) palettes[palette, 0] = Colors.Transparent;
        for (int shade = 0; shade < 4; shade++) palettes[1, shade] = Colors.Transparent;
        palettes[5, 1] = Colors.Transparent;
        palettes[7, 3] = Colors.Transparent;
        var tiles = new OracleVramTileMap();
        tiles.Map(OracleGraphicsCache.LoadImage(folder + "gfx_titlescreen_1.png"), 0x8800, 0);
        tiles.Map(OracleGraphicsCache.LoadImage(folder + "gfx_titlescreen_2.png"), 0x8d00, 0);
        tiles.Map(OracleGraphicsCache.LoadImage(folder + "gfx_titlescreen_3.png"), 0x9300, 0);
        tiles.Map(OracleGraphicsCache.LoadImage(folder + "gfx_titlescreen_4.png"), 0x9400, 0);
        Texture2D title = OracleTileRenderer.BuildTileMapTexture(
            OracleGraphicsData.ReadBytes(folder + "map_titlescreen.bin", 576),
            OracleGraphicsData.ReadBytes(folder + (game == OracleGame.Ages ? "flags_" : "flg_") + "titlescreen.bin", 576),
            tiles, palettes);
        using Image original = title.GetImage();
        using Image logo = original.GetRegion(new Rect2I(12, 20, 144, 90));
        return ImageTexture.CreateFromImage(logo);
    }
}
