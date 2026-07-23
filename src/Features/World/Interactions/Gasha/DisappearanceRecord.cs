using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal readonly record struct DisappearanceRecord(int Phase, int SourceStart, int SourceCount, int DestinationStart, byte[] TileMap);
