
namespace Oracles;

/// <summary>
/// Owns INTERAC_COMPANION_SPAWNER $67:$02 and
/// INTERAC_COMPANION_SCRIPTS $71:$03 in room $0:$6a. Its script and the native
/// D1 actor retain separate interaction and special-object dispatch phases.
/// </summary>
internal sealed class RickyGlovesEvent :
    RoomCutsceneCommandHost,
    IRoomEvent,
    ICutsceneCommandHost
{
    private const string Ricky = "Ricky";

    private readonly RickyGlovesEventDatabase _database = new();
    private readonly RickyGlovesEventRecord _record;
    private NpcCharacter? _placedActor;
    private ScriptedCompanionRoomEntity? _scriptedCompanion;
    private RickyGlovesControllerRoomEntity? _controller;
    private readonly RoomEventContext _context;
    private readonly CutsceneCommandRunner _runner;

    internal RickyGlovesEvent(RoomEventContext context)
    {
        _context = context;
        _runner = new(this);
        _record = _database.Record;
    }

    internal RickyGlovesEventDatabase Database => _database;
    internal NpcCharacter? RickyActor => _placedActor;
    public override RoomEventContext Context => _context;
    public bool HasState => _runner.Active || _scriptedCompanion is not null;
    public bool BlocksGameplay => InputControlHeld;
    public bool RequiresRoomEventUpdate => false;
    internal bool ButtonSensitive => _placedActor?.ScriptButtonSensitive == true;
    internal int CurrentCommandIndex => _runner.CurrentCommand?.Source.CommandIndex ?? -1;
    public bool MenusDisabled => HasState && Context.Entities.RuntimeState.ReadField("wMenuDisabled") != 0;

    internal bool InitializeController(RickyGlovesControllerRoomEntity controller)
    {
        _controller = controller;
        if (Context.Player.IsDying) { SetDisabledObjects(0); return false; }
        if ((Context.Rooms.SaveData.ReadWramByte(_record.RickyStateAddress) & 0x20) != 0) return false;
        _runner.Start(_database.Commands);
        return true;
    }

    internal bool AdvanceController(RickyGlovesControllerRoomEntity controller)
    {
        if (!ReferenceEquals(controller, _controller)) return false;
        if (Context.Player.IsDying) { SetDisabledObjects(0); _runner.Clear(); return false; }
        _runner.AdvanceFrame();
        if (_runner.Active) return true;
        OracleRuntimeState memory = Context.Entities.RuntimeState;
        memory.SetField("wStatusBarNeedsRefresh", (byte)(memory.ReadField("wStatusBarNeedsRefresh") | 2));
        return false;
    }

    internal void SpawnPreset()
    {
        if (!Context.Entities.SpecialObjectSlotAvailable ||
            !_database.ShouldSpawnPreset(Context.Rooms.ActiveGroup, Context.Rooms.CurrentRoom.Id, Context.Rooms.SaveData)) return;

        // The source spawner clears only wRememberedCompanionId after
        // installing Ricky's fixed preset.
        CompanionRuntimeState.InstallPreset(Context.Entities.RuntimeState, Context.Rooms.SaveData, new(_record.RickyX, _record.RickyY));
        if ((Context.Rooms.SaveData.ReadWramByte(_record.RickyStateAddress) & 0xe0) != 0)
        {
            Context.Entities.Spawn<RickyCompanionRoomEntity>(new RickyCompanionSpawn(
                new(_record.RickyX, _record.RickyY), ObjectDirection.Down, _record.Group, _record.Room));
            return;
        }
        _placedActor = new NpcCharacter { Name = Ricky, ZIndex = ObjectDrawPriority.BehindLinkZIndex };
        _placedActor.Initialize(_database.CreateActorRecord(), Context.Entities.AssetRoot);
        // Ricky is a special object, not a fixed 16x16 interaction. Preserve
        // the signed OAM cell positions above and beside his object origin.
        _placedActor.SetScriptAnimation(
            _database.Visual.Animations[_record.InitialAnimation],
            _database.Visual.SourceOffsets[
                _record.InitialAnimation]);
        _placedActor.SetActive(false);
        _scriptedCompanion = new RickyScriptedCompanionRoomEntity(_placedActor,
            Context.Entities.RuntimeState, Context.Rooms.SaveData, () => Context.Rooms.CurrentRoom, _database.Visual,
            _record.JumpSpeedZ, _record.JumpGravity, ContinueWithOrdinaryCompanion, SetNativeMenuDisabled);
        Context.Entities.AddEntity(_scriptedCompanion);
    }

    public void UpdateFrame() => throw new InvalidOperationException("Ricky's glove script advances only from its native interaction slot.");

    private void ContinueWithOrdinaryCompanion(ScriptedCompanionRoomEntity body, Player player, bool mounting)
    {
        body.CompleteNativeUpdate();
        int frame = body.Npc.CurrentAnimationFrame, counter = body.Npc.CurrentAnimationCounter;
        body.Npc.SetScriptButtonSensitive(false); body.Npc.SetActive(false);
        var next = (RickyCompanionRoomEntity)Context.Entities.ReplaceSpecialObjectPresentation(body,
            new RickyCompanionSpawn(body.NativeSpecialObjectPosition, ObjectDirection.Down,
                _record.Group, _record.Room, ForceMount: mounting));
        next.ContinueNativePresentation(player, frame, counter, mounting);
        _placedActor = null; _scriptedCompanion = null;
    }

    public override bool MemoryEquals(string binding, int value) =>
        binding switch
        {
            "RickyButton" => CompanionRuntimeState.ReadScriptButton(Context.Entities.RuntimeState) == value,
            "RickyTalked" =>
                ((Context.Rooms.SaveData.ReadWramByte(
                    _record.RickyStateAddress) & _record.TalkedMask) != 0
                        ? 1
                        : 0) == value,
            "AnimalCompanion" => Context.Inventory.AnimalCompanion == value,
            "HasRickyGloves" => CheckGloves() == value,
            "RickyMounted" => (Context.Entities.RuntimeState.ReadField("wLinkObjectIndex") == 0xd1 ? 1 : 0) == value,
            _ => throw UnsupportedCommand($"read '{binding}'=${value:x2}")
        };

    private int CheckGloves()
    {
        // scripting.s:scriptCmd_df retains checkTreasureObtained's related byte.
        Context.Entities.RuntimeState.SetWramByte(0xcfc1,
            (byte)Context.Inventory.TreasureQuantityDisplayValue(_record.GlovesTreasure));
        return Context.Inventory.HasTreasure(_record.GlovesTreasure) ? 1 : 0;
    }

    public override void WriteMemory(string binding, int value)
    {
        if (binding == "RickyButton")
        { CompanionRuntimeState.WriteScriptButton(Context.Entities.RuntimeState, value); return; }
        if (binding == "RickyVar03")
        { Context.Entities.RuntimeState.SetWramByte(0xd103, checked((byte)value)); return; }
        if (binding != "RickyStateOr")
            throw UnsupportedCommand($"write '{binding}'=${value:x2}");

        OracleSaveData save = Context.Rooms.SaveData;
        save.WriteWramByte(
            _record.RickyStateAddress,
            (byte)(save.ReadWramByte(_record.RickyStateAddress) | value));
    }

    public override void ShowText(int textId, string message)
    {
        if (textId is not (0x2000 or 0x2001 or 0x2003 or 0x2004 or 0x2005))
            throw UnsupportedCommand($"show TX_{textId:x4}");
        Context.ShowDialogue(message, sourceTextId: textId);
    }

    public override void RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "LoseRickyGloves":
                if (!Context.Inventory.LoseTreasure(_record.GlovesTreasure))
                {
                    throw UnsupportedCommand(
                        $"lose missing Ricky gloves ${_record.GlovesTreasure:x2}");
                }
                break;
            default:
                throw UnsupportedCommand($"run native handler '{handler}'");
        }
    }

    public override void ScriptEnded()
    {
        _placedActor = null;
        _scriptedCompanion = null;
    }

    public void Cancel()
    {
        ReleaseInputControl();
        _runner.Clear();
        if (_scriptedCompanion is not null) Context.Entities.RetireEntity(_scriptedCompanion);
        _placedActor = null;
        _scriptedCompanion = null;
        _controller = null;
    }
}
