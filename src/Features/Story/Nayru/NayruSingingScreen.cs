using Godot;
using System;
using static oracleofages.OracleGraphicsData;

namespace oracleofages;

/// <summary>Renders GFXH_NAYRU_SINGING_CUTSCENE and bank3f.oamData_7249.</summary>
public partial class NayruSingingScreen : Control
{
    private readonly Texture2D _background;
    private readonly Image _sprites;
    private readonly NayruIntroEventDatabase _database;
    private int _scrollX;

    public int ScrollX => _scrollX;
    internal Texture2D Background => _background;

    public NayruSingingScreen(NayruIntroEventDatabase database)
    {
        _database = database;
        Name = "NayruSingingScreen";
        Size = new Vector2(OracleRoomData.ViewportWidth, OracleRoomData.ViewportHeight);
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 14;
        _background = BuildBackground(database.SingingBackgroundPalettes);
        _sprites = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/cutscenes/spr_nayru_singing_cutscene.png");
    }

    public void SetScrollX(int scrollX)
    {
        _scrollX = Math.Clamp(scrollX, 0, 40);
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawTexture(_background, new Vector2(-_scrollX, 0));
        // Lower OAM indices cover later entries, so draw in reverse order.
        for (int index = _database.SingingOam.Count - 1; index >= 0; index--)
        {
            SingingOamRecord oam = _database.SingingOam[index];
            Vector2 position = new(oam.X - 8 - _scrollX, oam.Y - 16);
            if (position.X <= -8 || position.X >= OracleRoomData.ViewportWidth ||
                position.Y <= -16 || position.Y >= OracleRoomData.ViewportHeight)
                continue;
            DrawTexture(SpriteTexture(oam), position);
        }
    }

    private Texture2D SpriteTexture(SingingOamRecord oam) =>
        OracleTileRenderer.GetOamCellTexture(
            _sprites, oam.Tile, (byte)oam.Flags,
            _database.SingingSpritePalettes);

    private static Texture2D BuildBackground(Color[,] palettes)
    {
        byte[] map = ReadBytes(
            "res://assets/oracle/cutscenes/map_nayru_singing_cutscene.bin", 576);
        byte[] flags = ReadBytes(
            "res://assets/oracle/cutscenes/flags_nayru_singing_cutscene.bin", 576);
        var tiles = new OracleVramTileMap();
        tiles.Map(OracleGraphicsCache.LoadImage(
            "res://assets/oracle/cutscenes/gfx_nayru_singing_cutscene_1.png"), 0x8800, 0);
        tiles.Map(OracleGraphicsCache.LoadImage(
            "res://assets/oracle/cutscenes/gfx_nayru_singing_cutscene_2.png"), 0x9000, 0);
        tiles.Map(OracleGraphicsCache.LoadImage(
            "res://assets/oracle/cutscenes/gfx_nayru_singing_cutscene_3.png"), 0x8800, 1);
        return OracleTileRenderer.BuildTileMapTexture(
            map, flags, tiles, palettes);
    }

}
