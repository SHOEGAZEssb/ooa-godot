
namespace Oracles;

/// <summary>
/// Runs the room $0:$6c Ghini attack and hands the source special object to
/// the persistent mounted-companion runtime at companionForceMount.
/// </summary>
internal sealed class MooshRescueEvent :
    RoomCutsceneCommandHost,
    IRoomEntryEvent,
    ICutsceneCommandHost
{
    private const string Ghini0 = "Ghini0";
    private const string Ghini1 = "Ghini1";
    private const string Ghini2 = "Ghini2";
    private const string Moosh = "Moosh";

    private readonly RoomEventContext _context;
    private readonly MooshRescueEventDatabase _database = new();
    private readonly MooshRescueEventRecord _record;
    private readonly CutsceneCommandLaneScheduler _lanes;
    private readonly Dictionary<string, NpcCharacter> _actors =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, OracleObjectPosition> _positions =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _ghiniAngles =
        new(StringComparer.Ordinal);
    private NpcCharacter? _moosh;
    private ScriptedCompanionRoomEntity? _scriptedCompanion;
    private MooshCompanionRoomEntity? _companion;
    private IRoomEntity? _exclamation;
    private MooshRescueControllerRoomEntity? _controller;

    internal MooshRescueEvent(RoomEventContext context)
    {
        _context = context;
        _record = _database.Record;
        _lanes = new CutsceneCommandLaneScheduler(this);
    }

    public bool HasState => _lanes.Active || SourceHandlersAllowed() && _actors.Count != 0;
    public bool RequiresRoomEventUpdate => false;
    public bool BlocksGameplay => InputControlHeld;
    public override RoomEventContext Context => _context;
    public bool ScreenTransitionsDisabled => HasState && _context.Entities.RuntimeState.ReadField("wDisableScreenTransitions") != 0;
    // The source gate reads wLinkObjectIndex == >w1Companion. That remains
    // true while Moosh is airborne, charging, falling, or recovering too;
    // it is not a test for the grounded riding state alone.
    internal bool CompanionMounted => _context.Entities.RuntimeState.ReadField("wLinkObjectIndex") == 0xd1;
    internal int Signal => CompanionRuntimeState.ReadScriptSignals(_context.Entities.RuntimeState);
    internal MooshRescueEventDatabase Database => _database;
    internal NpcCharacter? MooshActor => _moosh;

    public bool Matches(int group, OracleRoomData room) =>
        group == _record.Group && room.Id == _record.Room;

    public void Start(OracleRoomData room)
    {
        Cancel();
        if (!Matches(_context.Rooms.ActiveGroup, room))
        {
            throw new InvalidOperationException(
                $"The Moosh rescue cannot start in " +
                $"{_context.Rooms.ActiveGroup:x}:{room.Id:x2}.");
        }

        AddPlacedGhini(Ghini0, 0);
        AddPlacedGhini(Ghini1, 1);
        AddPlacedGhini(Ghini2, 2);
        _controller = _context.Entities.EntityAdapters<MooshRescueControllerRoomEntity>().SingleOrDefault();
    }

    private bool PrerequisiteMet() =>
        (_context.Rooms.SaveData.ReadWramByte(_record.EssenceAddress) & _record.EssenceMask) != 0 &&
        _context.Rooms.SaveData.HasRoomFlag(_record.FlagGroup, _record.FlagRoom, (byte)_record.FlagMask);

    private bool SourceHandlersAllowed() => PrerequisiteMet() &&
        (_context.Rooms.SaveData.ReadWramByte(_record.MooshStateAddress) & _record.ActiveMask) == 0;

    internal bool InitializeGhini(NpcCharacter actor)
    {
        string name = $"Ghini{actor.Record.SubId}";
        if (!_actors.TryGetValue(name, out NpcCharacter? current) || !ReferenceEquals(actor, current)) return false;
        if (!SourceHandlersAllowed())
        {
            _actors.Remove(name); _positions.Remove(name);
            return false;
        }
        actor.SetActive(true);
        actor.SetScriptVisible(true);
        actor.SetFixedDrawPriority(ObjectDrawPriority.FixedHighPriorityZIndex);
        _lanes.StartLane(name, actor.Record.SubId switch
        { 0 => _database.Ghini0, 1 => _database.Ghini1, 2 => _database.Ghini2, _ => throw UnsupportedCommand($"initialize $73:${actor.Record.SubId:x2}") });
        return true;
    }

    internal bool AdvanceGhini(NpcCharacter actor)
    {
        string name = $"Ghini{actor.Record.SubId}";
        if (!_actors.TryGetValue(name, out NpcCharacter? current) || !ReferenceEquals(actor, current)) return false;
        if (actor.Record.SubId == 0 && (_context.Entities.RuntimeState.ReadField("wScrollMode") & 0x0e) != 0) return true;
        AnimatePlacedGhini(name);
        AdvanceGhiniCircle(name);
        return _lanes.AdvanceLane(name);
    }

    internal bool InitializeController(MooshRescueControllerRoomEntity controller)
    {
        if (!ReferenceEquals(controller, _controller)) return false;
        if (_context.Player.IsDying) { SetDisabledObjects(0); return false; }
        if (!SourceHandlersAllowed()) return false;
        EventResources.ScriptInput.SetScreenTransitionsDisabled(1);
        _context.Entities.RuntimeState.SetField("wDiggingUpEnemiesForbidden", 1);
        _lanes.StartLane(Moosh, _database.Companion);
        return true;
    }

    internal bool AdvanceController(MooshRescueControllerRoomEntity controller)
    {
        if (!ReferenceEquals(controller, _controller)) return false;
        if (_context.Player.IsDying) { SetDisabledObjects(0); _lanes.StopLane(Moosh); return false; }
        if (controller.ShakeCounter != 0 && (--controller.ShakeCounter & 3) == 0) ShakeCompanion();
        if (_lanes.AdvanceLane(Moosh)) return true;
        OracleRuntimeState memory = _context.Entities.RuntimeState;
        memory.SetField("wStatusBarNeedsRefresh", (byte)(memory.ReadField("wStatusBarNeedsRefresh") | 2));
        return false;
    }

    public void UpdateFrame() => throw new InvalidOperationException("Moosh rescue scripts advance only from their native interaction slots.");

    internal void SpawnPreset()
    {
        if (!_context.Entities.SpecialObjectSlotAvailable || !PrerequisiteMet() ||
            _context.Inventory.HasTreasure(_record.ChevalRopeTreasure) ||
            (_context.Rooms.SaveData.ReadWramByte(_record.MooshStateAddress) & 0x40) != 0) return;
        CompanionRuntimeState.InstallPreset(_context.Entities.RuntimeState, _context.Rooms.SaveData, new(_record.MooshX, _record.MooshY));
        if ((_context.Rooms.SaveData.ReadWramByte(_record.MooshStateAddress) & _record.RescuedMask) == 0) SpawnMoosh();
        else SpawnWaitingMoosh();
    }

    public void Cancel()
    {
        ReleaseInputControl();
        _lanes.Clear();
        if (_scriptedCompanion is not null) _context.Entities.RetireEntity(_scriptedCompanion);
        if (_moosh is not null && GodotObject.IsInstanceValid(_moosh))
            _moosh.SetScriptButtonSensitive(false);
        if (_exclamation is not null) _context.Entities.RetireEntity(_exclamation);
        _actors.Clear();
        _positions.Clear();
        _ghiniAngles.Clear();
        _moosh = null;
        _scriptedCompanion = null;
        _companion = null;
        _exclamation = null;
        _controller = null;
    }

    private void AddPlacedGhini(string name, int subId)
    {
        NpcCharacter? ghini = _context.Entities.Entities<NpcCharacter>().SingleOrDefault(npc =>
            npc.Record.Group == _record.Group && npc.Record.Room == _record.Room &&
            npc.Record.Id == _record.GhiniId && npc.Record.SubId == subId);
        if (ghini is null) return; // A full source interaction stream skips its remaining placements.
        ghini.SetScriptDrawOffset(Vector2.Up * 2.0f);
        _actors.Add(name, ghini);
        _positions.Add(
            name,
            OracleObjectMovement.Shared.PositionFromPixels(ghini.Position));
    }

    private void SpawnMoosh()
    {
        if (!_context.Entities.SpecialObjectSlotAvailable) return;
        _moosh = NpcCharacter.CreateFromRecord(_database.CreateMooshRecord(), _context.Entities.AssetRoot);
        _moosh.SetScriptAnimation(_database.Visual.Animations[0]);
        _moosh.SetActive(false);
        _scriptedCompanion = new AgesMooshScriptedCompanionRoomEntity(_moosh,
            _context.Entities.RuntimeState, _context.Rooms.SaveData, () => _context.Rooms.CurrentRoom, _database.Visual,
            new MooshGoodbyeEventDatabase().Record, ContinueWithOrdinaryCompanion,
            value => SetDisabledObjects(value), SetNativeMenuDisabled, _context.Entities.OnRoomEntityDialogueRequested);
        _context.Entities.AddEntity(_scriptedCompanion);
    }

    private void SpawnWaitingMoosh()
    {
        List<MooshCompanionRoomEntity> companions =
            _context.Entities.Entities<MooshCompanionRoomEntity>();
        companions.AddRange(
            _context.Entities.OutgoingEntities<MooshCompanionRoomEntity>());
        if (companions.Count > 1)
        {
            throw new InvalidOperationException(
                "Room 0:6c resolved more than one live w1Companion Moosh " +
                "across the active/outgoing scrolling sets.");
        }
        if (companions.Count == 1)
        {
            _companion = companions[0];
            return;
        }
        if (CompanionRuntimeState.AnyActive(_context.Entities.RuntimeState) ||
            _context.Entities.EntityAdapters<IRoomEntity>().Any(actor => actor is IRoomInitializedCompanion)) return;
        CompanionRuntimeState.InstallPreset(_context.Entities.RuntimeState, _context.Rooms.SaveData, new(_record.MooshX, _record.MooshY));
        SpawnMoosh();
    }

    private void ContinueWithOrdinaryCompanion(ScriptedCompanionRoomEntity body, Player player, bool mounting)
    {
        body.CompleteNativeUpdate();
        int frame = body.Npc.CurrentAnimationFrame, counter = body.Npc.CurrentAnimationCounter;
        body.Npc.SetScriptButtonSensitive(false); body.Npc.SetActive(false);
        _companion = (MooshCompanionRoomEntity)_context.Entities.ReplaceSpecialObjectPresentation(body,
            new MooshCompanionSpawn(body.NativeSpecialObjectPosition, ObjectDirection.Down,
                _record.Group, _record.Room, ForceMount: mounting));
        _companion.ContinueNativePresentation(player, frame, counter, mounting);
        _moosh = null; _scriptedCompanion = null;
    }

    private NpcCharacter Actor(string name) =>
        _actors.TryGetValue(name, out NpcCharacter? actor)
            ? actor
            : throw UnsupportedCommand($"resolve actor '{name}'");

    private void AnimatePlacedGhini(string name)
    {
        if (_actors.TryGetValue(name, out NpcCharacter? ghini))
            ghini.AnimateAndUpdateDrawPriorityOneUpdate(_context.Player);
    }

    private void SpawnEnemy(string actorName)
    {
        NpcCharacter actor = Actor(actorName);
        Vector2 position = actor.Position;
        actor.SetActive(false);
        _actors.Remove(actorName);
        _positions.Remove(actorName);
        _ghiniAngles.Remove(actorName);
        _context.Entities.TryCreateEnemy(
            new GhiniSpawn(position,0,$"Rescue{actorName}","scripts/ages/scripts.s:ghiniHarassingMoosh_*:spawnenemyhere"));
    }

    private void SpawnExclamation()
    {
        Vector2 position = new Vector2(_record.ControllerX, _record.ControllerY) + new Vector2(
            _record.ExclamationXOffset,
            _record.ExclamationYOffset);
        _exclamation = _context.Entities.TryCreateInteractionEntity(new ExclamationMarkSpawn(position, Duration: _record.ExclamationFrames));
    }

    public override bool HasActorBinding(CutsceneActorId actor) =>
        actor.Value is Ghini0 or Ghini1 or Ghini2 or Moosh or "Controller";

    public override void SetDisabledObjects(int value)
    {
        if (value is not (0 or 1 or 0x11))
            throw UnsupportedCommand($"set wDisabledObjects=${value:x2}");
        base.SetDisabledObjects(value);
    }

    public override bool MemoryEquals(string binding, int value) =>
        binding switch
        {
            "SignalBit01" => ((Signal & 0x01) != 0 ? 1 : 0) == value,
            "SignalBit02" => ((Signal & 0x02) != 0 ? 1 : 0) == value,
            "SignalBit04" => ((Signal & 0x04) != 0 ? 1 : 0) == value,
            "SignalBit08" => ((Signal & 0x08) != 0 ? 1 : 0) == value,
            "SignalBit10" => ((Signal & 0x10) != 0 ? 1 : 0) == value,
            "RoomEnemyCount" => _context.Entities.RoomEnemyCount == value,
            "MooshButton" => CompanionRuntimeState.ReadScriptButton(_context.Entities.RuntimeState) == value,
            "MooshMounted" => (CompanionMounted ? 1 : 0) == value,
            "IsLinkedGame" => (_context.Rooms.SaveData.IsLinkedGame ? 1 : 0) == value,
            "AnimalCompanion" => _context.Inventory.AnimalCompanion == value,
            "ShakeCounter" => (_controller?.ShakeCounter ?? 0) == value,
            "Ghini0Speed" => (_ghiniAngles.ContainsKey(Ghini0) ? _record.GhiniSpeed : 0) == value,
            "Ghini1Speed" => (_ghiniAngles.ContainsKey(Ghini1) ? _record.GhiniSpeed : 0) == value,
            "Ghini2Speed" => (_ghiniAngles.ContainsKey(Ghini2) ? _record.GhiniSpeed : 0) == value,
            _ => throw UnsupportedCommand($"read '{binding}'=${value:x2}")
        };

    public override void ShowText(int textId, string message) =>
        _context.ShowDialogue(message, sourceTextId: textId);

    public override void WriteObjectByte(string actor, int address, int value)
    {
        if (actor != "Controller" || address != 0x7a || _controller is null || (uint)value > 0xff)
            throw UnsupportedCommand($"write {actor}.${address:x2}=${value:x2}");
        _controller.ShakeCounter = (byte)value;
    }

    public override void WriteMemory(string binding, int value)
    {
        switch (binding)
        {
            case "SignalOr":
                CompanionRuntimeState.OrScriptSignals(_context.Entities.RuntimeState, value);
                break;
            case "MooshVar03":
                _context.Entities.RuntimeState.SetWramByte(0xd103, checked((byte)value));
                break;
            case "MooshStateOr":
            {
                OracleSaveData save = _context.Rooms.SaveData;
                save.WriteWramByte(
                    _record.MooshStateAddress,
                    (byte)(save.ReadWramByte(_record.MooshStateAddress) | value));
                break;
            }
            default:
                throw UnsupportedCommand($"write '{binding}'=${value:x2}");
        }
    }

    public override void PlaySound(int sound) =>
        _context.Sound.PlaySound(sound);

    public override void SetMusic(int music) =>
        _context.Sound.PlaySound(music);

    public override void RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "BeginGhini0Circle":
                BeginGhiniCircle(Ghini0);
                break;
            case "BeginGhini1Circle":
                BeginGhiniCircle(Ghini1);
                break;
            case "BeginGhini2Circle":
                BeginGhiniCircle(Ghini2);
                break;
            case "SpawnEnemyGhini0":
                SpawnEnemy(Ghini0);
                break;
            case "SpawnEnemyGhini1":
                SpawnEnemy(Ghini1);
                break;
            case "SpawnEnemyGhini2":
                SpawnEnemy(Ghini2);
                break;
            case "RestoreRoomMusic":
                _context.Sound.SetNativeActiveMusic(_context.Sound.NativeRoomMusic);
                _context.Sound.PlayNativeSound(_context.Sound.NativeRoomMusic);
                break;
            case "SpawnExclamation":
                SpawnExclamation();
                break;
            case "FaceMooshTowardLink":
                FaceMooshTowardLink();
                break;
            case "CompleteMooshRescue":
                // companionScript_subid00Script checks wLinkObjectIndex before
                // TX_2205. After the text closes it clears this byte and ends
                // without checking the companion state a second time.
                EventResources.ScriptInput.SetScreenTransitionsDisabled(0);
                break;
            default:
                throw UnsupportedCommand($"run native handler '{handler}'");
        }
    }

    private void BeginGhiniCircle(string actor)
    {
        if (!_actors.ContainsKey(actor) || !_ghiniAngles.TryAdd(actor, _record.GhiniAngle))
            throw UnsupportedCommand($"begin an unavailable Ghini circle for {actor}");
    }

    private void AdvanceGhiniCircle(string actor)
    {
        if (!_ghiniAngles.TryGetValue(actor, out int angle))
            return;
        OracleObjectPosition position = NativeObjectMovement.ApplySpeed(
            _context.Entities.RuntimeState, _positions[actor], _record.GhiniSpeed, angle);
        _positions[actor] = position;
        Actor(actor).SetStatePosition(position.PixelPosition);
        angle = (angle - 1) & ObjectAngle.Mask;
        if (angle == _record.GhiniAngle)
            _ghiniAngles.Remove(actor);
        else
            _ghiniAngles[actor] = angle;
    }

    internal void ShakeCompanion()
    {
        OracleRuntimeState memory = _context.Entities.RuntimeState;
        memory.SetWramByte(0xd10d, (byte)(memory.ReadWramByte(0xd10d) ^ 2));
        if (_scriptedCompanion is not null && _context.Entities.IsSpecialObjectOwner(_scriptedCompanion))
            _scriptedCompanion.NativeSpecialObjectPosition = CompanionRuntimeState.Read(memory).Position;
    }

    public override void ScriptEnded()
    {
        // Each placed interaction deletes itself independently. The companion
        // lane has already transferred its actor into w1Companion ownership.
    }

    private void FaceMooshTowardLink()
    {
        Vector2I direction = RoomEventResources.DirectionToward(
            new Vector2(_record.ControllerX, _record.ControllerY), _context.Player.Position);

        // companionScript_writeAngleTowardLinkToCompanionVar3f stores
        // direction + 1, then SPECIALOBJECT_MOOSH_CUTSCENE state $0a calls
        // companionSetAnimationToVar3f during the following 60-update wait.
        int animation = direction == Vector2I.Up ? 0x01
            : direction == Vector2I.Right ? 0x02
            : direction == Vector2I.Down ? 0x03
            : 0x04;
        _context.Entities.RuntimeState.SetWramByte(0xd13f, (byte)animation);
    }
}
