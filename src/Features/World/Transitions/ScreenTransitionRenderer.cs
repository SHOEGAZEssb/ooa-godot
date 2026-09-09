using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// Both room tilemaps address the same live BG VRAM and palette slots. This
// owner schedules the original uploads; it never crossfades room textures.
internal sealed class ScreenTransitionRenderer
{
    private readonly OracleRoomData _source;
    private readonly OracleRoomData _target;
    private readonly BackgroundPaletteState _palettes;
    private readonly Image _vram;
    private readonly Color[,] _destinationColors;
    private readonly IReadOnlyList<ScreenGraphicsUpload> _uploads;
    private readonly int _uploadStart;
    private readonly int _paletteCommit;
    private readonly int _smoothStart;
    private readonly (byte[] Source, byte[] Destination)? _smooth;
    private int _tick;

    internal ScreenTransitionRenderer(OracleRoomData source, OracleRoomData target,
        Image vram, Color[,] originalColors, IReadOnlyList<ScreenGraphicsUpload> uploads,
        int uploadStart, int paletteCommit, int smoothStart,
        (byte[] Source, byte[] Destination)? smooth)
    {
        _source = source;
        _target = target;
        _palettes = target.BackgroundPalettes;
        _vram = vram;
        _uploads = uploads;
        _uploadStart = uploadStart;
        _paletteCommit = paletteCommit;
        _smoothStart = smoothStart;
        _smooth = smooth;
        Color[,] incoming = _palettes.Capture();
        _destinationColors = new Color[6, 4];
        for (int p = 0; p < 6; p++)
        for (int c = 0; c < 4; c++)
            _destinationColors[p, c] = incoming[p + 2, c];
        _palettes.Restore(originalColors);
        source.SetLiveGraphics(vram, true);
        target.SetLiveGraphics(vram, true);
    }

    internal void Advance()
    {
        _tick++;
        int upload = _tick - _uploadStart;
        if (upload >= 0 && upload < _uploads.Count)
        {
            ScreenGraphicsUpload entry = _uploads[upload];
            if (entry.Palette >= 0)
            {
                if (entry.Palette != _target.TilesetPaletteId)
                    throw new InvalidOperationException($"uniqueGfxHeader palette ${entry.Palette:x2} disagrees with destination tileset ${_target.TilesetId:x2}.");
                _palettes.LoadTileset(_destinationColors);
            }
            else
            {
                ApplyUpload(entry);
            }
        }
        if (_tick == _paletteCommit && _smooth is null)
            _palettes.LoadTileset(_destinationColors);
        int age = _tick - _smoothStart;
        if (_smooth is { } fade && age is > 0 and < 32 && (age & 1) == 0)
        {
            // paletteFadeHandler08 updates BG2-4 on odd updates and BG5-7
            // on even updates. Only the latter dirties hardware palettes.
            // Both halves then have the same 4-bit blend weight; update 32
            // stops the thread without writing the final lower half.
            int weight = age / 2;
            Color[,] colors = new Color[6, 4];
            for (int p = 0; p < 6; p++)
            for (int c = 0; c < 4; c++)
            {
                int i = (p * 4 + c) * 3;
                byte Component(int n) => (byte)Mathf.RoundToInt(
                    ((fade.Source[n] * (16 - weight) + fade.Destination[n] * weight) >> 4) * 255.0f / 31.0f);
                colors[p, c] = Color.Color8(Component(i), Component(i + 1), Component(i + 2));
            }
            _palettes.LoadTileset(colors);
        }
        // OracleWorldData already redraws the active room on a palette write;
        // outgoing tiles must use those same slots and the same VRAM writes.
        if ((upload >= 0 && upload < _uploads.Count) ||
            _tick == _paletteCommit || (_smooth is not null && age is > 0 and < 32 && (age & 1) == 0))
        {
            _source.RedrawForPaletteChange();
            if (_source != _target) _target.RedrawForPaletteChange();
        }
    }

    private void ApplyUpload(ScreenGraphicsUpload upload)
    {
        int startTile = (upload.Address - 0x8800) / 16;
        for (int tile = 0; tile < upload.Tiles; tile++)
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
        {
            int offset = tile * 16 + y * 2;
            int shade = ((upload.Data[offset] >> (7 - x)) & 1) |
                (((upload.Data[offset + 1] >> (7 - x)) & 1) << 1);
            byte intensity = (byte)(255 - shade * 85);
            int index = startTile + tile;
            _vram.SetPixel(index % 16 * 8 + x, index / 16 * 8 + y,
                Color.Color8(intensity, intensity, intensity));
        }
    }

    internal void Finish()
    {
        _palettes.LoadTileset(_destinationColors);
        if (_source != _target) _source.SetLiveGraphics(null, false);
        // Retain uploaded bytes after scrolling; animation/scripted uploads
        // resume on the next gameplay update. Full loads replace this buffer.
        _target.SetLiveGraphics(_vram, false);
    }
}
