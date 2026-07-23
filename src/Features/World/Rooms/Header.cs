using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
public readonly record struct Header(int Sheet, int DestinationTile, int TileCount, int SourceTile);
