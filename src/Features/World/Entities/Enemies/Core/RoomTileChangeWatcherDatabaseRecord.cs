using System;
using System.Collections.Generic;

namespace oracleofages;
internal readonly record struct RoomTileChangeWatcherDatabaseRecord(int Group, int Room, int Order, int Position, byte RoomFlag, string Source);
