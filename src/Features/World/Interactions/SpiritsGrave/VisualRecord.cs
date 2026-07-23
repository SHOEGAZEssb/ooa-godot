using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal readonly record struct VisualRecord(string Key, string[] Sprites, int TileBase, int Palette, bool SourceGrayscaleInverted, string[] Animations);
