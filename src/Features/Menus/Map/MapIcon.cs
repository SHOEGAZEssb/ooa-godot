using Godot;
using System;
using static oracleofages.OracleGraphicsData;
using static oracleofages.OracleTileRenderer;

namespace oracleofages;
internal readonly record struct MapIcon(int LeftTile, int RightTile, int Palette, bool RightFlipX = false, bool RightFlipY = false);
