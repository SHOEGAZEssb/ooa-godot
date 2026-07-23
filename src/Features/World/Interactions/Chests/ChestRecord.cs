using System;
using System.Collections.Generic;

namespace oracleofages;
public readonly record struct ChestRecord(int Group, int Room, int Position, string TreasureObject, int TreasureId, int SubId, int Parameter, int TextId, int Graphic, int Amount, string Message);
