using System;
using System.Collections.Generic;

namespace oracleofages;
internal readonly record struct DungeonMechanicDatabaseRecord(int Group, int Room, int Order, int Id, int SubId, int PackedPosition, int Parameter, TriggerPredicate Predicate, bool CountSourceComplete);
