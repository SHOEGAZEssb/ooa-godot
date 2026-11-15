namespace Oracles;

// danceHallMinigame.s: each $6a actor owns one native visit; $cfc0-$cfdf are shared WRAM.
internal sealed class SeasonsDanceRoomEntity : NativeNpcRoomEntity
{
    private readonly SeasonsOpeningSession _session;
    private readonly SeasonsTalkNpcDatabase _data;
    private readonly NativeObjectInstruction _source;
    private readonly NativeScriptInputControl _control;
    private bool _palette, _linkOwned, _warpRequested;
    public override bool UpdatesDuringDialogue => NativeInitializationPending || (ReadByte(0x40) & 0x80) != 0;
    public override bool UpdatesDuringRoomEntityFreeze => UpdatesDuringDialogue;
    private int State { get => ReadByte(0x44); set => WriteByte(0x44, value); }
    private int Substate { get => ReadByte(0x45); set => WriteByte(0x45, value); }
    private int Shared(int low) => _session.Memory.ReadWramByte(0xcf00 | low);
    private void Shared(int low, int value) => _session.Memory.SetWramByte(0xcf00 | low, unchecked((byte)value));
    internal SeasonsDanceRoomEntity(SeasonsOpeningSession session, SeasonsTalkNpcDatabase data,
        NativeObjectInstruction source, OracleRoomData room)
        : base(data.Create(source, room, session.Data.World.SpritePalettes), session.Memory, session.Link,
            index => data.Animation(0x6a, index), data.Text)
    { _session = session; _data = data; _source = source; _control = new(session.Memory); Npc.SetBlocksLink(false); }

    protected override bool InitializeNpc(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        int subid = ReadByte(0x42);
        if (subid == 0)
        {
            _session.Memory.SetWramByte(0xccea, 1);
            for (int low = 0xc0; low < 0xe0; low++) Shared(low, 0);
            _session.Entities.ParseNativeObjectStream("objectData7e6c", EnemyPlacementContext.Unrestricted);
            Delete(); return false;
        }
        if (subid == 3) { WriteByte(0x40, 0x81); UpdateTutorial(); return false; }
        WriteByte(0x40, ReadByte(0x40) | 0x80); WriteByte(0x48, 2); WriteByte(0x49, 0x10);
        WriteByte(0x5a, 0xc2); Npc.SetScriptVisible(true); Npc.ZIndex = ObjectDrawPriority.BehindLinkZIndex;
        if (subid == 1) Program("dancecLeaderScript_promptToStartDancing");
        else
        {
            int text = 0x0100 | _data.NativeTable("dance-texts")[ReadByte(0x4d)];
            Npc.SetDialogue(text, _data.Text(text), true); WriteByte(0x7b, 1);
            int packed = ReadByte(0x4b); WriteByte(0x4b, (packed & 0xf0) | 8); WriteByte(0x4d, ((packed & 15) << 4) | 8);
            Program("danceLeaderScript_showLoadedText");
        }
        return false;
    }
    private void Program(string program)
    {
        if (Script is SeasonsDanceScriptHost host) host.Replace(program);
        else Script = new SeasonsDanceScriptHost(this, _session, _data, program);
    }
    protected override bool UpdateNativeNpc(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        switch (ReadByte(0x42))
        {
            case 1: UpdateLeader(frame.Player); break;
            case 2: UpdateDancer(frame.Player); break;
            case 3: UpdateTutorial(); break;
            default: throw new NotSupportedException($"{_source.Source}: INTERAC$6a:${ReadByte(0x42):x2}.");
        }
        Npc.ProjectNativePosition(); return true;
    }
    private void UpdateLeader(Player player)
    {
        switch (State)
        {
            case 1: Script!.AdvanceObjectUpdate(player); Npc.FaceLinkAndAnimateOneUpdate(player, preventPassing: true); break;
            case 2: UpdateZ(); Script!.AdvanceObjectUpdate(player); Npc.AdvanceAnimationUpdates(1); break;
            case 3:
                Npc.AdvanceAnimationUpdates(1);
                if (Substate == 0) { Substate = 1; Shared(0xda, 1); Shared(0xd3, 0x50); Program("danceLeaderScript_promptForTutorial"); }
                else if (Substate == 1) Script!.AdvanceObjectUpdate(player);
                else if (Substate == 2 && !_session.Transitions.NativePaletteFadeActive)
                {
                    State = 4; Substate = 0; _linkOwned = true;
                    player.RequestSubrosianDance(_data.NativeTable("dance-grid"), _data.NativeTable("dance-link-animations"));
                    Fade(false);
                }
                break;
            case 4: UpdateGame(); break;
            case 5: UpdatePrize(player); break;
            default: throw new NotSupportedException($"danceHallMinigame.s: leader state ${State:x2}.");
        }
    }
    private void ConfigureTempo()
    {
        var table = _data.NativeTable("dance-speed-tempo"); int index = Shared(0xd7) * 2;
        if (index + 1 >= table.Length) throw new NotSupportedException($"danceHallMinigame.s: difficulty ${Shared(0xd7):x2} exceeds table_5ced.");
        Shared(0xd3, table[index]); Shared(0xd6, table[index + 1]);
    }
    private void UpdateGame()
    {
        switch (Substate)
        {
            case 0:
                Substate = 1; int difficulty = Math.Min(8, (int)_session.Save.ReadField("wNumTimesPlayedSubrosianDance"));
                Shared(0xd7, difficulty); Shared(0xdc, difficulty); ConfigureTempo();
                Shared(0xd5, _data.NativeTable("dance-round-counts")[difficulty] + (_session.Random.Next().Value & 3));
                Shared(0xd4, 0); Shared(0xdb, 0); _session.Sound.PlayNativeSound(0xcc);
                WriteByte(0x47, 0x3c); _session.Sound.PlayNativeSound(0x22); return;
            case 1:
                if (!Tick(0x47)) return;
                WriteByte(0x47, 1); Substate = 2;
                for (int i = 0; i < 3; i++) Shared(0xc8 + i, _data.NativeTable("dance-random-moves")[_session.Random.Next().Value & 15]);
                Shared(0xcb, 0); WriteByte(0x46, Shared(0xd6)); SetAnimation(2); SetWord(0x54, -0x180);
                goto case 2;
            case 2:
                if (!AdvanceBeat()) return;
                if (Shared(0xcb) != 3) { CueMove(false); return; }
                Substate = 3; WriteByte(0x46, Shared(0xd6)); Shared(0xcb, 0); Shared(0xd9, 0);
                Shared(0xd8, 0xff); SetAnimation(2); goto case 3;
            case 3:
                if (CheckInput()) { FailDance(); return; }
                if (!AdvanceBeat() || Shared(0xd1) != 0) return;
                if (Shared(0xcb) != 3) { CueMove(true); return; }
                if (Shared(0xd9) != 3) { FailDance(); return; }
                Shared(0xd5, Shared(0xd5) - 1);
                if (Shared(0xd5) == 0)
                {
                    State = 5; Substate = 0; Shared(0xd0, 1);
                    _session.Sound.PlayNativeSound(0xfb); _session.ShowText(0x010a, _data.Text(0x010a)); return;
                }
                int rounds = Math.Min(8, Shared(0xdb)) + 1; Shared(0xdb, rounds);
                if ((rounds & 3) == 0) { Shared(0xd7, Shared(0xd7) + ((rounds >> 2) & 3)); ConfigureTempo(); }
                Substate = 1; Shared(0xcb, 0); return;
            case 4: LeaveWhenTextClosed(); return;
            default: throw new NotSupportedException($"danceHallMinigame.s: game substate ${Substate:x2}.");
        }
    }
    private bool CheckInput()
    {
        if (Shared(0xdd) != 0) return true;
        int input = Shared(0xd8), count = Shared(0xd9);
        if (input == 0xff || count == 3) return false;
        Shared(0xd9, count + 1);
        if (Shared(0xc8 + count) != input) return true;
        Shared(0xd8, 0xff); return false;
    }
    private void CueMove(bool dancers)
    {
        int move = Shared(0xc8 + Shared(0xcb)); var cues = _data.NativeTable("dance-cues");
        _session.Sound.PlayNativeSound(cues[move * 2]);
        if (dancers) BroadcastMove(move); else SetAnimation(cues[move * 2 + 1]);
        Shared(0xcb, Shared(0xcb) + 1); WriteByte(0x46, Shared(0xd6));
    }
    internal void BroadcastMove(int move)
    {
        if (move < 2) Shared(0xd2, move == 0 ? 1 : 0xff);
        Shared(0xd4, move < 2 ? 4 : 5); Shared(0xd1, 9); Shared(0xda, Shared(0xda) + 1);
    }
    private bool AdvanceBeat()
    {
        UpdateZ(); Npc.AdvanceAnimationUpdates(1);
        int parameter = Npc.CurrentAnimationParameter;
        if (parameter is not (0 or 0xff)) { Npc.ConsumeAnimationParameter(); WriteByte(0x4d, ReadByte(0x4d) + parameter); }
        if (ReadByte(0x46) == 0) return true;
        WriteByte(0x46, ReadByte(0x46) - 1); return ReadByte(0x46) == 0;
    }
    private void FailDance()
    {
        _session.ShowText(0x0104, _data.Text(0x0104)); Substate = 4; Shared(0xd0, 0xff);
        _session.Sound.PlayNativeSound(0xcc); _session.Sound.PlayNativeSound(0xfb);
    }
    private void UpdatePrize(Player player)
    {
        switch (Substate)
        {
            case 0: if (!_session.Dialogue.IsOpen) { Substate = 1; Fade(true); } return;
            case 1:
                if (_session.Transitions.NativePaletteFadeActive) return;
                player.SetDanceLinkId(0); player.SetScriptedCoordinateHigh(false, 0x30); player.SetScriptedCoordinateHigh(true, 0x48);
                player.Face(Vector2I.Down); Substate = 2; _control.SetDisabledObjects(0x81);
                _control.SetLinkCollisionsAndMenuDisabled(0x81); _session.Save.AddGashaMaturity(0x1e); Fade(false); return;
            case 2:
                if (_session.Transitions.NativePaletteFadeActive) return;
                _control.SetDisabledObjects(0x81); _control.SetMenuDisabled(0x81);
                int wins = Math.Min(0xff, _session.Save.ReadField("wNumTimesPlayedSubrosianDance") + 1);
                _session.Save.WriteField("wNumTimesPlayedSubrosianDance", (byte)wins);
                string program;
                if (wins == 1) program = "danceLeaderScript_boomerang";
                else if (wins == 6 && _session.Memory.ReadField("wIsLinkedGame") == 0 && (_session.Save.ReadField("wRickyState") & 0x20) == 0)
                    program = "danceLeaderScript_giveFlute";
                else if (wins == 9) program = TryRingPrize() ? "danceLeaderScript_itemGiven" : "danceLeaderScript_gashaSeed";
                else program = _session.Random.Next().Value < 0x60 ? "danceLeaderScript_gashaSeed" : "danceLeaderScript_giveOreChunks";
                Program(program); Substate = 3; return;
            case 3: Script!.AdvanceObjectUpdate(player); return;
            case 4: LeaveWhenTextClosed(); return;
            default: throw new NotSupportedException($"danceHallMinigame.s: prize substate ${Substate:x2}.");
        }
    }
    private bool TryRingPrize()
    {
        var rings = _data.NativeTable("dance-prize-rings"); int first = _session.Random.Next().Value & 3;
        for (int index = 0; index < 4; index++)
        {
            int ring = rings[(first + index) & 3];
            if (_session.Inventory.HasAppraisedRing(ring)) continue;
            if (_session.Entities.InteractionSlotAvailable)
            {
                var item = _session.Entities.Treasures.GetObject(TreasureId.Ring, 0);
                _session.Entities.SpawnGroundTreasure(new(_session.Rooms.ActiveGroup, _session.RoomId, 0,
                    (int)_session.Link.Position.Y, (int)_session.Link.Position.X, item.Name, "danceHallMinigame.s: giveRingToLink")
                { GrabMode = item.NativeFlags & 7, RoomFlagTiming = GroundTreasureRoomFlagTiming.Never,
                    InventoryWrite = GroundTreasureInventoryWrite.UnappraisedRing, InventoryParameter = ring });
            }
            return true;
        }
        return false;
    }
    private void LeaveWhenTextClosed()
    {
        if (_session.Dialogue.IsOpen || _warpRequested) return;
        var warp = _data.NativeTable("dance-exit");
        _session.Transitions.RequestNativeFadeOutWarp(new(_session.Rooms.ActiveGroup, _session.RoomId, 0, 0, 0,
            warp[0] & 7, warp[1], warp[3], warp[2], warp[4], DirectFadeOut: true, NativeSource: "danceHallMinigame.s:warpDestVariables"));
        _warpRequested = true;
    }
    private void UpdateDancer(Player player)
    {
        switch (State)
        {
            case 1:
                if (Shared(0xda) == 0) { Script!.AdvanceObjectUpdate(player); Npc.FaceLinkAndAnimateOneUpdate(player, preventPassing: true); }
                else { State = 2; SetAnimation(2); }
                return;
            case 2:
                if (Collided(player)) { StopDancer(); return; }
                Npc.AdvanceAnimationUpdates(1);
                if (Shared(0xd0) != 0) { StopDancer(); return; }
                if (ReadByte(0x7b) == Shared(0xda)) return;
                WriteByte(0x7b, Shared(0xda)); State = Shared(0xd4);
                if (State == 4)
                {
                    int packed = (ReadByte(0x4b) & 0xf0) | (ReadByte(0x4d) >> 4);
                    var grid = _data.NativeTable("dance-grid"); int index;
                    for (index = 0; index < grid.Length && grid[index] != packed; index += 2) { }
                    if (index >= grid.Length) throw new NotSupportedException($"danceHallMinigame.s: dancer off grid at ${packed:x2}.");
                    int direction = (Shared(0xd2) < 0x80 ? grid[index + 1] : grid[index + 1] >> 4) & 3;
                    WriteByte(0x48, direction); WriteByte(0x49, direction * 8); SetAnimation(direction);
                }
                WriteByte(0x78, 0); return;
            case 3: State = 2; SetAnimation(2); CompleteMove(); return;
            case 4:
                if (!MoveDancer()) return;
                State = 2; CompleteMove(); return;
            case 5: State = 2; SetAnimation(4); CompleteMove(); return;
            case 6: Npc.AdvanceAnimationUpdates(1); return;
            default: throw new NotSupportedException($"danceHallMinigame.s: dancer state ${State:x2}.");
        }
    }
    private bool Collided(Player player)
    {
        bool collision = unchecked((byte)(player.EnemyContactZ - ReadByte(0x4f) + 7)) < 14 &&
            unchecked((byte)((int)player.Position.X - ReadByte(0x4d) + ReadByte(0x67) + 6)) < (ReadByte(0x67) + 6) * 2 &&
            unchecked((byte)((int)player.Position.Y - ReadByte(0x4b) + ReadByte(0x66) + 6)) < (ReadByte(0x66) + 6) * 2;
        if (collision) Shared(0xdd, 1);
        return collision;
    }
    private void StopDancer() { SetAnimation(2); State = 6; }
    private void CompleteMove() { if (Shared(0xd1) != 0) Shared(0xd1, Shared(0xd1) - 1); }
    private bool MoveDancer()
    {
        int y = ReadByte(0x4b), x = ReadByte(0x4d); WriteByte(0x79, y); WriteByte(0x7a, x);
        WriteByte(0x50, Shared(0xd3)); Move(ReadByte(0x50), ReadByte(0x49));
        foreach (int offset in new[] { 0x4b, 0x4d })
        {
            int position = ReadByte(offset);
            if (position is >= 0x18 and <= 0x68) continue;
            WriteByte(offset, Math.Clamp(position, 0x18, 0x68));
            int direction = (ReadByte(0x48) + Shared(0xd2)) & 3;
            WriteByte(0x48, direction); WriteByte(0x49, direction * 8); SetAnimation(direction);
        }
        int distance = Math.Abs(y - ReadByte(0x4b)) + Math.Abs(x - ReadByte(0x4d));
        WriteByte(0x78, ReadByte(0x78) + distance);
        if (ReadByte(0x78) < 16) return false;
        SetWord(0x4a, ((ReadByte(0x4b) & 0xf0) | 8) << 8); SetWord(0x4c, ((ReadByte(0x4d) & 0xf0) | 8) << 8);
        return true;
    }
    private void UpdateTutorial()
    {
        int pose = Shared(0xdf);
        Npc.SetScriptVisible(pose != 0);
        if (pose == 0) return;
        WriteByte(0x5a, 0x80);
        if (pose == 0xff) { Delete(); return; }
        var poses = _data.NativeTable("dance-tutorial-poses"); int index = pose * 3;
        if (index + 2 >= poses.Length) throw new NotSupportedException($"danceHallMinigame.s: tutorial pose ${pose:x2}.");
        WriteByte(0x4b, poses[index]); WriteByte(0x4d, poses[index + 1]); SetAnimation(poses[index + 2]);
    }
    private bool Tick(int offset) { WriteByte(offset, ReadByte(offset) - 1); return ReadByte(offset) == 0; }
    private void SetWord(int offset, int value) { WriteByte(offset, value); WriteByte(offset + 1, value >> 8); }
    private void UpdateZ()
    {
        int z = unchecked((short)Npc.NativeState!.Word(0x4e)), speed = unchecked((short)Npc.NativeState.Word(0x54));
        OracleObjectMath.UpdateSpeedZ(ref z, ref speed, 0x28); SetWord(0x4e, z); SetWord(0x54, speed);
        Npc.SetScriptDrawOffset(new(0, z >> 8));
    }
    internal void Fade(bool fadeOut) { _session.Transitions.BeginNativeWhiteFade(fadeOut, speed: 3); _palette = true; }
    internal void AdvancePaletteBeforeObjects()
    {
        if (!_palette) return;
        if (_session.Transitions.NativePaletteFadeActive) _session.Transitions.AdvanceNativeWhiteFade(); else _palette = false;
    }
    public override void ReleaseNativeInteractionSlot()
    {
        if (_palette) _session.Transitions.CancelNativeWhiteFade(clearPresentation: true);
        if (_linkOwned) _session.Link.CancelSubrosianDance();
        _control.Cancel(); base.ReleaseNativeInteractionSlot();
    }
}
