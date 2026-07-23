using Godot;
using System;

namespace oracleofages;
internal readonly record struct SwordPart(int Y, int X, int Tile, bool FlipX = false, bool FlipY = false);
