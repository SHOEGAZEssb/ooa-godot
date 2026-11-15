namespace Oracles;

// INTERAC_SEASON_SPIRITS_SCRIPTS $23 remains an invisible, A-sensitive native slot.
internal sealed class SeasonsTempleScriptRoomEntity : NativeNpcRoomEntity
{
    private readonly SeasonsOpeningSession _session;
    private readonly SeasonsTalkNpcDatabase _npcs;
    private readonly RodOfSeasonsDatabase _data;
    internal SeasonsTempleScriptHost? Host { get; private set; }
    internal SeasonsTempleScriptRoomEntity(SeasonsOpeningSession session, SeasonsTalkNpcDatabase npcs,
        RodOfSeasonsDatabase data, NativeObjectInstruction source, OracleRoomData room)
        : base(npcs.CreateUninitializedActor(source, room, session.Data.World.SpritePalettes), session.Memory, session.Link,
            index => npcs.Animation(0x30, index), data.Text)
    {
        _session = session; _npcs = npcs; _data = data;
        Npc.SetScriptVisible(false); Npc.SetBlocksLink(false);
    }
    protected override bool InitializeNpc(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        WriteByte(0x45, 1); WriteByte(0x43, ReadByte(0x42) >> 4);
        WriteByte(0x7e, _session.Save.ReadWramByte(0xc6e5) >= 9 ? 1 : 0);
        string program = ReadByte(0x42) == 0x40 ? "seasonsSpiritsScript_enteringTempleArea" :
            (ReadByte(0x42) & 15) == 0 ? "seasonsSpiritsScript_winterTempleOrbBridge" : "seasonsSpiritsScript_spiritStatue";
        Script = Host = new(this, _session, _npcs, _data, program);
        Script.AdvanceObjectUpdate(frame.Player);
        if (Host.Completed) Delete();
        return false;
    }
    protected override bool UpdateNativeNpc(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    { Script!.AdvanceObjectUpdate(frame.Player); if (Host!.Completed) Delete(); return true; }
    public override void ReleaseNativeInteractionSlot() { Host?.CancelPalette(); base.ReleaseNativeInteractionSlot(); }
    protected override void AdvanceNpc(RoomEntityFrame frame) { }
}
