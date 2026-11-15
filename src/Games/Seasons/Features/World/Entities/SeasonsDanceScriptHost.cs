namespace Oracles;

internal sealed class SeasonsDanceScriptHost : NpcInteractionCommandHost
{
    private readonly SeasonsDanceRoomEntity _owner;
    private readonly SeasonsOpeningSession _session;
    private readonly SeasonsTalkNpcDatabase _data;
    internal SeasonsDanceScriptHost(SeasonsDanceRoomEntity owner, SeasonsOpeningSession session,
        SeasonsTalkNpcDatabase data, string program) : base(owner, session.Rooms, session.Entities, session.Dialogue, data.Program(program))
    { _owner = owner; _session = session; _data = data; }
    internal void Replace(string program) => ReplaceObjectScript(_data.Program(program));
    public override void ScriptEnded() { }
    public override void OrRoomFlag(int flag) => _session.Save.SetRoomFlag(_session.Rooms.ActiveGroup, _session.RoomId, (byte)flag);
    public override void PlaySound(int sound) => _session.Sound.PlayNativeSound(sound);
    public override void SetMusic(int music)
    {
        if (music == 0xff) music = _session.Sound.NativeRoomMusic;
        _session.Sound.SetNativeActiveMusic(music); _session.Sound.PlayNativeSound(music);
    }
    public override bool TextOptionEquals(int value)
    { Dialogue.TryTakeChoiceResult(out _); return Dialogue.SelectedChoice == value; }
    public override bool MemoryEquals(string binding, int value) => ReadMemory(binding) == value;
    public override int ReadMemory(string binding) => binding.StartsWith('$')
        ? _session.Memory.ReadWramByte(Hex(binding)) : base.ReadMemory(binding);
    public override void WriteMemory(string binding, int value)
    {
        int address = Hex(binding);
        _session.Memory.SetWramByte(address, (byte)value);
        if (address == 0xd008) ScriptPlayer.Face(value switch { 0 => Vector2I.Up, 1 => Vector2I.Right, 2 => Vector2I.Down, _ => Vector2I.Left });
    }
    public override void InitializeActorCollisionRadii(string actor)
    { base.InitializeActorCollisionRadii(actor); ScriptActor.SetBlocksLink(true); }
    public override void SetActorMovementAnimation(string actor, int angle, string encodedAnimation) => _owner.SetAnimation(angle >> 3);
    public override void GiveItem(int treasureId, int parameter)
    {
        if (parameter != 0 || treasureId is not (TreasureId.Boomerang or TreasureId.Flute or TreasureId.GashaSeed or TreasureId.OreChunks))
            throw UnsupportedCommand($"giveitem ${treasureId:x2}:${parameter:x2}");
        if (!_session.Entities.InteractionSlotAvailable) return;
        var item = _session.Entities.Treasures.GetObject(treasureId, parameter);
        _session.Entities.SpawnGroundTreasure(new(_session.Rooms.ActiveGroup, _session.RoomId, 0,
            (int)ScriptPlayer.Position.Y, (int)ScriptPlayer.Position.X, item.Name, $"danceLeaderScript: giveitem ${treasureId:x2}:${parameter:x2}")
        { GrabMode = item.NativeFlags & 7, RoomFlagTiming = GroundTreasureRoomFlagTiming.Never,
            ExpectedTreasureId = treasureId, ExpectedSubId = parameter }).BeginScriptGrant();
        _owner.WriteByte(0x46, 3);
    }
    public override void RunNativeHandler(string handler)
    {
        var fields = handler.Split(' ', 2);
        var args = fields.Length == 1 ? [] : fields[1].Split(',', StringSplitOptions.TrimEntries);
        switch (fields[0])
        {
            case "incstate": _owner.WriteByte(0x44, _owner.ReadByte(0x44) + 1); return;
            case "setcounter1": _owner.WriteByte(0x46, Hex(args[0])); return;
            case "setsubstate": _owner.WriteByte(0x45, args[0] == "$ff" ? _owner.ReadByte(0x45) + 1 : Hex(args[0])); return;
            case "setangleandanimation":
                int angle = Hex(args[0]); _owner.WriteByte(0x49, angle); _owner.SetAnimation(angle >> 3); return;
            case "spawninteraction":
                if (args.Length != 4 || args[0] != "INTERAC_DANCE_HALL_MINIGAME" || Hex(args[1]) != 3)
                    throw UnsupportedCommand(handler);
                _session.Entities.TryAllocateInteraction(() => new SeasonsDanceRoomEntity(_session, _data,
                    new(0, 0, 2, false, 0, 0x6a, 3, Hex(args[2]), Hex(args[3]), 0, 0, 0, 0,
                        "scripts2.s:danceLeaderScript_danceTutorial"), _session.Room)); return;
            case "asm15":
                switch (args[0])
                {
                    case "fastFadeoutToWhite": _owner.Fade(true); return;
                    case "scriptHelp.seasonsFunc_15_5d20": _owner.BroadcastMove(0); return;
                    case "scriptHelp.seasonsFunc_15_5d29": _owner.BroadcastMove(1); return;
                    case "scriptHelp.seasonsFunc_15_5d32": _owner.BroadcastMove(2); return;
                    case "scriptHelp.seasonsFunc_15_5d45":
                        _owner.WriteByte(0x54, 0x80); _owner.WriteByte(0x55, 0xfe); _owner.WriteByte(0x4e, 1); return;
                }
                break;
        }
        throw UnsupportedCommand(handler);
    }
    private static int Hex(string value) => Convert.ToInt32(value.TrimStart('$'), 16);
}
