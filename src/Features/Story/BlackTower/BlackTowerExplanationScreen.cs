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
    private readonly int _stage;
    private readonly Texture2D? _completedTop;
    private int _scrollY;
    private bool _topLoaded;
    private bool _flashWhite;

    internal Texture2D Background => _background;

    public BlackTowerExplanationScreen(
        BlackTowerEntranceEventDatabase database,
        int stage = 0)
    {
        _database = database;
        _stage = stage;
        _scrollY = database.Record.ScreenOffsetY;
        Name = "BlackTowerExplanationScreen";
        Size = new Vector2(OracleRoomData.ViewportWidth, OracleRoomData.ScreenHeight);
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = ScreenDrawPriority.StoryScreenZIndex;
        _oam = database.OamForStage(stage);
        _background = BuildBackground(database.BackgroundPalettes, stage);
        if (stage == 2)
            _completedTop = BuildCompletedTop(database.BackgroundPalettes);
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

    internal void SetCompletedTowerScroll(int scrollY, bool topLoaded)
    {
        if (_stage != 2 || scrollY is < 0 or > 0xff)
            throw new InvalidOperationException($"Black Tower stage {_stage}: invalid completed-tower SCY ${scrollY:x2}.");
        _scrollY = scrollY;
        _topLoaded = topLoaded;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_stage == 2)
        {
            // The original 32x32 BG map wraps at y=$100. At SCY=0,
            // UNCMP_GFXH_34 replaces its final eight rows with the tower top.
            DrawTexture(_background, new Vector2(0, -_scrollY));
            DrawTexture(_background, new Vector2(0, 256 - _scrollY));
            if (_topLoaded)
                DrawTexture(_completedTop!, new Vector2(0, 192 - _scrollY));
            if (_scrollY < 0x71)
                DrawOam(_database.Stage1Oam, start: 0, count: 16);
            else
                DrawOam(_database.Stage2BaseOam, start: 0, count: 10);
        }
        else
            DrawTexture(_background, Vector2.Zero);
        DrawOam(_oam, 0, _oam.Count);
        if (_flashWhite)
            DrawRect(new Rect2(Vector2.Zero, Size), Colors.White);
    }

    private void DrawOam(IReadOnlyList<OamRecord> records, int start, int count)
    {
        for (int index = start + count - 1; index >= start; index--)
        {
            OamRecord oam = records[index];
            // func_6f44 supplies b=-SCY ($90 for SCY=$70). OAM coordinates
            // wrap as bytes before the hardware's x-8/y-16 origin offsets.
            int rawY = (oam.Y - _scrollY) & 0xff;
            Vector2 position = new(oam.X - 8, rawY - 16);
            if (position.X <= -8 || position.X >= OracleRoomData.ViewportWidth ||
                position.Y <= -16 || position.Y >= OracleRoomData.ScreenHeight)
                continue;
            DrawTexture(SpriteTexture(oam), position);
        }
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
        else if (stage == 2)
        {
            // GFXH_STAGE_3_MIDDLE fills $9800..$99df; the shared middle
            // follows at $99e0 and the base at $9b40. Remaining rows wrap.
            byte[] completedMap = new byte[1024];
            byte[] completedFlags = new byte[1024];
            Array.Copy(ReadBytes(Root + "map_black_tower_stage_3_middle.bin", 480), completedMap, 480);
            Array.Copy(ReadBytes(Root + "flags_black_tower_stage_3_middle.bin", 480), completedFlags, 480);
            Array.Copy(ReadBytes(Root + "map_black_tower_middle.bin", 352), 0, completedMap, 480, 352);
            Array.Copy(ReadBytes(Root + "flags_black_tower_middle.bin", 352), 0, completedFlags, 480, 352);
            Array.Copy(ReadBytes(Root + "map_black_tower_base.bin", 192), 0, completedMap, 832, 192);
            Array.Copy(ReadBytes(Root + "flags_black_tower_base.bin", 192), 0, completedFlags, 832, 192);
            return BuildTexture(completedMap, completedFlags, palettes);
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

        return BuildTexture(map, flags, palettes);
    }

    private static Texture2D BuildCompletedTop(Color[,] palettes) => BuildTexture(
        ReadBytes(Root + "map_black_tower_stage_3_top.bin", 256),
        ReadBytes(Root + "flags_black_tower_stage_3_top.bin", 256), palettes);

    private static Texture2D BuildTexture(byte[] map, byte[] flags, Color[,] palettes)
    {
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
            map, flags, tiles, palettes, rows: map.Length / 32);
    }
}
