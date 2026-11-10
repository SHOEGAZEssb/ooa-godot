namespace Oracles;

internal sealed class SeasonsRosaChaseScriptHost : NpcInteractionCommandHost
{
    private readonly SeasonsRosaChaseRoomEntity _owner;
    private readonly SeasonsOpeningSession _session;
    private readonly SeasonsTalkNpcDatabase _data;
    internal bool Completed { get; private set; }
    internal SeasonsRosaChaseScriptHost(SeasonsRosaChaseRoomEntity owner, SeasonsOpeningSession session,
        SeasonsTalkNpcDatabase data, int step) : base(owner, session.Rooms, session.Entities, session.Dialogue, data.Program(0x6c, 1, step))
    { _owner = owner; _session = session; _data = data; }
    internal void BeginCaught() { Completed = false; ReplaceObjectScript(_data.Program(0x6c, 1, 5)); }
    public override void ScriptEnded() => Completed = true;
    public override void OrRoomFlag(int flag) => _session.Save.SetRoomFlag(_session.Rooms.ActiveGroup, _session.RoomId, checked((byte)flag));
    public override int ReadMemory(string binding) => binding == "random-bit" ? _session.Random.Next().Value & 1 : base.ReadMemory(binding);
    public override void PlaySound(int sound) => _session.Sound.PlaySound(sound);
    public override void SetMusic(int music)
    {
        if (music == 0xff) music = _session.Sound.NativeRoomMusic;
        _session.Sound.SetNativeActiveMusic(music); _session.Sound.PlaySound(music);
    }
    public override void SetActorMovementAnimation(string actor, int angle, string encodedAnimation)
    { _owner.SetAnimation(angle >> 3); }
    public override void RunNativeHandler(string handler)
    {
        if (handler == "ClearCfc0Bit0")
        { _session.Memory.SetWramByte(0xcfc0, (byte)(_session.Memory.ReadWramByte(0xcfc0) ^ 1)); return; }
        string[] fields = handler.Split(' ', 2);
        if (fields[0] == "setsubstate") { _owner.WriteByte(0x45, Hex(fields[1])); return; }
        if (fields[0] == "setangleandanimation")
        {
            int angle = Hex(fields[1]); _owner.WriteByte(0x49, angle);
            _owner.SetAnimation(angle >> 3); return;
        }
        if (fields[0] == "settilehere")
        {
            _session.Room.SetPositionTileAndCollision(_owner.Npc.Position, (byte)Hex(fields[1]), null, (long)_session.AnimationTicks); return;
        }
        if (fields[0] == "asm15")
        {
            switch (fields[1])
            {
                case "scriptHelp.subrosianHiding_store02Intocc9e": _session.Memory.SetField("wcc84", 2); return;
                case "scriptHelp.subrosianHiding_createDetectionHelper": _session.Entities.TryCreateInteractionDetector(_owner); return;
                case "scriptHelp.rosaHiding_hidingFinishedSetInitialRoomsFlags":
                    _session.Save.SetRoomFlag(0, 0xcb, 0x80);
                    _session.Memory.SetField("wcc84", 0); _session.Memory.SetField("wcc85", 0); return;
            }
        }
        throw UnsupportedCommand(handler);
    }
    private static int Hex(string text) => Convert.ToInt32(text.TrimStart('$'), 16);
}
