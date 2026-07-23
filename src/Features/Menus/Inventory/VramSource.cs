using Godot;
using System;
using static oracleofages.OracleGraphicsData;
using static oracleofages.OracleTileRenderer;

namespace oracleofages;
internal readonly record struct VramSource(int FirstTile, Image Image, bool Interleaved, bool SpriteEncoding = false)
{
    public int TileCount => Image.GetWidth() / 8 * (Image.GetHeight() / 8);
}
