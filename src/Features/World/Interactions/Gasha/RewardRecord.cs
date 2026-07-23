using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal readonly record struct RewardRecord(int Type, int TreasureId, int Parameter, int TextId, GashaSpotDatabaseVisualRecord Visual);
