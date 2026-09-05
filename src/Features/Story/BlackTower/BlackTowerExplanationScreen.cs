using Godot;
using System;
using System.Collections.Generic;
using static oracleofages.OracleGraphicsData;

namespace oracleofages;

/// <summary>Renders the imported Black Tower explanation stage and OAM.</summary>
internal partial class BlackTowerExplanationScreen : Control
{
    private const string Root = "res://assets/oracle/cutscenes/";
    private readonly BlackTowerEntranceEventDatabase _database;
    private readonly IReadOnlyList<OamRecord> _oam;
    private readonly Texture2D _background;
    private readonly Image _sprites;
    private bool _flashWhite;

    internal Texture2D Background => _background;

    public BlackTowerExplanationScreen(
        BlackTowerEntranceEventDatabase database,
        int stage = 0)
    {
        _database = database;
        Name = "BlackTowerExplanationScreen";
        Size = new Vector2(OracleRoomData.ViewportWidth, OracleRoomData.ScreenHeight);
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 14;
        _oam = database.OamForStage(stage);
        _background = BuildBackground(database.BackgroundPalettes, stage);
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
        for (int index = _oam.Count - 1; index >= 0; index--)
        {
            OamRecord oam = _oam[index];
            // func_6f44 supplies b=-SCY ($90 for SCY=$70). OAM coordinates
            // wrap as bytes before the hardware's x-8/y-16 origin offsets.
            int rawY = (oam.Y - _database.Record.ScreenOffsetY) & 0xff;
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

    private static Texture2D BuildBackground(Color[,] palettes, int stage)
    {
        byte[] topMap;
        byte[] topFlags;
        if (stage == 0)
        {
            topMap = ReadBytes(Root + "map_black_tower_stage_1.bin", 384);
            topFlags = ReadBytes(Root + "flags_black_tower_stage_1.bin", 384);
        }
        else if (stage == 1)
        {
            // GFXH_BLACK_TOWER_STAGE_2_LAYOUT begins at $9840. With
            // SCY=$70, only its final 32-byte row is visible, followed by
            // GFXH_BLACK_TOWER_MIDDLE and the shared base.
            byte[] stageMap = ReadBytes(
                Root + "map_black_tower_stage_2.bin", 416);
            byte[] stageFlags = ReadBytes(
                Root + "flags_black_tower_stage_2.bin", 416);
            byte[] middleMap = ReadBytes(
                Root + "map_black_tower_middle.bin", 352);
            byte[] middleFlags = ReadBytes(
                Root + "flags_black_tower_middle.bin", 352);
            topMap = new byte[384];
            topFlags = new byte[384];
            Array.Copy(stageMap, 384, topMap, 0, 32);
            Array.Copy(middleMap, 0, topMap, 32, middleMap.Length);
            Array.Copy(stageFlags, 384, topFlags, 0, 32);
            Array.Copy(middleFlags, 0, topFlags, 32, middleFlags.Length);
        }
        else
        {
            throw new InvalidOperationException(
                $"Black Tower explanation stage {stage} is not imported.");
        }
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
