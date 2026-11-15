namespace Oracles;

internal sealed class SeasonsTempleDecorationRoomEntity : NativeNpcRoomEntity
{
    internal SeasonsTempleDecorationRoomEntity(SeasonsOpeningSession session, SeasonsTalkNpcDatabase data,
        NativeObjectInstruction source, OracleRoomData room)
        : base(data.Create(source, room, session.Data.World.SpritePalettes), session.Memory, session.Link,
            index => data.Animation(0x4c, index), _ => string.Empty) { Npc.SetBlocksLink(false); }
    protected override bool InitializeNpc(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    { WriteByte(0x5a, 0x83); Npc.SetFixedDrawPriority(ObjectDrawPriority.FixedLowPriorityZIndex); Npc.SetScriptVisible(true); return false; }
    protected override bool UpdateNativeNpc(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    { Npc.AdvanceAnimationUpdates(1); return true; }
    protected override void AdvanceNpc(RoomEntityFrame frame) { }
}
