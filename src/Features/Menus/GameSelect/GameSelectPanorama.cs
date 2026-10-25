using Godot;
using System.Collections.Generic;

namespace oracleofages;

// The original close-ups are background maps plus a fixed OAM layer. Only
// their camera scrolls; the selector loops that pan without running gameplay.
internal sealed class GameSelectPanorama
{
    internal Texture2D Background { get; }
    internal IReadOnlyList<PanoramaSprite> Sprites { get; }

    internal GameSelectPanorama(OracleGame game)
    {
        bool ages = game == OracleGame.Ages;
        string folder = ages ? "res://assets/oracle/cutscenes/" : "res://assets/oracle/game_select/";
        string name = ages ? "nayru_singing_cutscene" : "din_dance_cutscene";
        string palette = ages ? "nayru_singing" : "din";
        var backgrounds = OracleGraphicsData.LoadPalette(folder + palette + "_bg_palette.bin");
        var sprites = OracleGraphicsData.LoadPalette(folder + palette + "_sprite_palette.bin", 7, transparentZero: true);
        var tiles = new OracleVramTileMap();
        tiles.Map(OracleGraphicsCache.LoadImage(folder + "gfx_" + name + "_1.png"), 0x8800, 0);
        tiles.Map(OracleGraphicsCache.LoadImage(folder + "gfx_" + name + "_2.png"), 0x9000, 0);
        tiles.Map(OracleGraphicsCache.LoadImage(folder + "gfx_" + name + "_3.png"), 0x8800, 1);
        Background = OracleTileRenderer.BuildTileMapTexture(
            OracleGraphicsData.ReadBytes(folder + "map_" + name + ".bin", 576),
            OracleGraphicsData.ReadBytes(folder + (ages ? "flags_" : "flg_") + name + ".bin", 576),
            tiles, backgrounds);
        Image sheet = OracleGraphicsCache.LoadImage(folder + "spr_" + name + ".png");
        var oam = GeneratedTable.Load(folder + (ages ? "nayru_singing_oam.tsv" : "din_oam.tsv"),
            new GeneratedTableSchema("Game-select close-up OAM", GeneratedTableKeySemantics.Ordered,
                ["y", "x", "tile", "flags"], headerRequired: true));
        var result = new List<PanoramaSprite>();
        foreach (var row in oam.Rows)
            result.Add(new(new Vector2(row.HexByte(1) - 8, row.HexByte(0) - 16),
                OracleTileRenderer.GetOamCellTexture(sheet, row.HexByte(2), (byte)row.HexByte(3), sprites)));
        Sprites = result;
    }
}

internal readonly record struct PanoramaSprite(Vector2 Position, Texture2D Texture);
