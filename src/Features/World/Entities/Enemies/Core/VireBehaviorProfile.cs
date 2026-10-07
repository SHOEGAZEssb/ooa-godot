using System.Collections.Generic;

namespace oracleofages;

internal sealed record VireBehaviorProfile(IReadOnlyList<EnemyBehaviorValue> State,
    IReadOnlyList<EnemyBehaviorValue> Behaviors, IReadOnlyList<EnemyBehaviorValue> Spawns,
    IReadOnlyList<EnemyBehaviorValue> BatZ, IReadOnlyList<EnemyBehaviorValue> BatOffsets,
    IReadOnlyList<EnemyBehaviorValue> Effects, IReadOnlyList<EnemyBehaviorValue> ActiveCollisions)
{
    internal int InitialSpeed => State[0].Value;
    internal int HiddenWait => State[1].Value;
    internal int FireAnimation => State[2].Value;
    internal int IntroWait => State[3].Value;
    internal int RetreatWait => State[4].Value;
    internal int CircleWait => State[5].Value;
    internal int CreepWait => State[6].Value;
    internal int FireWait => State[7].Value;
    internal int SplitWait => State[8].Value;
    internal int Children => State[9].Value;
    internal int ReappearWait => State[10].Value;
    internal int FarewellWait => State[11].Value;
    internal int DeathWait => State[12].Value;
    internal int DepartureZStep => State[13].Value;
    internal int BatRiseWait => State[14].Value;
    internal int BatChaseWait => State[15].Value;
    internal int BatChargeWait => State[16].Value;
    internal int BatAvoidRadius => State[17].Value;
    internal int BatRecoverWait => State[18].Value;
    internal int FleeRadius => State[19].Value;
    internal int RingAxisLimit => State[20].Value;
    internal int RingRadius => State[21].Value;
    internal int RingWidth => State[22].Value;
    internal int BoundsY => State[23].Value;
    internal int BoundsX => State[24].Value;
    internal int CenterY => State[25].Value;
    internal int CenterX => State[26].Value;
    internal int HighHealth => State[27].Value;
    internal int MediumHealth => State[28].Value;
    internal int BatRiseZStep => State[29].Value;
    internal int BatArrivalRadius => State[30].Value;
    internal int BatArrivalZ => State[31].Value;
    internal int BatMinimumZ => State[32].Value;
    internal int ApproachSpeed => State[33].Value;
    internal int ChargeSpeed => State[34].Value;
    internal int FleeSpeed => State[35].Value;
    internal int EscapeSpeed => State[36].Value;
    internal int CircleSpeed => State[37].Value;
    internal int RadialSpeed => State[38].Value;
    internal int BatFollowSpeed => State[39].Value;
}
