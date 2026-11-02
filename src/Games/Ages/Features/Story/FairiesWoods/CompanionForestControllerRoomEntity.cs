
namespace Oracles;

// Placed $71:$08-$0c owns its script and its slot, including the $0a flute graphic.
internal sealed class CompanionForestControllerRoomEntity : RoomCutsceneCommandHost,
    INativeInteractionRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    private readonly ForestNativeObjectRecord _record;
    private readonly CompanionForestDatabase _data;
    private readonly FairiesWoodsDatabase _fairies;
    private readonly OracleSaveData _save;
    private readonly OracleRuntimeState _memory;
    private readonly Func<CompanionForestEvent?> _ownerSource;
    private readonly CutsceneCommandRunner _runner;
    private int _companionId, _choice, _companionDescription = 0x1124;
    private readonly NpcCharacter? _flute;
    private int _role => _record.SubId;
    private OracleSaveData Save => _save;
    private bool Flag(int flag) => Save.HasGlobalFlag(flag);
    private CompanionForestEvent Owner => _ownerSource() ??
        throw new NotSupportedException($"{_record.Source}: active $71:${_role:x2} requires its story binding.");
    public override RoomEventContext Context => Owner.Context;
    public Node2D? PresentationNode => _flute;
    public bool Finished { get; private set; }
    public bool NativeInitializationPending => State == 0 && !Finished;
    public bool BlocksGameplay => InputControlHeld;
    public bool UpdatesDuringDialogue => State == 0;
    public bool UpdatesDuringRoomEntityFreeze => State == 0;
    internal int State { get; private set; }
    internal int Counter => _runner.NativeCounter1;
    internal int Instruction => _runner.Instruction;
    internal int Signal => _memory.ReadWramByte(0xcfd2);

    internal CompanionForestControllerRoomEntity(ForestNativeObjectRecord record, CompanionForestDatabase data,
        FairiesWoodsDatabase fairies, OracleSaveData save, OracleRuntimeState memory, Func<CompanionForestEvent?> owner)
    {
        _record = record; _data = data; _fairies = fairies; _save = save; _memory = memory; _ownerSource = owner;
        _runner = new(this);
        if (record.SubId == 0x0a)
        {
            int companion = save.ReadField("wAnimalCompanion");
            _flute = NpcCharacter.CreateFromRecord(data.FluteRecord(record.Group, record.Room, Vector2.Zero,
                companion is >= 0x0b and <= 0x0d ? companion : 0x0c), AgesGameContent.AssetRoot);
            _flute.Name = "ForestController_71_0a"; _flute.SetAnimationRate(0); _flute.SetScriptVisible(false);
        }
    }

    public void SetTransitionDrawOffset(Vector2 offset) => _flute?.SetTransitionDrawOffset(offset);
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (State == 0 && !Finished) Initialize();
        return _flute?.Visible == true ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }

    private void Initialize()
    {
        if (_role == 0x0c)
        {
            byte value = Save.ReadWramByte(AgesWramAddress.wDimitriState);
            if ((value & 0x20) != 0) Save.WriteWramByte(AgesWramAddress.wDimitriState, (byte)(value | 0x40));
            Finished = true; return;
        }
        if (_role == 8)
        {
            for (int address = 0xcfd0; address < 0xcfe0; address++) _memory.SetWramByte(address, 0);
            Save.SetGlobalFlag(AgesGlobalFlag.CanBuyFlute, false);
            if (Save.ReadField("wAnimalCompanion") == 0)
            {
                Context.Inventory.AssignAnimalCompanion(0x0d);
            }
            _companionDescription = 0x1123 + Save.ReadField("wAnimalCompanion") - 0x0b;
            _memory.SetField("wTextSubstitutions", (byte)_companionDescription);
            if (_memory.ReadField("wScreenTransitionDirection") != ObjectDirection.Left || !Flag(0x22) ||
                Save.HasRoomFlag(_record.Group, _record.Room, 0x40)) { Finished = true; return; }
        }
        else if (_role == 9)
        {
            State = 1; _memory.SetWramByte(0xcfd2, 0);
            if (!Flag(0x22) || Flag(0x23)) { Finished = true; return; }
        }
        else if (_role == 10)
        {
            if (!Flag(0x24) || Flag(0x23)) { Finished = true; return; }
        }
        else if (_role == 11)
        {
            if (_memory.ReadField("wScreenTransitionDirection") != ObjectDirection.Down || !Flag(0x42) || Flag(0x23))
            { Finished = true; return; }
        }
        else throw new NotSupportedException($"{_record.Source}: unsupported $71:${_role:x2}.");

        Owner.RegisterController(this);
        _companionId = Save.ReadField("wAnimalCompanion");
        State = 1;
        if (_role == 8) return;
        if (_role == 10)
        {
            HoldNativeControl(1); _memory.SetWramByte(0xcfd2, 1);
            Context.Player.Face(Vector2I.Up);
            for (int preset = 0x11; preset <= 0x13; preset++) SpawnFairy(4, preset);
        }
        else if (_role == 11)
        {
            SpawnFairy(3, 0x14); HoldNativeControl(0x81); _memory.SetWramByte(0xcfd2, 0);
        }
        _runner.Start(_data.Commands(_role));
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        if (State == 0) { Initialize(); return; }
        if (_role == 8 && State == 1)
        {
            if (Context.Player.Position.X >= 0x50) return;
            HoldNativeControl(0x81); Context.Player.PutOnGroundForScript(); SpawnFairy(3, 0x0f);
            _runner.Start(_data.Commands(8)); State = 2; return;
        }
        if (_role == 10 && State == 2)
        {
            State = 3; RunNativeHandler("GiveFluteNow"); _runner.AdvanceFrame(); return;
        }
        if (_role == 10 && State == 3)
        {
            if (Context.ScriptDialogueOpen) return;
            WriteMountMask(); _flute!.SetScriptVisible(false);
        }
        _runner.AdvanceFrame();
    }

    private void HoldNativeControl(byte value)
    { SetNativeMenuDisabled(value); SetDisabledObjects(value); }
    private void WriteMountMask()
    {
        int doubled = (_memory.ReadField("wLinkObjectIndex") & 15) * 2;
        SetDisabledObjects(((doubled << 4) | (doubled >> 4)) & 255);
    }
    public override bool MemoryEquals(string binding, int value) => binding switch
    {
        "wTmpcfc0.fairyHideAndSeek.cfd2" => Signal == value,
        "w1Companion.var3d" => _memory.ReadWramByte(0xd13d) == value,
        "wLinkObjectIndex" => _memory.ReadField("wLinkObjectIndex") == value,
        _ => throw UnsupportedCommand($"compare forest binding '{binding}'")
    };
    public override void WriteMemory(string binding, int value)
    {
        int address = binding switch
        {
            "wTmpcfc0.fairyHideAndSeek.cfd2" => 0xcfd2,
            "w1Companion.var03" => 0xd103,
            _ => throw UnsupportedCommand($"write forest binding '{binding}'")
        };
        _memory.SetWramByte(address, checked((byte)value));
    }
    public override bool TextOptionEquals(int value)
    {
        if (Context.TryTakeDialogueChoice(out int choice)) _choice = choice;
        return _choice == value;
    }
    public override void ShowText(int textId, string message)
    {
        if (textId is 0x1131 or 0x1132)
        {
            var branch = _data.Companion(_companionId);
            textId = textId == 0x1131
                ? _role == 9 ? branch.RescueFirst : IsLinkedGame ? branch.RewardLinked : branch.RewardUnlinked
                : _role == 9 ? IsLinkedGame ? branch.RescueLinked : branch.RescueUnlinked : branch.RewardAfter;
        }
        message = _data.Text(textId).Replace("\\call(0xff)", _data.Text(_role == 8 ? _companionDescription : 0x1124));
        if (textId == 0x1121) Context.ShowChoiceDialogue(message, sourceTextId: textId);
        else Context.ShowDialogue(message, sourceTextId: textId);
    }
    public override void OrRoomFlag(int flag) => Save.SetRoomFlag(Context.Rooms.ActiveGroup, Context.Rooms.CurrentRoom.Id, (byte)flag);
    public override void RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "NoticeLink":
                int animation = _data.Companion(_companionId).NoticeAnimation;
                if (animation != 0) _memory.SetWramByte(0xd13f, (byte)animation);
                Vector2 point = new(_record.X, _record.Y - 16);
                if (Context.Entities.TryCreateInteraction(new ExclamationMarkSpawn(point, Duration: 30,
                    Record: _data.ExclamationRecord(_record.Room, point))) is { } mark) Owner.RegisterMarker(mark);
                break;
            case "SpawnRescueFairy": SpawnFairy(3, 0x0f); break;
            case "ScrambleForest": Save.SetGlobalFlag(AgesGlobalFlag.ForestUnscrambled, false); break;
            case "GiveFlute":
                State = 2;
                break;
            case "GiveFluteNow":
                ShowText((Context.Inventory.HasTreasure(TreasureId.Flute) ? 0x0069 : 0x0038) + _companionId - 0x0b, string.Empty);
                // companionScript_subid0a_state2 writes $01 before the flute grant.
                SetNativeMenuDisabled(0x01);
                Save.WriteWramByte(AgesWramAddress.wFluteIcon, (byte)(_companionId - 0x0a));
                int stateAddress = 0xc646 + _companionId - 0x0b;
                Save.WriteWramByte(stateAddress, (byte)(Save.ReadWramByte(stateAddress) | 0x80));
                Context.Inventory.GiveTreasure(TreasureId.Flute, 1);
                Context.Sound.PlaySound(SoundId.SndGetItem);
                _flute!.Name = "CompanionFluteReward";
                // objectTakePositionWithOffset copies YH/XH, leaving this new graphic's subpixels zero.
                _flute.Position = OracleObjectMath.ToPixelPosition(Context.Player.PrecisePosition) + new Vector2(0, -14);
                _flute.SetBasePalette(((_companionId - 0x0a) & 1) * 2 ^ (_companionId - 0x0a));
                _flute.SetFixedDrawPriority(ObjectDrawPriority.FixedHighPriorityZIndex); // objectSetVisible80.
                _flute.SetScriptVisible(true);
                _memory.SetField("wLinkForceState", 4);
                _memory.SetField("wcc50", 1);
                break;
            case "WarpOut":
                Context.Entities.OnRoomWarpRequested(_data.ReturnWarp);
                break;
            default: throw UnsupportedCommand($"run forest helper '{handler}'");
        }
    }

    public override void ScriptEnded()
    {
        if (_role == 10) { SetNativeMenuDisabled(0); SetDisabledObjects(0); }
        else _memory.SetField("wStatusBarNeedsRefresh", (byte)(_memory.ReadField("wStatusBarNeedsRefresh") | 2));
        RelinquishInputControl();
        Finished = true;
    }
    internal void Cancel()
    {
        if (Finished) return;
        _runner.Clear(); Finished = true;
        if (GodotObject.IsInstanceValid(_flute)) _flute!.SetActive(false);
        ReleaseInputControl();
    }
    private void SpawnFairy(int subId, int preset)
    {
        var flight = ForestFairyFlight.TryCreate(Context, _fairies, preset, subId, $"CompanionFairy{subId:x2}_{preset:x2}");
        if (flight is not null) Owner.RegisterFlight(flight);
        else Context.Entities.WriteSoundAllocationAlias(0xc043, (byte)preset); // Failed HL=$e040; LD L,var03 aliases sound RAM.
    }
}
