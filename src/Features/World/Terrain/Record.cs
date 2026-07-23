using System;
using System.Collections.Generic;

namespace oracleofages;
internal readonly record struct Record(int Group, int Room, byte Mask, int Position, byte Tile, string Source);
