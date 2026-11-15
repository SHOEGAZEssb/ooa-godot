namespace Oracles;

internal sealed class SeasonsTempleSparkleRoomEntity : NativeNpcRoomEntity
{
    private readonly SeasonsOpeningSession _session;
    public override bool UpdatesDuringDialogue => true;
    public override bool UpdatesDuringRoomEntityFreeze => true;
    internal SeasonsTempleSparkleRoomEntity(SeasonsOpeningSession session, SeasonsTalkNpcDatabase data,
        NativeObjectInstruction source, OracleRoomData room)
        : base(data.Create(source, room, session.Data.World.SpritePalettes), session.Memory, session.Link,
            index => data.Animation(0x84, index), _ => string.Empty)
    { _session = session; Npc.SetScriptVisible(false); Npc.SetBlocksLink(false); }
    protected override bool InitializeNpc(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        WriteByte(0x40, ReadByte(0x40) | 0x80);
        int subid = ReadByte(0x42);
        if (subid == 0) WriteByte(0x46, 120);
        int visible = subid is 0 or 7 ? 0x82 : 0x81;
        WriteByte(0x5a, visible); Npc.SetFixedDrawPriority(ObjectDrawPriority.FromVisible(visible)); Npc.SetScriptVisible(true);
        return false;
    }
    protected override bool UpdateNativeNpc(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (ReadByte(0x42) is 0 or 7)
        {
            WriteByte(0x46, ReadByte(0x46) - 1);
            if (ReadByte(0x46) == 0) { Delete(); return true; }
            Npc.AdvanceAnimationUpdates(1); Npc.SetScriptVisible((_session.FrameCounter & 1) == 0); return true;
        }
        if (Npc.CurrentAnimationParameter == 0xff) Delete(); else Npc.AdvanceAnimationUpdates(1);
        return true;
    }
    protected override void AdvanceNpc(RoomEntityFrame frame) { }
}
