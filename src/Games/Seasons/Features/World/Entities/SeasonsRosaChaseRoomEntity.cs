namespace Oracles;

// subrosiansHiding.s: $6c:$00 retains its slot across rooms; $01 is the visible hider.
internal sealed class SeasonsRosaChaseRoomEntity : NativeNpcRoomEntity,
    IAlwaysUpdateDuringScreenTransitionRoomEntity, IScrollPersistentRoomEntity
{
    private readonly SeasonsOpeningSession _session;
    private readonly SeasonsTalkNpcDatabase _data;
    private readonly NativeObjectInstruction _source;
    private readonly NativeScriptInputControl _control;
    private bool _roomGateOwned;
    // This handler dispatches on substate, leaving Interaction.state untouched.
    protected override bool Initialized { get => ReadByte(0x45) != 0; set { } }
    public override bool UpdatesDuringDialogue => true;
    public override bool UpdatesDuringRoomEntityFreeze => true;
    public bool PersistsAcrossScroll => (ReadByte(0x40) & 2) != 0;
    public void EnterRoom(OracleRoomData room) { }
    public void UpdateDuringScreenTransition(RoomEntityFrame frame) => UpdateFrame(frame, []);
    internal SeasonsRosaChaseRoomEntity(SeasonsOpeningSession session, SeasonsTalkNpcDatabase data,
        NativeObjectInstruction source, OracleRoomData room)
        : base(data.Create(source, room, session.Data.World.SpritePalettes), session.Memory, session.Link,
            index => data.Animation(0x6c, index), data.Text)
    { _session = session; _data = data; _source = source; _control = new(session.Memory); Npc.SetBlocksLink(false); }
    protected override bool InitializeNpc(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    { AdvanceNative(frame.Player); return false; }
    protected override bool UpdateNativeNpc(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    { AdvanceNative(frame.Player); return true; }
    private void AdvanceNative(Player player)
    {
        if (ReadByte(0x42) == 0) { AdvanceController(); return; }
        int substate = ReadByte(0x45);
        if (substate == 0)
        {
            WriteByte(0x45, 1); WriteByte(0x6d, 8); WriteByte(0x5a, 0xc2);
            Npc.ZIndex = ObjectDrawPriority.BehindLinkZIndex;
            Npc.SetScriptVisible(true);
            Script = new SeasonsRosaChaseScriptHost(this, _session, _data, ReadByte(0x43));
            substate = 1;
        }
        var script = (SeasonsRosaChaseScriptHost)Script!;
        switch (substate)
        {
            case 1:
            case 2:
                script.AdvanceObjectUpdate(player);
                if (script.Completed) { DeleteNative(); return; }
                if (UpdateZ() && substate == 2) SetWord(0x54, -0x1c0);
                break;
            case 3:
            case 4:
                if (substate == 4 && ReadByte(0x7b) == 0xff)
                { script.BeginCaught(); script.AdvanceObjectUpdate(player); return; }
                Npc.AdvanceAnimationUpdates(1);
                if (!UpdateZ()) return;
                script.AdvanceObjectUpdate(player);
                if (script.Completed) DeleteNative();
                break;
            case 5:
                Npc.AdvanceAnimationUpdates(1);
                if (!UpdateZ()) return;
                Escape();
                break;
            default: throw new NotSupportedException($"subrosiansHiding.s: Rosa substate ${substate:x2} is unsupported.");
        }
    }
    private void AdvanceController()
    {
        if (ReadByte(0x45) == 0)
        {
            WriteByte(0x45, 1);
            if (!_session.Save.HasTreasure(TreasureId.Essence) || (_session.Save.ReadField("wEssencesObtained") & 1) == 0 ||
                _session.Save.HasRoomFlag(_session.Rooms.ActiveGroup, _session.RoomId, 0x80))
            { DeleteNative(); return; }
            WriteByte(0x40, 0x83);
            _control.SetDisabledObjects(1); _control.SetMenuDisabled(1);
            WriteByte(0x79, _session.RoomId);
            _session.Sound.SetNativeActiveMusic(0); _session.Sound.PlayNativeSound(0x0b);
            SpawnHider(); return;
        }
        if (ReadByte(0x78) == 0)
        {
            if (_session.RoomId != 0xcb) { AbortRoute(); return; }
            if (_session.Memory.ReadField("wcc84") == 2) WriteByte(0x78, 1);
            return;
        }
        if (_session.Memory.ReadWramByte(0xcfc0) != 0)
        { WriteByte(0x7a, 1); _control.SetScreenTransitionsDisabled(0); }
        if (_session.RoomId == ReadByte(0x79)) return;
        WriteByte(0x79, _session.RoomId);
        if (_session.RoomId == 0xcb || ReadByte(0x7a) == 0) { AbortRoute(); return; }
        WriteByte(0x7a, 0); WriteByte(0x46, ReadByte(0x46) + 1);
        var rooms = _data.NativeTable("rosa-rooms");
        int step = ReadByte(0x46);
        if (step >= rooms.Length || _session.RoomId != rooms[step]) { AbortRoute(); return; }
        SpawnHider();
    }
    private void SpawnHider()
    {
        _session.Memory.SetField("wcc85", 0x80); _roomGateOwned = true;
        _control.SetScreenTransitionsDisabled(1);
        int step = ReadByte(0x46);
        var positions = _data.NativeTable("rosa-start-positions");
        if (step * 2 + 1 >= positions.Length)
            throw new NotSupportedException($"subrosiansHiding.s: spawnHider step ${step:x2} is outside table_67f1.");
        if (!_session.Entities.InteractionSlotAvailable)
        {
            // Unchecked allocation leaves HL=$e040, aliasing the sound owner's WRAM through echo RAM.
            var owner = _session.Entities;
            owner.WriteSoundAllocationAlias(0xc03f, (byte)(owner.NativeSoundAllocationAliasSource(0xc03f) | 0x80));
            owner.WriteSoundAllocationAlias(0xc040, 0x6c);
            owner.WriteSoundAllocationAlias(0xc041, 1);
            owner.WriteSoundAllocationAlias(0xc042, (byte)step);
            owner.WriteSoundAllocationAlias(0xc04b, positions[step * 2]);
            owner.WriteSoundAllocationAlias(0xc04d, positions[step * 2 + 1]);
        }
        else
        {
            var source = _source with { SubId = 1, Var03 = step, Y = positions[step * 2], X = positions[step * 2 + 1] };
            var child = _session.Entities.TryAllocateInteraction(() =>
                new SeasonsRosaChaseRoomEntity(_session, _data, source, _session.Room))!;
            child.WriteByte(0x40, 0x81);
        }
        if (step == 4) { ClearRouteGate(); DeleteNative(); }
    }
    private void AbortRoute()
    {
        if (_session.RoomId == 0xcb) { DeleteNative(); return; }
        int music = _session.Sound.NativeRoomMusic;
        _session.Sound.SetNativeActiveMusic(music); _session.Sound.PlayNativeSound(music);
        ClearRouteGate(); DeleteNative();
    }
    private void ClearRouteGate()
    { _session.Memory.SetField("wcc85", 0); _roomGateOwned = false; _control.SetScreenTransitionsDisabled(0); }
    private void DeleteNative() { _control.Relinquish(); _roomGateOwned = false; Delete(); }
    private int Word(int offset) => ReadByte(offset) | ReadByte(offset + 1) << 8;
    private void SetWord(int offset, int value) { WriteByte(offset, value); WriteByte(offset + 1, value >> 8); }
    private bool UpdateZ()
    {
        int z = unchecked((short)Word(0x4e)), speed = unchecked((short)Word(0x54));
        bool landed = OracleObjectMath.UpdateSpeedZ(ref z, ref speed, 0x20);
        SetWord(0x4e, z); SetWord(0x54, speed);
        Npc.SetScriptDrawOffset(new(0, z >> 8));
        return landed;
    }
    private void Escape()
    {
        int position = _session.Room.GetPackedPosition(Npc.Position.Floor() + new Vector2(0, 9));
        byte collision = _session.Memory.ReadWramByte(0xce00 + position);
        int angle = 0x10;
        if (collision is not (0 or 0xff))
        {
            angle = ReadByte(0x6d); WriteByte(0x49, angle);
            int direction = ((angle + 4) & 0x1f) >> 3;
            int adjacent = unchecked((byte)(position + ((direction ^ 2) - 2)));
            if (_session.Memory.ReadWramByte(0xce00 + adjacent) != 0)
            { angle = angle == 8 ? 0x18 : 8; WriteByte(0x6d, angle); }
        }
        WriteByte(0x49, angle); Move(ReadByte(0x50), angle);
        if (ReadByte(0x4b) < 0x90) return;
        _session.Memory.SetField("wDisabledObjects", 0); _session.Memory.SetField("wDisableScreenTransitions", 0);
        DeleteNative();
    }
    public override void ReleaseNativeInteractionSlot()
    {
        _control.Cancel();
        if (_roomGateOwned && _session.Memory.ReadField("wcc85") == 0x80) _session.Memory.SetField("wcc85", 0);
        base.ReleaseNativeInteractionSlot();
    }
}
