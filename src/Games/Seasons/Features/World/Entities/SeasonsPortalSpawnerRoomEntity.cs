namespace Oracles;

// portalSpawner.s: the visible portal waits for the covered tile to become $e6.
internal sealed class SeasonsPortalSpawnerRoomEntity : NativeNpcRoomEntity, IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly SeasonsOpeningSession _session;
    public override bool UpdatesDuringDialogue => true;
    public override bool UpdatesDuringRoomEntityFreeze => true;
    public void UpdateDuringScreenTransition(RoomEntityFrame frame) => UpdateFrame(frame, []);
    internal SeasonsPortalSpawnerRoomEntity(SeasonsOpeningSession session, SeasonsTalkNpcDatabase data,
        NativeObjectInstruction source, OracleRoomData room)
        : base(data.Create(source, room, session.Data.World.SpritePalettes), session.Memory, session.Link,
            index => data.Animation(0xe1, index), data.Text)
    { _session = session; Npc.SetBlocksLink(false); }
    protected override bool InitializeNpc(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    { WriteByte(0x44, 1); WriteByte(0x40, 0x81); return true; }
    protected override bool UpdateNativeNpc(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (ReadByte(0x44) == 2) { Npc.AdvanceAnimationUpdates(1); return true; }
        int group = _session.Rooms.ActiveGroup;
        if (group == 0 && _session.Room.GetMetatile(Npc.Position) != 0xe6) return true;
        if (group < 2) _session.Save.SetRoomFlag(group, _session.RoomId, 8);
        else if (group == 3) _session.Save.SetRoomFlag(0, _session.RoomId == 0xa8 ? 4 : 0xf7, 8);
        WriteByte(0x44, 2); WriteByte(0x5a, 0x83); Npc.ZIndex = ObjectDrawPriority.FixedLowPriorityZIndex; Npc.SetScriptVisible(true);
        return true;
    }
}
