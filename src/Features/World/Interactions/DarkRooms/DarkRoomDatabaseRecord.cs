using System;
using System.Collections.Generic;

namespace oracleofages;
internal readonly record struct DarkRoomDatabaseRecord(int Group, int Room, int Order, DarkRoomDatabaseObjectKind Kind, int Id, int SubId, int Y, int X, int Parameter, int RequiredCount, string TreasureObject, string Source);
