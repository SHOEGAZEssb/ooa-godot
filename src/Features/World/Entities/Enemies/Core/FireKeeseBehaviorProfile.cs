using System.Collections.Generic;

namespace oracleofages;

internal readonly record struct FireKeeseBehaviorProfile(IReadOnlyList<EnemyBehaviorValue> Values)
{
    internal int OrbitFrames => Values[0].Value;
    internal int InitialZ => Values[1].Value;
    internal int OrbitSpeed => Values[2].Value;
    internal int DiveFrames => Values[3].Value;
    internal int DiveSpeed => Values[4].Value;
    internal int RiseStep => Values[5].Value;
    internal int CloseRange => Values[6].Value;
    internal int RelightFrames => Values[7].Value;
    internal int RelightMidpoint => Values[8].Value;
    internal int TorchTile => Values[9].Value;
    internal int ScanTiles => Values[10].Value;
    internal int LayoutSize => Values[11].Value;
    internal int RoomWidth => Values[12].Value;
    internal int CenterX => Values[13].Value;
    internal int CenterY => Values[14].Value;
    internal int CorrectionSpeed => Values[15].Value;
    internal int SeekSpeed => Values[16].Value;
    internal int GroundHigh => Values[17].Value;
    internal int GroundStep => Values[18].Value;
    internal int LitDamage => Values[19].Value;
    internal int UnlitDamage => Values[20].Value;
    internal int AngleMask => Values[21].Value;
}
