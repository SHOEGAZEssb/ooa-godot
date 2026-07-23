using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace oracleofages;
internal readonly record struct InterleavedTile(int Position, byte Tile1, byte Tile2, int Type);
