using System.Collections.Generic;

namespace oracleofages;

internal readonly record struct CheepCheepBehaviorProfile(
    int SpeedRaw, int RestFrames, IReadOnlyList<EnemyBehaviorValue> Sources);
