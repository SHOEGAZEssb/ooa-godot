using BitOperations = System.Numerics.BitOperations;

namespace Oracles;

internal sealed class SeasonsTempleScriptHost : NpcInteractionCommandHost
{
    private readonly NativeNpcRoomEntity _owner;
    private readonly SeasonsOpeningSession _session;
    private readonly SeasonsTalkNpcDatabase _npcs;
    private readonly RodOfSeasonsDatabase _data;
    private GroundTreasurePickup? _reward;
    private bool _palette;
    internal bool Completed { get; private set; }
    internal SeasonsTempleScriptHost(NativeNpcRoomEntity owner, SeasonsOpeningSession session, SeasonsTalkNpcDatabase npcs,
        RodOfSeasonsDatabase data, string program) : base(owner, session.Rooms, session.Entities, session.Dialogue, data.Program(program))
    { _owner = owner; _session = session; _npcs = npcs; _data = data; }
    internal void AdvancePaletteBeforeObjects()
    {
        if (!_palette) return;
        if (!_session.Transitions.NativePaletteFadeActive) { _palette = false; return; }
        _session.Transitions.AdvanceNativeWhiteFade();
    }
    internal void CancelPalette()
    {
        if (_palette) _session.Transitions.CancelNativeWhiteFade(clearPresentation: true);
        _palette = false;
    }
    internal void Darken(bool darken)
    {
        if (darken) _session.Transitions.BeginNativeRoomDarkening(-16);
        else _session.Transitions.BeginNativeRoomBrightening();
        _palette = true;
    }
    public override void ScriptEnded() => Completed = true;
    public override bool MemoryEquals(string binding, int value) => ReadMemory(binding) == value;
    public override int ReadMemory(string binding)
    {
        if (binding == "w1Link.state") return ScriptPlayer.NativeNormalStateForInteraction ? 1 : 0;
        if (binding.StartsWith("cfc0bit", StringComparison.Ordinal)) return (_session.Memory.ReadWramByte(0xcfc0) >> int.Parse(binding[7..])) & 1;
        if (binding.StartsWith("flag:", StringComparison.Ordinal)) return _session.Save.HasGlobalFlag(Hex(binding[5..])) ? 1 : 0;
        if (binding.StartsWith("treasure:", StringComparison.Ordinal)) return _session.Inventory.HasTreasure(Hex(binding[9..])) ? 1 : 0;
        if (binding.StartsWith('$'))
        {
            int address = Hex(binding);
            return address < 0x80 ? _owner.ReadByte(address) : _session.Memory.ReadWramByte(address);
        }
        return _session.Memory.ReadField(binding);
    }
    public override void WriteMemory(string binding, int value)
    {
        if (binding.StartsWith('$')) _session.Memory.SetWramByte(Hex(binding), unchecked((byte)value));
        else _session.Memory.SetField(binding, unchecked((byte)value));
    }
    public override bool GateOpen(string gate)
    {
        if (gate == "PaletteDone") return !_session.Transitions.NativePaletteFadeActive;
        if (gate == "TouchGround")
        {
            int dy = unchecked((byte)((int)ScriptPlayer.Position.Y - (int)ScriptActor.Position.Y + _owner.ReadByte(0x66) + 6));
            int dx = unchecked((byte)((int)ScriptPlayer.Position.X - (int)ScriptActor.Position.X + _owner.ReadByte(0x67) + 6));
            bool touching = _session.Memory.ReadField("wLinkInAir") == 0 && ScriptPlayer.EnemyContactZ == 0 && !ScriptPlayer.IsDying &&
                dy < (_owner.ReadByte(0x66) + 6) * 2 && dx < (_owner.ReadByte(0x67) + 6) * 2;
            if (touching) ScriptPlayer.ApplyInteractionInvincibility(0x80);
            return touching;
        }
        if (gate.StartsWith("flagset ", StringComparison.Ordinal))
        {
            string[] fields = gate[8..].Split(',', StringSplitOptions.TrimEntries);
            return (_session.Memory.ReadWramByte(Hex(fields[1])) & (1 << Hex(fields[0]))) != 0;
        }
        throw UnsupportedCommand(gate);
    }
    public override void OrRoomFlag(int flag) => _session.Save.SetRoomFlag(_session.Rooms.ActiveGroup, _session.RoomId, (byte)flag);
    public override void PlaySound(int sound) => _session.Sound.PlaySound(sound);
    public override void SetMusic(int music)
    {
        int selected = music == 0xff ? _session.Sound.NativeRoomMusic : music;
        _session.Sound.SetNativeActiveMusic(selected); _session.Sound.PlayNativeSound(selected);
    }
    public override void GiveItem(int treasureId, int parameter)
    {
        if (treasureId != TreasureId.RodOfSeasons || parameter is < 2 or > 5) throw UnsupportedCommand($"giveitem ${treasureId:x2}:${parameter:x2}");
        var item = _session.Entities.Treasures.GetObject(treasureId, parameter);
        _reward = _session.Entities.GrantGroundTreasure(new(_session.Rooms.ActiveGroup, _session.RoomId, 0,
            (int)ScriptPlayer.Position.Y, (int)ScriptPlayer.Position.X, item.Name, $"seasonsSpiritsScript_imbueSeason ${parameter:x2}")
        {
            SpawnMode = TreasureSpawnMode.Instant, GrabMode = TreasureGrabMode.TwoHands,
            DialogueTiming = GroundTreasureDialogueTiming.AfterGrab, CompletionOwner = GroundTreasureCompletionOwner.Caller,
            RoomFlagTiming = GroundTreasureRoomFlagTiming.Never, ExpectedTreasureId = treasureId, ExpectedSubId = parameter
        }, ScriptPlayer);
    }
    protected override void BeforeAdvanceFrame()
    { if (_reward is not null && !DialogueOpen) { _reward.Finish(ScriptPlayer); _reward = null; } base.BeforeAdvanceFrame(); }
    public override void RunNativeHandler(string handler)
    {
        string[] fields = handler.Split(' ', 2);
        string[] args = fields.Length > 1 ? fields[1].Split(',', StringSplitOptions.TrimEntries) : [];
        switch (fields[0])
        {
            case "StopIfRoomFlag40": if (RoomFlagSet(0x40)) { _owner.Delete(); Completed = true; } return;
            case "XorSignal": _session.Memory.SetWramByte(0xcfc0, (byte)(_session.Memory.ReadWramByte(0xcfc0) ^ (1 << Hex(args[0])))); return;
            case "setcounter1": _owner.WriteByte(0x46, Hex(args[0])); return;
            case "createpuff": _session.Entities.TryCreatePuzzlePuff(ScriptActor.Position); return;
            case "settilehere": _session.Rooms.TrySetTile((byte)_session.Room.GetPackedPosition(ScriptActor.Position), (byte)Hex(args[0])); return;
            case "spawninteraction":
                if (args[0] != "INTERAC_GET_ROD_OF_SEASONS" || Hex(args[1]) != 2) throw UnsupportedCommand(handler);
                SpawnRod(2, 0, new(Hex(args[3]), Hex(args[2]))); return;
            case "asm15":
                switch (args[0])
                {
                    case "fadeoutToWhite": _session.Transitions.BeginNativeWhiteFade(true); _palette = true; return;
                    case "fadeinFromWhiteWithDelay": _session.Transitions.BeginNativeWhiteFade(false, Hex(args[1])); _palette = true; return;
                    case "seasonsInteractionsBank15.forceLinksDirection":
                        ScriptPlayer.Face(Vector2I.Down); _session.Memory.SetField("wLinkForceState", 8); _session.Memory.SetField("wcc50", 0x80); return;
                    case "seasonsInteractionsBank15.spawnRodOfSeasonsSparkles":
                        Vector2[] positions = [new(24,120),new(24,8),new(136,8),new(136,120)];
                        for (int i = 0; i < 4; i++) if (SpawnRod(1, i, positions[i]) is null) break;
                        return;
                    case "scriptHelp.spawnSeasonsSpiritSubId00": SpawnSpirit(0); return;
                    case "scriptHelp.spawnSeasonsSpiritSubId01": SpawnSpirit(1); return;
                    case "scriptHelp.seasonsSpirit_createSwirl":
                        int page = _owner.ReadByte(0x57) << 8;
                        _session.Entities.CreateEnergySwirl(new(_session.Memory.ReadWramByte(page | 0x4d), _session.Memory.ReadWramByte(page | 0x4b)), 0x6e); return;
                    case "scriptHelp.seasonsSpirits_checkPostSeasonGetText":
                        _owner.WriteByte(0x7f, BitOperations.PopCount((uint)(_session.Inventory.ObtainedSeasons & 15)) == 4 ? _session.RoomId == 0xf5 ? 1 : 2 : 0); return;
                }
                break;
        }
        throw UnsupportedCommand(handler);
    }
    private SeasonsRodAcquisitionRoomEntity? SpawnRod(int subid, int variable, Vector2 position) =>
        _session.Entities.TryAllocateInteraction(() => new SeasonsRodAcquisitionRoomEntity(_session, _npcs, _data,
            Child(0xe6, subid, variable, position), _session.Room));
    private void SpawnSpirit(int variant)
    {
        var spirit = _session.Entities.TryAllocateInteraction(() => new SeasonsSpiritRoomEntity(_session, _npcs,
            Child(0x50, _owner.ReadByte(0x43), variant, new(112,24)), _session.Room, _data));
        if (spirit is not null) _owner.WriteByte(0x57, 0xd0 + _session.Entities.NativeInteractionSlot(spirit));
    }
    private static int Hex(string value) => Convert.ToInt32(value.StartsWith("DIR_", StringComparison.Ordinal) ? "02" : value.TrimStart('$'), 16);
    private NativeObjectInstruction Child(int id, int subid, int variable, Vector2 position) =>
        new(0, 0, 2, false, 0, id, subid, (int)position.Y, (int)position.X, 0, variable, 0, 0,
            $"Seasons temple INTERAC${_owner.Npc.Record.Id:x2}: spawned ${id:x2}:${subid:x2}");
    protected override void ResetHostState() { _reward = null; base.ResetHostState(); }
}
