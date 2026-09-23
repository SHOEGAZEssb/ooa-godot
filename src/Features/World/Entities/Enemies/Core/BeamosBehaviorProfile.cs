using System.Collections.Generic;

namespace oracleofages;

/// <summary>Source operands for ENEMY_BEAMOS $16 and PART_BEAM $29.</summary>
internal sealed class BeamosBehaviorProfile(
    IReadOnlyList<EnemyBehaviorValue> state,
    IReadOnlyList<EnemyBehaviorValue> angles,
    IReadOnlyList<EnemyBehaviorValue> beamState,
    IReadOnlyList<EnemyBehaviorValue> beamAngles)
{
    internal int RotationFrames => state[0].Value;
    internal int CooldownFrames => state[1].Value;
    internal int SoundCounter => state[2].Value;
    internal int ChargeFrames => state[3].Value;
    internal int GlowFrames => state[4].Value;
    internal int TargetAngleWindow => state[5].Value;
    internal int BeamBlinkMask => state[6].Value;
    internal int BeamCollisionDelay => beamState[0].Value;
    internal int BeamSpeed => beamState[1].Value;
    internal int BeamVelocityScale => beamState[2].Value;
    internal IReadOnlyList<EnemyBehaviorValue> AngleAnimations => angles;
    internal IReadOnlyList<EnemyBehaviorValue> BeamAngleAnimations => beamAngles;
}
