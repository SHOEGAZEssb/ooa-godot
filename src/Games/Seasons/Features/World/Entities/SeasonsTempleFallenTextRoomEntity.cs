namespace Oracles;

// miscellaneous1.s:$6b:$26 installs a generic NPC script without initializing graphics or a hitbox.
internal sealed class SeasonsTempleFallenTextRoomEntity : NativeNpcRoomEntity
{
    private readonly SeasonsOpeningSession _session;
    private readonly SeasonsTalkNpcDatabase _data;
    internal SeasonsTempleFallenTextRoomEntity(SeasonsOpeningSession session, SeasonsTalkNpcDatabase data,
        NativeObjectInstruction source, OracleRoomData room)
        : base(data.CreateUninitializedActor(source, room, session.Data.World.SpritePalettes), session.Memory,
            session.Link, index => data.Animation(0x30,index), data.Text)
    { _session = session; _data = data; Npc.SetScriptVisible(false); Npc.SetBlocksLink(false); }
    protected override bool InitializeNpc(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        WriteByte(0x44, 1); WriteByte(0x66, 0); WriteByte(0x67, 0); WriteByte(0x72, 0); WriteByte(0x73, 0);
        Script = new SeasonsTalkNpcScriptHost(this, _session, _data, _data.Program("subrosianScript_templeFallenText"));
        return false;
    }
    protected override bool UpdateNativeNpc(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    { Script!.AdvanceObjectUpdate(frame.Player); return true; }
}
