using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Runs the room $0:$6c Ghini attack and hands the source special object to
/// the persistent mounted-companion runtime at companionForceMount.
/// </summary>
internal sealed class MooshRescueEvent :
    InteractiveCutsceneCommandHost,
    IRoomEntryEvent,
    IUpdatesDuringDialogueRoomEvent,
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
    private NpcCharacter? _moosh;
    private MooshCompanionRoomEntity? _companion;
    private NpcCharacter? _exclamation;
    private MooshRescueStage _stage;
    private int _signal;
    private int _exclamationCounter;
    private bool _mooshTalkable;
    private bool _mooshTalked;
    private bool _screenTransitionsDisabled;

    internal MooshRescueEvent(RoomEventContext context)
    {
        _context = context;
        _record = _database.Record;
        _lanes = new CutsceneCommandLaneScheduler(this);
    }

    public bool HasState => _stage == MooshRescueStage.Running;
    public bool BlocksGameplay => InputLeaseHeld;
    protected override RoomEventContext InputContext => _context;
    RoomEventContext ICutsceneCommandHost.Context => _context;
    internal bool ScreenTransitionsDisabled => _screenTransitionsDisabled;
    // The source gate reads wLinkObjectIndex == >w1Companion. That remains
    // true while Moosh is airborne, charging, falling, or recovering too;
    // it is not a test for the grounded riding state alone.
    internal bool CompanionMounted => _companion?.LinkRiding == true;
    internal int Signal => _signal;
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

        OracleSaveData save = _context.Rooms.SaveData;
        bool prerequisite =
            (save.ReadWramByte(_record.EssenceAddress) & _record.EssenceMask) != 0 &&
            save.HasRoomFlag(
                _record.FlagGroup, _record.FlagRoom, (byte)_record.FlagMask);
        int mooshState = save.ReadWramByte(_record.MooshStateAddress);
        bool ghiniActive = prerequisite &&
            (mooshState & _record.ActiveMask) == 0;
        bool spawnerActive = prerequisite &&
            !_context.Inventory.HasTreasure(_record.ChevalRopeTreasure) &&
            (mooshState & 0x40) == 0;

        if (!ghiniActive)
        {
            DeactivatePlacedGhinis();
            if (spawnerActive)
                SpawnWaitingMoosh();
            return;
        }
        if (!spawnerActive)
        {
            throw new InvalidOperationException(
                "Room 0:6c activated INTERAC_GHINI_HARASSING_MOOSH and " +
                "INTERAC_COMPANION_SCRIPTS without a source-eligible " +
                "INTERAC_COMPANION_SPAWNER $67:$00 Moosh actor.");
        }

        SpawnMoosh();
        _stage = MooshRescueStage.Running;
        _screenTransitionsDisabled = true;
        _lanes.StartLane(Ghini0, _database.Ghini0);
        _lanes.StartLane(Ghini1, _database.Ghini1);
        _lanes.StartLane(Ghini2, _database.Ghini2);
        _lanes.StartLane(Moosh, _database.Companion);
    }

    public void UpdateFrame()
    {
        if (!HasState)
            return;

        if (_stage == MooshRescueStage.Running)
            _lanes.AdvanceFrame();
        UpdateExclamation();
    }

    public void UpdateDuringDialogueFrame()
    {
        // INTERAC_GHINI_HARASSING_MOOSH sets its always-update bit. Its script
        // is held by showtext, but interactionAnimate still advances.
        AnimatePlacedGhini(Ghini0);
        AnimatePlacedGhini(Ghini1);
        AnimatePlacedGhini(Ghini2);
        if (_moosh is not null && GodotObject.IsInstanceValid(_moosh))
            _moosh.AnimateAndUpdateDrawPriorityOneUpdate(_context.Player);
    }

    internal bool TryInteractNpc(NpcCharacter npc)
    {
        if (_stage != MooshRescueStage.Running || !_mooshTalkable ||
            _context.DialogueOpen || !ReferenceEquals(npc, _moosh))
        {
            return false;
        }

        _mooshTalkable = false;
        _mooshTalked = true;
        npc.SetScriptButtonSensitive(false);
        // SPECIALOBJECT_MOOSH state $0a/var03 $01 observes var3d on its next
        // update and writes both wDisabledObjects and wMenuDisabled. Retain
        // that lease through the fear shake so the later angle-to-Link helper
        // sees the side from which Link initiated the conversation.
        SetInputEnabled(false);
        return true;
    }

    public void Cancel()
    {
        ReleaseInputControl();
        _lanes.Clear();
        if (_moosh is not null && GodotObject.IsInstanceValid(_moosh))
            _moosh.SetScriptButtonSensitive(false);
        if (_exclamation is not null && GodotObject.IsInstanceValid(_exclamation))
            _exclamation.SetActive(false);
        _actors.Clear();
        _positions.Clear();
        _moosh = null;
        _companion = null;
        _exclamation = null;
        _stage = MooshRescueStage.Inactive;
        _signal = 0;
        _exclamationCounter = 0;
        _mooshTalkable = false;
        _mooshTalked = false;
        _screenTransitionsDisabled = false;
    }

    private void AddPlacedGhini(string name, int subId)
    {
        NpcCharacter ghini = _context.RequireNpc(
            _record.Group,
            _record.Room,
            _record.GhiniId,
            subId,
            "INTERAC_GHINI_HARASSING_MOOSH");
        ghini.SetScriptDrawOffset(Vector2.Up * 2.0f);
        _actors.Add(name, ghini);
        _positions.Add(
            name,
            OracleObjectMovement.Shared.PositionFromPixels(ghini.Position));
    }

    private void DeactivatePlacedGhinis()
    {
        foreach (string name in new[] { Ghini0, Ghini1, Ghini2 })
            Actor(name).SetActive(false);
        _actors.Clear();
        _positions.Clear();
    }

    private void SpawnMoosh()
    {
        _moosh = _context.Entities.Spawn<NpcCharacter>(new CutsceneNpcSpawn(
            _database.CreateMooshRecord(), Moosh, Talkable: true, Solid: true));
        _actors.Add(Moosh, _moosh);
        _positions.Add(
            Moosh,
            OracleObjectMovement.Shared.PositionFromPixels(_moosh.Position));
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
        _companion = _context.Entities.Spawn<MooshCompanionRoomEntity>(
            new MooshCompanionSpawn(
                new Vector2(_record.MooshX, _record.MooshY),
                2,
                _record.Group,
                _record.Room));
    }

    private void BeginMooshMount()
    {
        if (_moosh is null || !GodotObject.IsInstanceValid(_moosh))
            throw UnsupportedCommand("force-mount missing Moosh");
        bool meetingAgain =
            _context.Rooms.SaveData.IsLinkedGame &&
            _context.Inventory.AnimalCompanion == _record.MooshId;
        // The clean-US first-meeting handoff enters companionForceMount in
        // the left-facing ride pose. The preceding angle-to-Link helper owns
        // only the cutscene actor's response pose; do not leak a below/right
        // approach into SPECIALOBJECT_MOOSH's initial mounted direction.
        int direction = meetingAgain
            ? _moosh.FacingVector == Vector2I.Up ? 0
                : _moosh.FacingVector == Vector2I.Right ? 1
                : _moosh.FacingVector == Vector2I.Down ? 2
                : 3
            : 3;
        Vector2 position = _moosh.Position;
        _moosh.SetActive(false);
        _actors.Remove(Moosh);
        _positions.Remove(Moosh);
        _companion = _context.Entities.Spawn<MooshCompanionRoomEntity>(
            new MooshCompanionSpawn(
                position,
                direction,
                _record.Group,
                _record.Room,
                ForceMount: true));
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
        _context.Entities.Spawn<GhiniCharacter>(
            new GhiniSpawn(position, $"Rescue{actorName}"));
    }

    private void SpawnExclamation()
    {
        if (_moosh is null)
            throw UnsupportedCommand("spawn an exclamation without Moosh");
        Vector2 position = _moosh.Position + new Vector2(
            _record.ExclamationXOffset,
            _record.ExclamationYOffset);
        _exclamation = _context.Entities.Spawn<NpcCharacter>(new CutsceneNpcSpawn(
            _database.CreateExclamationRecord(
                Mathf.RoundToInt(position.Y),
                Mathf.RoundToInt(position.X)),
            "MooshExclamation"));
        _context.Sound.PlaySound(_record.ExclamationSound);
        _exclamationCounter = _record.ExclamationFrames;
    }

    private void UpdateExclamation()
    {
        if (_exclamationCounter <= 0 || _exclamation is null)
            return;
        if (--_exclamationCounter == 0)
        {
            _exclamation.SetActive(false);
            _exclamation = null;
        }
    }

    public override bool HasActorBinding(CutsceneActorId actor) =>
        actor.Value is Ghini0 or Ghini1 or Ghini2 or Moosh;

    public override void SetDisabledObjects(int value)
    {
        if (value is not (0 or 0x11))
            throw UnsupportedCommand($"set wDisabledObjects=${value:x2}");
        SetInputEnabled(value == 0);
    }

    public override bool MemoryEquals(string binding, int value) =>
        binding switch
        {
            "SignalBit01" => ((_signal & 0x01) != 0 ? 1 : 0) == value,
            "SignalBit02" => ((_signal & 0x02) != 0 ? 1 : 0) == value,
            "SignalBit04" => ((_signal & 0x04) != 0 ? 1 : 0) == value,
            "SignalBit08" => ((_signal & 0x08) != 0 ? 1 : 0) == value,
            "SignalBit10" => ((_signal & 0x10) != 0 ? 1 : 0) == value,
            "RoomEnemyCount" => _context.Entities.RoomEnemyCount == value,
            "MooshTalked" => (_mooshTalked ? 1 : 0) == value,
            "AlreadyMooshCompanion" =>
                (_context.Rooms.SaveData.IsLinkedGame &&
                 _context.Inventory.AnimalCompanion == _record.MooshId ? 1 : 0) == value,
            "MooshMounted" => (CompanionMounted ? 1 : 0) == value,
            _ => throw UnsupportedCommand($"read '{binding}'=${value:x2}")
        };

    public override void ShowText(int textId, string message) =>
        _context.ShowDialogue(message);

    public override void WriteMemory(string binding, int value)
    {
        switch (binding)
        {
            case "SignalOr":
                _signal |= value;
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
                _context.Sound.PlayRoomMusic(_record.Group, _record.Room);
                break;
            case "SetMooshTalkable":
                if (_moosh is null)
                    throw UnsupportedCommand("make missing Moosh talkable");
                _mooshTalkable = true;
                _moosh.SetScriptButtonSensitive(true);
                break;
            case "SetMooshAwaitingLink":
                _mooshTalkable = false;
                _moosh?.SetScriptButtonSensitive(false);
                break;
            case "SpawnExclamation":
                SpawnExclamation();
                break;
            case "FaceMooshTowardLink":
                FaceMooshTowardLink();
                break;
            case "BeginMooshMount":
                BeginMooshMount();
                break;
            case "CompleteMooshRescue":
                // companionScript_subid00Script checks wLinkObjectIndex before
                // TX_2205. After the text closes it clears this byte and ends
                // without checking the companion state a second time.
                _screenTransitionsDisabled = false;
                _stage = MooshRescueStage.Inactive;
                break;
            default:
                throw UnsupportedCommand($"run native handler '{handler}'");
        }
    }

    public override bool UpdateNativeHandler(
        string handler,
        CutsceneActorId? actor,
        int commandUpdate,
        int frames,
        string payload)
    {
        if (actor is null)
            throw UnsupportedCommand($"run '{handler}' without an actor");
        switch (handler)
        {
            case "CircleGhini":
                return UpdateGhiniCircle(actor.Value.Value, commandUpdate, frames);
            case "ShakeMoosh":
                return UpdateMooshShake(actor.Value.Value, commandUpdate, frames);
            default:
                throw UnsupportedCommand($"update native handler '{handler}'");
        }
    }

    private bool UpdateGhiniCircle(string actor, int update, int frames)
    {
        if (frames != _record.GhiniFrames)
            throw UnsupportedCommand($"circle {actor} for {frames} updates");
        if (update == 0)
            return false;

        int angle = (_record.GhiniAngle + 1 - update) & 0x1f;
        OracleObjectPosition position = OracleObjectMovement.Shared.ApplySpeed(
            _positions[actor], _record.GhiniSpeed, angle);
        _positions[actor] = position;
        Actor(actor).SetStatePosition(position.PixelPosition);
        return update >= frames;
    }

    private bool UpdateMooshShake(string actor, int update, int frames)
    {
        if (actor != Moosh || frames != _record.ShakeFrames)
            throw UnsupportedCommand($"shake {actor} for {frames} updates");
        if (update == 0)
            return false;

        int remaining = frames - update;
        if ((remaining & 3) == 0)
        {
            OracleObjectPosition position = _positions[actor];
            int x = (Mathf.FloorToInt(position.PixelPosition.X) ^ 0x02);
            position = OracleObjectMovement.Shared.PositionFromPixels(
                new Vector2(x, position.PixelPosition.Y));
            _positions[actor] = position;
            Actor(actor).SetStatePosition(position.PixelPosition);
        }
        return update >= frames;
    }

    public override void ScriptEnded()
    {
        // Each placed interaction deletes itself independently. The companion
        // lane has already transferred its actor into w1Companion ownership.
    }

    private static Vector2I DirectionToward(Vector2 origin, Vector2 target)
    {
        int angle = (OracleObjectMovement.Shared.RelativeAngle(origin, target) + 4) & 0x18;
        return angle switch
        {
            0 => Vector2I.Up,
            8 => Vector2I.Right,
            16 => Vector2I.Down,
            _ => Vector2I.Left
        };
    }

    private void FaceMooshTowardLink()
    {
        NpcCharacter moosh = Actor(Moosh);
        Vector2I direction = DirectionToward(
            moosh.Position, _context.Player.Position);
        moosh.SetFacingDirection(direction);

        // companionScript_writeAngleTowardLinkToCompanionVar3f stores
        // direction + 1, then SPECIALOBJECT_MOOSH_CUTSCENE state $0a calls
        // companionSetAnimationToVar3f during the following 60-update wait.
        int animation = direction == Vector2I.Up ? 0x01
            : direction == Vector2I.Right ? 0x02
            : direction == Vector2I.Down ? 0x03
            : 0x04;
        moosh.SetScriptAnimation(_database.Visual.Animations[animation]);
    }
}

internal enum MooshRescueStage
{
    Inactive,
    Running
}
