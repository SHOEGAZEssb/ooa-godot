
namespace Oracles;

internal readonly record struct CheepCheepBehaviorProfile(
    int SpeedRaw, int RestFrames, IReadOnlyList<EnemyBehaviorValue> Sources);
