using System;
using System.Collections.Generic;

namespace oracleofages;
public readonly record struct BehaviourRecord(int TreasureId, TreasureVariable Variable, CollectionMode Mode, int RawMode, int Sound);
