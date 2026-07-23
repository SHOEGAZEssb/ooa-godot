using System;
using System.Collections.Generic;

namespace oracleofages;
public readonly record struct PortalRecord(int Group, int Room, int SubId, int Y, int X, string SpriteName, int TileBase, int Palette, int LoopStart, string Animation);
