namespace Oracles;

// commonBossCode.s: enemyBoss_initializeRoomWithoutExtraGfx publishes this
// after Link's visit (or during scrolling preload). Link alone runs state$0b.
internal sealed class BossEntryRequest(OracleRuntimeState memory)
{
    internal byte Publish()
    {
        if ((memory.ReadField("wScrollMode") & 1) != 0) return 1;
        byte direction = memory.ReadField("wScreenTransitionDirection");
        if (direction > 3)
            throw new System.NotSupportedException($"commonBossCode.s: boss entry direction${direction:x2} requires its native Link binding.");
        memory.SetField("wLinkForceState", 0x0b);
        memory.SetField("wLinkStateParameter", memory.Game == OracleGame.Ages ? (byte)0x16 : (byte)0x1a);
        memory.SetWramByte(0xd008, direction);
        byte angle = (byte)(direction * 8);
        memory.SetWramByte(0xd009, angle);
        return angle;
    }
}
