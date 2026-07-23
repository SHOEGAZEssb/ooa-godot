using System;
using System.Collections.Generic;

namespace oracleofages;
internal readonly record struct PlacementRecord(int Group, int Room, int Order, DungeonEntranceInteractionDatabaseObjectKind Kind, int Id, int SubId, int Y, int X, int Dungeon, string Source);
