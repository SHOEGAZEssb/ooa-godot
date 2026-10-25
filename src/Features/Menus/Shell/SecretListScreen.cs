using Godot;
using System;
using System.Text;

namespace Oracles;

// GFXH_SECRET_LIST_MENU uses unsigned tiles and a window at Y=$60.
// The middle $18..$5f scanlines alone use the scrolling register set.
public sealed partial class SecretListScreen : Node2D
{
    private readonly byte[][] _vram = [new byte[0x1000], new byte[0x1000]];
    private Texture2D? _map;
    private Image? _cursor;
    private Color[,] _cursorPalette = new Color[8, 4];
    internal int Cursor { get; set; }
    internal int ScrollY { get; set; } = 0x30;
    internal byte VramByte(int bank, int offset) => _vram[bank][offset];
    internal Texture2D Map => _map!;
    internal void Initialize(SecretListDatabase data, OracleSaveData save)
    {
        foreach (var bank in _vram) Array.Fill(bank, (byte)0xff);
        var graphics = OracleGraphicsCache.LoadImage(AgesGameContent.AssetRoot + "menu/gfx_secret_list_menu.png");
        int count = graphics.GetWidth() / 8 * (graphics.GetHeight() / 8);
        CopyGraphics(graphics, 0x700, count);
        _cursor = OracleGraphicsCache.LoadImage(AgesGameContent.AssetRoot + "map/sprites.png");
        CopyGraphics(_cursor, 0, 0x20);
        int index = 0;
        foreach (var entry in data.Entries(save))
        {
            byte[] glyphs = OracleTileRenderer.CopyTextCharacterGraphics(Encoding.ASCII.GetBytes(data.Name(entry, save)), 0);
            int address = 0xa00 + index++ * 0x200;
            glyphs.CopyTo(_vram[address < 0x1000 ? 0 : 1], address < 0x1000 ? address : address - 0x1000);
        }
        _cursorPalette = OracleGraphicsData.LoadPalette(AgesGameContent.AssetRoot + "menu/secret_list_obj.bin");
        SetSecret(new byte[24], unlocked: false);
    }
    private void CopyGraphics(Image graphics, int destination, int count)
    {
        for (int tile = 0; tile < count; tile++)
        {
            var origin = OracleTileRenderer.SourceTileOrigin(graphics, tile, interleaved: true);
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                int shade = OracleGraphicsData.TwoBitShade(graphics.GetPixel(origin.X + x, origin.Y + y));
                for (int bit = 0; bit < 2; bit++)
                {
                    int address = destination + tile * 16 + y * 2 + bit;
                    _vram[0][address] = (byte)((_vram[0][address] & ~(0x80 >> x)) | (((shade >> bit) & 1) << (7 - x)));
                }
            }
        }
    }
    internal void SetSecret(byte[] text, bool unlocked)
    {
        Array.Clear(_vram[0], 0x300, 0x300);
        if (unlocked) OracleTileRenderer.CopyTextCharacterGraphics(text, 0xff).CopyTo(_vram[0], 0x300);
        using Image bank0 = OracleGraphicsCache.DecodeTwoBitSpriteSheet(_vram[0], spriteEncoding: false);
        using Image bank1 = OracleGraphicsCache.DecodeTwoBitSpriteSheet(_vram[1], spriteEncoding: false);
        var tiles = new OracleVramTileMap();
        for (int tile = 0; tile < 256; tile++)
        {
            var origin = OracleTileRenderer.SourceTileOrigin(bank0, tile, interleaved: true);
            int sourceTile = origin.Y / 8 * 16 + origin.X / 8;
            tiles.MapTile(bank0, sourceTile, tile, 0); tiles.MapTile(bank1, sourceTile, tile, 1);
        }
        _map = OracleTileRenderer.BuildTileMapTexture(
            OracleAssetCache.ReadBytes(AgesGameContent.AssetRoot + "menu/map_secret_list_menu.bin"),
            OracleAssetCache.ReadBytes(AgesGameContent.AssetRoot + "menu/flg_secret_list_menu.bin"), tiles,
            OracleGraphicsData.LoadPalette(AgesGameContent.AssetRoot + "menu/secret_list_bg.bin", 4, 2), 32, 32);
        QueueRedraw();
    }
    public override void _Draw()
    {
        if (_map is null) return;
        DrawTextureRectRegion(_map, new(0, 0, 160, 24), new(0, 48, 160, 24));
        DrawTextureRectRegion(_map, new(0, 24, 160, 72), new(0, 24 + ScrollY, 160, 72));
        DrawTextureRectRegion(_map, new(0, 96, 160, 48), new(0, 0, 160, 48));
        OracleTileRenderer.DrawOamTile(this, _cursor!, 0, 0x0c, 4,
            new(12, 74 + Cursor * 16 - ScrollY), true, false, _cursorPalette, inverted: false);
    }
}
