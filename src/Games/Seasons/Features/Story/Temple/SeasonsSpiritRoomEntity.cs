namespace Oracles;

// common/interactions/seasonsFairy.s: shared cfc0 handshake and native orbit centre.
internal sealed class SeasonsSpiritRoomEntity : NativeNpcRoomEntity
{
    private readonly SeasonsOpeningSession _session;
    private readonly SeasonsTalkNpcDatabase _data;
    private readonly NativeObjectInstruction _source;
    private readonly RodOfSeasonsDatabase _rod;
    public override bool UpdatesDuringDialogue => true;
    public override bool UpdatesDuringRoomEntityFreeze => true;
    internal SeasonsSpiritRoomEntity(SeasonsOpeningSession session, SeasonsTalkNpcDatabase data,
        NativeObjectInstruction source, OracleRoomData room, RodOfSeasonsDatabase rod)
        : base(data.Create(source, room, session.Data.World.SpritePalettes), session.Memory, session.Link,
            index => data.Animation(0x50, index), _ => string.Empty)
    { _session = session; _data = data; _source = source; _rod = rod; Npc.SetScriptVisible(false); Npc.SetBlocksLink(false); }
    protected override bool InitializeNpc(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        WriteByte(0x44, 0); WriteByte(0x45, 1); WriteByte(0x46, 1); WriteByte(0x4f, 0);
        WriteByte(0x40, ReadByte(0x40) | 0x80);
        _session.Sound.SetNativeActiveMusic(0x0d); _session.Sound.PlaySound(0x0d); return false;
    }
    protected override bool Initialized
    { get => Npc.NativeState is not null && (ReadByte(0x45) != 0 || ReadByte(0x44) != 0); set { } }
    protected override bool UpdateNativeNpc(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        int state = ReadByte(0x44);
        if (state == 0)
        {
            if (!Tick(0x46)) return true;
            if (ReadByte(0x45) == 1)
            { WriteByte(0x45, 2); WriteByte(0x46, 16); _session.Entities.TryCreatePuzzlePuff(Npc.Position); return true; }
            WriteByte(0x44, 1); WriteByte(0x45, 0); WriteByte(0x46, ReadByte(0x43) == 0 ? 120 : 60);
            WriteByte(0x5a, 0x80); Npc.SetFixedDrawPriority(ObjectDrawPriority.FixedHighPriorityZIndex); Npc.SetScriptVisible(true);
            Sparkle(ReadByte(0x43) == 0 ? 0 : 7, ReadByte(0x46)); Npc.AdvanceAnimationUpdates(1); return true;
        }
        if (state == 1)
        {
            Oscillate();
            if (!Tick(0x46))
            { Npc.AdvanceAnimationUpdates(1); Npc.SetScriptVisible((_session.FrameCounter & 1) != 0); return true; }
            Npc.SetScriptVisible(true);
            if (ReadByte(0x43) != 0) { WriteByte(0x44, 5); Signal(); Npc.AdvanceAnimationUpdates(1); return true; }
            WriteByte(0x44, 2); WriteByte(0x4f, 0); WriteByte(0x7a, 0x30); WriteByte(0x49, 0); WriteByte(0x50, 0x14);
            _session.Sound.PlaySound(SoundId.SndChargeSword); state = 2;
        }
        if (state == 2)
        {
            Move(ReadByte(0x50), ReadByte(0x49));
            if (ReadByte(0x4b) >= 16) { Npc.AdvanceAnimationUpdates(1); return true; }
            WriteByte(0x44, 3); WriteByte(0x46, 4); WriteByte(0x7b, 0);
            WriteByte(0x4b, (int)frame.Player.Position.Y); WriteByte(0x4d, (int)frame.Player.Position.X);
            SaveCentre(); state = 3;
        }
        if (state == 3)
        {
            if (unchecked((byte)((int)frame.Player.Position.Y - 16 - ReadByte(0x4b) + 4)) < 9 &&
                unchecked((byte)((int)frame.Player.Position.X - ReadByte(0x4d) + 2)) < 5)
            { WriteByte(0x44, 4); Signal(); }
            else
            {
                WriteByte(0x4b, ReadByte(0x78)); WriteByte(0x4d, ReadByte(0x79));
                WriteByte(0x49, OracleObjectMovement.Shared.RelativeAngle(Npc.Position, frame.Player.Position));
                Move(ReadByte(0x50), ReadByte(0x49)); SaveCentre();
                if (ReadByte(0x7a) != 0 && Tick(0x46)) { WriteByte(0x46, 4); WriteByte(0x7a, ReadByte(0x7a) - 1); }
                Vector2I offset = NativeObjectMovement.CircleArcOffset(_session.Memory, ReadByte(0x7a), ReadByte(0x7b));
                WriteByte(0x4b, ReadByte(0x78) + offset.Y); WriteByte(0x4d, ReadByte(0x79) + offset.X);
                if ((_session.FrameCounter & 1) != 0) WriteByte(0x7b, (ReadByte(0x7b) + 1) & 31);
                if ((_session.FrameCounter & 7) == 0) Sparkle(1, 0);
            }
        }
        else if (state is 4 or 5)
        {
            Oscillate();
            if (_session.Memory.ReadWramByte(0xcfc0) == 7)
            { if (state == 5) _session.Entities.TryCreatePuzzlePuff(Npc.Position); Delete(); return true; }
        }
        Npc.AdvanceAnimationUpdates(1); Npc.SetScriptDrawOffset(new(0, unchecked((sbyte)ReadByte(0x4f))));
        return true;
    }
    private void Oscillate()
    {
        if ((_session.FrameCounter & 7) != 0) return;
        ReadOnlySpan<byte> offsets = _rod.Table("oscillation");
        WriteByte(0x4f, offsets[(_session.FrameCounter & 0x38) >> 3]);
        Npc.SetScriptDrawOffset(new(0, unchecked((sbyte)ReadByte(0x4f))));
    }
    private void SaveCentre() { WriteByte(0x78, ReadByte(0x4b)); WriteByte(0x79, ReadByte(0x4d)); }
    private void Signal() => _session.Memory.SetWramByte(0xcfc0, (byte)(_session.Memory.ReadWramByte(0xcfc0) | 2));
    private bool Tick(int offset) { WriteByte(offset, ReadByte(offset) - 1); return ReadByte(offset) == 0; }
    private void Sparkle(int subid, int duration)
    {
        var child = _session.Entities.TryAllocateInteraction(() => new SeasonsTempleSparkleRoomEntity(_session, _data,
            _source with { Id = 0x84, SubId = subid, X = (int)Npc.Position.X, Y = (int)Npc.Position.Y }, _session.Room));
        if (subid == 7 && child is not null) child.WriteByte(0x46, duration);
    }
    protected override void AdvanceNpc(RoomEntityFrame frame) { }
}
