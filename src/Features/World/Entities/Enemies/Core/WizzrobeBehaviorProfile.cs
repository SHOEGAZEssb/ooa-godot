using System.Collections.Generic;

namespace oracleofages;

internal sealed record WizzrobeBehaviorProfile(
    IReadOnlyList<EnemyBehaviorValue> State,
    IReadOnlyList<EnemyBehaviorValue> HookRecovery,
    IReadOnlyList<EnemyBehaviorValue> Effects,
    IReadOnlyList<EnemyBehaviorValue> ActiveCollisions,
    IReadOnlyList<EnemyBehaviorValue> EmberStatus,
    IReadOnlyList<EnemyBehaviorValue> PegasusStatus,
    IReadOnlyList<EnemyBehaviorValue> PartData,
    IReadOnlyList<EnemyBehaviorValue> PartState,
    IReadOnlyList<EnemyBehaviorValue> PartEffects,
    IReadOnlyList<EnemyBehaviorValue> PartActiveCollisions)
{
    internal int GreenInitial => State[0].Value;
    internal int GreenPhaseIn => State[1].Value;
    internal int AttackCounter => State[2].Value;
    internal int FireCounter => State[3].Value;
    internal int GreenFlickerThreshold => State[4].Value;
    internal int GreenPhaseOut => State[5].Value;
    internal int GreenWait => State[6].Value;
    internal int RedPhaseIn => State[7].Value;
    internal int RedPhaseOut => State[8].Value;
    internal int RedHideCounter => State[9].Value;
    internal int BlueSpeed => State[10].Value;
    internal int BlueWait => State[11].Value;
    internal int BlueAttackBase => State[12].Value;
    internal int BlueAttackMask => State[13].Value;
    internal int BlueAimBase => State[14].Value;
    internal int BlueAimMask => State[15].Value;
    internal int BlueReaimBase => State[16].Value;
    internal int BlueReaimMask => State[17].Value;
    internal int BlueFireMask => State[18].Value;
    internal int SpawnYMask => State[19].Value;
    internal int SpawnXMask => State[20].Value;
    internal int SpawnXLimit => State[21].Value;
    internal int SpawnCenter => State[22].Value;
    internal int SpawnTileMask => State[23].Value;
    internal int ReservationCount => State[24].Value;
    internal int ReservationBytes => State[25].Value;
    internal int TargetRadius => State[26].Value;
    internal int TargetDiameter => State[27].Value;
    internal int HookGravity => State[28].Value;
}
