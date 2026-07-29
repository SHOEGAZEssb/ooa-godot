using Godot;
using System;
using static oracleofages.OracleGraphicsData;

namespace oracleofages;

/// <summary>Renders GFXH_BLACK_TOWER_STAGE_1_LAYOUT/BASE and OAM $714c.</summary>
internal partial class BlackTowerExplanationScreen : Control
{
    private const string Root = "res://assets/oracle/cutscenes/";
    private readonly BlackTowerEntranceEventDatabase _database;
    private readonly Texture2D _background;
    private readonly Image _sprites;
    private bool _flashWhite;

    internal ulong BackgroundPixelHash { get; }

    public BlackTowerExplanationScreen(BlackTowerEntranceEventDatabase database)
    {
        _database = database;
        Name = "BlackTowerExplanationScreen";
        Size = new Vector2(OracleRoomData.ViewportWidth, OracleRoomData.ScreenHeight);
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 14;
        _background = BuildBackground(database.BackgroundPalettes);
        BackgroundPixelHash = OracleGraphicsCache.PixelHash(
            _background.GetImage());
        _sprites = OracleGraphicsCache.LoadImage(
            Root + "spr_black_tower_scene.png");
    }

    internal void SetFlashWhite(bool white)
    {
        if (_flashWhite == white)
            return;
        _flashWhite = white;
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawTexture(_background, Vector2.Zero);
        for (int index = _database.Oam.Count - 1; index >= 0; index--)
        {
            OamRecord oam = _database.Oam[index];
            // func_6f44 supplies b=-SCY ($90 for SCY=$70). OAM coordinates
            // wrap as bytes before the hardware's x-8/y-16 origin offsets.
            int rawY = (oam.Y + 0x90) & 0xff;
            Vector2 position = new(oam.X - 8, rawY - 16);
            if (position.X <= -8 || position.X >= OracleRoomData.ViewportWidth ||
                position.Y <= -16 || position.Y >= OracleRoomData.ScreenHeight)
                continue;
            DrawTexture(SpriteTexture(oam), position);
        }
        if (_flashWhite)
            DrawRect(new Rect2(Vector2.Zero, Size), Colors.White);
    }

    private Texture2D SpriteTexture(OamRecord oam) =>
        OracleTileRenderer.GetOamCellTexture(
            _sprites, oam.Tile, (byte)oam.Flags, _database.SpritePalettes);

    private static Texture2D BuildBackground(Color[,] palettes)
    {
        byte[] topMap = ReadBytes(Root + "map_black_tower_stage_1.bin", 384);
        byte[] topFlags = ReadBytes(Root + "flags_black_tower_stage_1.bin", 384);
        byte[] baseMap = ReadBytes(Root + "map_black_tower_base.bin", 192);
        byte[] baseFlags = ReadBytes(Root + "flags_black_tower_base.bin", 192);
        byte[] map = new byte[576];
        byte[] flags = new byte[576];
        Array.Copy(topMap, map, topMap.Length);
        Array.Copy(baseMap, 0, map, topMap.Length, baseMap.Length);
        Array.Copy(topFlags, flags, topFlags.Length);
        Array.Copy(baseFlags, 0, flags, topFlags.Length, baseFlags.Length);

        var tiles = new OracleVramTileMap();
        tiles.Map(OracleGraphicsCache.LoadImage(
            Root + "gfx_black_tower_scene_1.png"), 0x8800, 0);
        tiles.Map(OracleGraphicsCache.LoadImage(
            Root + "gfx_black_tower_scene_2.png"), 0x9000, 0);
        tiles.Map(OracleGraphicsCache.LoadImage(
            Root + "gfx_black_tower_scene_3.png"), 0x8800, 1);
        tiles.Map(OracleGraphicsCache.LoadImage(
            Root + "gfx_black_tower_scene_4.png"), 0x9000, 1);
        return OracleTileRenderer.BuildTileMapTexture(
            map, flags, tiles, palettes);
    }
}
