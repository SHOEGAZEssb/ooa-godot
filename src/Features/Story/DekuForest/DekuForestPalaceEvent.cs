using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Runs the Mystery Seeds escort from room $1:$46 through Ambi's throne room
/// and the direct black-screen return to the palace entrance.
/// </summary>
internal sealed class DekuForestPalaceEvent :
    CutsceneCommandHost,
    IRoomEntryEvent,
    IUpdatesDuringDialogueRoomEvent,
    ICutsceneCommandHost
{
    private const string EntranceGuard = "EntranceGuard";
    private const string CorridorGuard = "CorridorGuard";
    private const string RewardGuard = "RewardGuard";
    private const string EscortGuard = "EscortGuard";
    private const string Ambi = "Ambi";
    private const string Nayru = "Nayru";
    private const string ExitGuard = "ExitGuard";
    private const string OtherExitGuard = "OtherExitGuard";
    private const string CutsceneSignal = "CutsceneSignal";

    private readonly RoomEventContext _context;
    private readonly DekuForestPalaceEventDatabase _database = new();
    private readonly DekuForestPalaceEventRecord _record;
    private readonly Dictionary<string, NpcCharacter> _actors =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, Vector2> _precisePositions =
        new(StringComparer.Ordinal);
    private readonly List<NpcCharacter> _genericGuards = new();
    private readonly List<SideGuardState> _sideGuards = new();
    private readonly CutsceneCommandRunner _singleRunner;
    private readonly CutsceneCommandLaneScheduler _lanes;

    private DekuForestPalaceStage _stage;
    private PalacePlayerInput _playerInput;
    private RewardGuardFlightStage _rewardFlightStage;
    private CutsceneCommandRunner? _corridorRunner;
    private int _signal;
    private int _textboxFlags;
    private int _playerInputCounter;
    private int _entranceHorizontal;
    private int _rewardCounter;
    private int _rewardZFixed;
    private int _rewardSpeedZ;
    private bool _inputHeld;
    private bool _menusDisabled;
    private bool _fadeCompleted;
    private bool _returnRequested;
    private bool _directExit;

    internal DekuForestPalaceEvent(RoomEventContext context)
    {
        _context = context;
        _record = _database.Record;
        _singleRunner = new CutsceneCommandRunner(this);
        _lanes = new CutsceneCommandLaneScheduler(this);
    }

    public bool HasState => _stage != DekuForestPalaceStage.Inactive;
    public bool BlocksGameplay => _stage is not
        (DekuForestPalaceStage.Inactive or DekuForestPalaceStage.GenericGuards);
    internal bool MenusDisabled => _menusDisabled;
    internal DekuForestPalaceStage Stage => _stage;
    internal int Signal => _signal;
    internal bool DirectExit => _directExit;

    public bool Matches(int group, OracleRoomData room) =>
        group == _record.Group &&
        room.Id is var id &&
        (id == _record.EntranceRoom ||
         id == _record.CorridorRoom1 ||
         id == _record.CorridorRoom2 ||
         id == _record.ThroneRoom);

    public void Start(OracleRoomData room)
    {
        Cancel();
        if (!Matches(_context.Rooms.ActiveGroup, room))
        {
            throw new InvalidOperationException(
                $"The Ambi palace escort cannot start in " +
                $"{_context.Rooms.ActiveGroup:x}:{room.Id:x2}.");
        }

        bool completed = _context.Rooms.SaveData.HasGlobalFlag(
            _record.CompletionFlag);
        bool hasMysterySeeds = _context.Inventory.HasTreasure(
            _record.MysterySeeds);

        if (room.Id == _record.EntranceRoom)
        {
            if (completed || !hasMysterySeeds)
                StartOrdinaryEntranceGuards(completed);
            else
                StartEntranceEscort();
            return;
        }

        if (!hasMysterySeeds || completed)
            return;

        if (room.Id == _record.ThroneRoom)
            StartThroneRoom();
        else
            StartCorridor(room.Id);
    }

    public void UpdateFrame()
    {
        switch (_stage)
        {
            case DekuForestPalaceStage.GenericGuards:
                AnimateAllActors();
                break;

            case DekuForestPalaceStage.Entrance:
                AdvancePlayerInput();
                AnimateAllActors();
                _singleRunner.AdvanceFrame();
                break;

            case DekuForestPalaceStage.Corridor:
                UpdateCorridorGuardScreenBoundary();
                AdvancePlayerInput();
                UpdateSideGuards();
                AnimateAllActors(corridor: true);
                _singleRunner.AdvanceFrame();
                break;

            case DekuForestPalaceStage.Throne:
                AdvancePlayerInput();
                UpdateRewardGuardFlight();
                AnimateAllActors();
                _lanes.AdvanceFrame();
                if (_returnRequested)
                    LoadDirectExit();
                break;

            case DekuForestPalaceStage.Exit:
                AnimateAllActors();
                _singleRunner.AdvanceFrame();
                break;
        }
    }

    public void UpdateDuringDialogueFrame() => UpdateFrame();

    public bool TryInteractNpc(NpcCharacter npc)
    {
        if (_stage != DekuForestPalaceStage.GenericGuards ||
            _context.DialogueOpen ||
            !_genericGuards.Contains(npc))
        {
            return false;
        }

        _context.ShowDialogue(npc.Message);
        return true;
    }

    public void Cancel() => Cancel(deactivateActors: true);

    internal void Cancel(bool deactivateActors)
    {
        bool restoreFade =
            _stage == DekuForestPalaceStage.Exit ||
            _stage == DekuForestPalaceStage.Throne && _signal == 0x07;
        if (_inputHeld)
            _context.Player.EndCutsceneControl();
        _inputHeld = false;

        foreach (NpcCharacter actor in _actors.Values)
        {
            if (GodotObject.IsInstanceValid(actor))
            {
                actor.SetScriptDrawOffset(Vector2.Zero);
                actor.SetScriptButtonSensitive(false);
                actor.SetAnimationRate(1.0f);
                if (deactivateActors)
                    actor.SetActive(false);
            }
        }

        _actors.Clear();
        _precisePositions.Clear();
        _genericGuards.Clear();
        _sideGuards.Clear();
        _singleRunner.Clear();
        _lanes.Clear();
        _corridorRunner = null;
        _stage = DekuForestPalaceStage.Inactive;
        _playerInput = PalacePlayerInput.None;
        _rewardFlightStage = RewardGuardFlightStage.Inactive;
        _signal = 0;
        _textboxFlags = 0;
        _playerInputCounter = 0;
        _entranceHorizontal = 0;
        _rewardCounter = 0;
        _rewardZFixed = 0;
        _rewardSpeedZ = 0;
        _menusDisabled = false;
        _fadeCompleted = false;
        _returnRequested = false;
        _directExit = false;
        if (restoreFade)
            RestoreFade();
    }

    RoomEventContext ICutsceneCommandHost.Context => _context;

    public override bool HasActorBinding(CutsceneActorId actor) =>
        _actors.ContainsKey(actor.Value);

    public override void SetInputEnabled(bool enabled)
    {
        if (enabled == !_inputHeld)
            return;
        _inputHeld = !enabled;
        if (enabled)
            _context.Player.EndCutsceneControl();
        else
            _context.Player.BeginCutsceneControl();
    }

    public override void SetMenuEnabled(bool enabled)
    {
        if (enabled)
        {
            throw new InvalidOperationException(
                "The Ambi palace escort never re-enables the menu before input.");
        }
        _menusDisabled = true;
    }

    public override void SetDisabledObjects(int value)
    {
        if (value != 0x11)
        {
            throw new InvalidOperationException(
                $"The palace reward requested unsupported wDisabledObjects ${value:x2}.");
        }
        SetInputEnabled(enabled: false);
    }

    public override bool MemoryEquals(string binding, int value)
    {
        if (binding != CutsceneSignal)
        {
            throw new InvalidOperationException(
                $"The Ambi palace scripts cannot read '{binding}'.");
        }
        return _signal == value;
    }

    public override void ShowText(
        int textId,
        string message,
        int? textboxPosition) =>
        _context.ShowDialogue(
            message,
            textboxPosition,
            _textboxFlags);

    public override void SetActorAnimation(
        string actor,
        int animation,
        string encodedAnimation)
    {
        if (string.IsNullOrEmpty(encodedAnimation))
        {
            throw new InvalidOperationException(
                $"Palace actor '{actor}' animation ${animation:x2} has no imported OAM.");
        }
        RequireActor(actor).SetScriptAnimation(encodedAnimation);
    }

    public override void SetActorMovementAnimation(
        string actor,
        int angle,
        string encodedAnimation)
    {
        if (string.IsNullOrEmpty(encodedAnimation))
        {
            throw new InvalidOperationException(
                $"Palace actor '{actor}' movement ${angle:x2} has no imported OAM.");
        }
        RequireActor(actor).SetScriptAnimation(encodedAnimation);
    }

    public override void MoveActorAtSpeed(string actor, int speed, int angle)
    {
        NpcCharacter npc = RequireActor(actor);
        if (actor == CorridorGuard)
        {
            // soldierSubid05 overwrites Interaction.speed from the tile below
            // the actor immediately before interactionRunScript each update.
            speed = _context.Rooms.CurrentRoom.GetTerrainInfo(npc.Position).Type ==
                TerrainType.Stairs
                ? _record.StairsSpeed
                : _record.NormalSpeed;
        }
        Vector2 precise = _precisePositions[actor];
        npc.SetStatePosition(OracleObjectMovement.Shared.ApplySpeed(
            ref precise, speed, angle));
        _precisePositions[actor] = precise;
    }

    public override void SetActorVisible(string actor, bool visible) =>
        RequireActor(actor).SetScriptVisible(visible);

    public override void WriteMemory(string binding, int value)
    {
        switch (binding)
        {
            case CutsceneSignal:
                _signal = value;
                return;

            case "MysterySeeds":
                if (value != 0)
                {
                    throw new InvalidOperationException(
                        $"soldierSubid06Script wrote unexpected Mystery Seeds value ${value:x2}.");
                }
                _context.Inventory.SetMysterySeedsFromScript(value);
                return;

            case "ScrollMode":
                if (value != 0)
                {
                    throw new InvalidOperationException(
                        $"soldierSubid06Script wrote unexpected wScrollMode ${value:x2}.");
                }
                return;

            case "SimulatedInput":
                if (value != 0)
                {
                    throw new InvalidOperationException(
                        $"soldierSubid07Script wrote unexpected simulated input ${value:x2}.");
                }
                _playerInput = PalacePlayerInput.None;
                return;

            case "TextboxFlags":
                if (value != _record.TextboxFlags)
                {
                    throw new InvalidOperationException(
                        $"nayruScript01 wrote unexpected textbox flags ${value:x2}.");
                }
                _textboxFlags = value;
                return;

            default:
                throw new InvalidOperationException(
                    $"The Ambi palace scripts cannot write '{binding}'=${value:x2}.");
        }
    }

    public override void GiveItem(int treasureId, int parameter)
    {
        if (treasureId != _record.RewardTreasure ||
            parameter != _record.RewardSubId)
        {
            throw new InvalidOperationException(
                $"soldierSubid04Script requested unexpected reward " +
                $"${treasureId:x2}:${parameter:x2}.");
        }

        _context.GrantScriptTreasure(
            _record.Group,
            _record.ThroneRoom,
            treasureId,
            parameter,
            _record.RewardObject,
            "scripts.s:soldierSubid04Script giveitem TREASURE_OBJECT_BOMBS_02",
            objectParameter: _record.RewardParameter);
    }

    public override void SetGlobalFlag(int flag)
    {
        if (flag != _record.EntranceFlag &&
            flag != _record.CompletionFlag)
        {
            throw new InvalidOperationException(
                $"The Ambi palace scripts requested unexpected global flag ${flag:x2}.");
        }
        _context.Rooms.SaveData.SetGlobalFlag(flag);
        if (flag == _record.EntranceFlag)
        {
            foreach (NpcCharacter actor in _actors.Values)
                actor.SetBlocksLink(false);
        }
    }

    public override void RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "ForceLinkUp":
                _context.Player.Face(Vector2I.Up);
                return;

            case "ForceLinkLeft":
                _context.Player.Face(Vector2I.Left);
                return;

            case "StartEntranceInput":
                StartEntranceInput();
                return;

            case "GiveMysterySeeds":
                _context.Inventory.GiveTreasure(_record.MysterySeeds, 0);
                return;

            case "StartExitInput":
                _playerInput = PalacePlayerInput.Exit;
                _playerInputCounter = 0;
                return;

            case "EnableAllObjects":
                return;

            case "ShowNayru":
                RequireActor(Nayru).SetScriptVisible(true);
                return;

            case "UpdateMinimap":
                _context.Rooms.SaveData.SetMinimapLocation(
                    _record.Group, _record.EntranceRoom);
                return;

            case "BecomeGenericGuard":
                return;

            default:
                throw new InvalidOperationException(
                    $"Unknown Ambi palace native script handler '{handler}'.");
        }
    }

    public override bool UpdateNativeHandler(
        string handler,
        CutsceneActorId? actor,
        int commandUpdate,
        int frames,
        string payload)
    {
        if (handler != "FadeOutBlack" ||
            actor is not null ||
            frames != _record.FadeFrames ||
            payload.Length != 0)
        {
            throw new InvalidOperationException(
                $"Unsupported Ambi palace blocking native '{handler}'.");
        }

        float progress = Math.Clamp((commandUpdate + 1.0f) / frames, 0.0f, 1.0f);
        _context.RoomView.SetBackgroundFade(Colors.Black, progress);
        _context.Hud.SetHiddenStatusBarFade(Colors.Black, progress);
        _fadeCompleted = commandUpdate + 1 >= frames;
        return _fadeCompleted;
    }

    public override void ScriptEnded()
    {
        switch (_stage)
        {
            case DekuForestPalaceStage.Throne when
                _fadeCompleted && _signal == 0x07:
                _returnRequested = true;
                break;

            case DekuForestPalaceStage.Exit:
                FinishExitIntroduction();
                break;
        }
    }

    private void StartEntranceEscort()
    {
        _stage = DekuForestPalaceStage.Entrance;
        _menusDisabled = true;
        Spawn(EntranceGuard, _database.RequireActor(
            _record.EntranceRoom, 0x40, 0x02));
        NpcCharacter right = Spawn("RightEntranceGuard", _database.RequireActor(
            _record.EntranceRoom, 0x40, 0x09));
        NpcCharacter red = Spawn("RedEscortGuard", _database.RequireActor(
            _record.EntranceRoom, 0x40, 0x0b));
        right.SetBlocksLink(true);
        red.SetBlocksLink(false);
        _singleRunner.Start(_database.EntranceCommands);
    }

    private void StartOrdinaryEntranceGuards(bool completed)
    {
        _stage = DekuForestPalaceStage.GenericGuards;
        int textId = completed ? _record.TerminalTextId : 0x5903;
        string text = completed
            ? _record.TerminalText
            : _database.RequireActor(
                _record.EntranceRoom, 0x40, 0x02).Message;
        AddGenericGuard(Spawn(
            "LeftEntranceGuard",
            _database.RequireActor(
                _record.EntranceRoom, 0x40, 0x02) with
            {
                TextId = textId,
                Message = text
            }));
        AddGenericGuard(Spawn(
            "RightEntranceGuard",
            _database.RequireActor(
                _record.EntranceRoom, 0x40, 0x09) with
            {
                TextId = textId,
                Message = text
            }));
    }

    private void StartCorridor(int room)
    {
        _stage = DekuForestPalaceStage.Corridor;
        _menusDisabled = true;
        SetInputEnabled(enabled: false);
        _playerInput = PalacePlayerInput.Enter;
        // soldierSubid05 state 0 writes only w1Link.xh. Preserve the 8.8 low
        // byte and all other Link state across the scrolling-room preload.
        _context.Player.SetScriptedCoordinateHigh(
            horizontal: true, coordinate: 0x50);

        NpcCharacter corridorGuard = Spawn(
            CorridorGuard,
            _database.RequireActor(room, 0x40, 0x05));
        // soldierSubid05 state 0 runs xor a; interactionSetAnimation before
        // objectSetVisible82, so the preloaded guard is already facing up.
        corridorGuard.SetScriptAnimation(_database.InitialEscortAnimation);
        corridorGuard.SetBlocksLink(false);
        _corridorRunner = _singleRunner;
        _corridorRunner.Start(_database.CorridorCommands);
        _corridorRunner.SetInitialMotionRegisters(
            CorridorGuard, _record.NormalSpeed, 0x00);

        if (room != _record.CorridorRoom1)
            return;
        for (int occurrence = 0; occurrence < 4; occurrence++)
        {
            NpcCharacter guard = Spawn(
                $"SideGuard{occurrence}",
                _database.RequireActor(room, 0x40, 0x03, occurrence));
            _sideGuards.Add(new SideGuardState(guard));
        }
    }

    private void StartThroneRoom()
    {
        _stage = DekuForestPalaceStage.Throne;
        _menusDisabled = true;
        _signal = 0;
        SetInputEnabled(enabled: false);
        _playerInput = PalacePlayerInput.Enter;

        Spawn(RewardGuard, _database.RequireActor(
            _record.ThroneRoom, 0x40, 0x04));
        NpcCharacter escortGuard = Spawn(EscortGuard, _database.RequireActor(
            _record.ThroneRoom, 0x40, 0x06));
        // soldierSubid06 performs the same animation-$00 write before becoming
        // visible; this pose must be present throughout the incoming scroll.
        escortGuard.SetScriptAnimation(_database.InitialEscortAnimation);
        Spawn(Ambi, _database.RequireActor(
            _record.ThroneRoom, 0x4d, 0x00));
        NpcCharacter nayru = Spawn(Nayru, _database.RequireActor(
            _record.ThroneRoom, 0x36, 0x01));
        nayru.SetScriptVisible(false);
        nayru.SetScriptPaletteOverride(_database.PossessedNayruPalette);

        _lanes.StartLane(RewardGuard, _database.RewardGuardCommands);
        _lanes.StartLane(EscortGuard, _database.EscortGuardCommands);
        _lanes.StartLane(Ambi, _database.AmbiCommands);
        _lanes.StartLane(Nayru, _database.NayruCommands);
    }

    private void StartEntranceInput()
    {
        int x = OracleObjectPosition.HighByte(_context.Player.Position.X);
        int difference;
        if (x >= 0x50)
        {
            // $60 is BTN_UP|BTN_LEFT.
            _entranceHorizontal = -1;
            difference = x - 0x50;
        }
        else
        {
            // $50 is BTN_UP|BTN_RIGHT.
            _entranceHorizontal = 1;
            difference = 0x50 - x;
        }
        _playerInputCounter = difference + (difference >> 1);
        _playerInput = PalacePlayerInput.Enter;
    }

    private void AdvancePlayerInput()
    {
        switch (_playerInput)
        {
            case PalacePlayerInput.Enter:
                if (_stage == DekuForestPalaceStage.Throne &&
                    OracleObjectPosition.HighByte(_context.Player.Position.Y) == 0x68)
                {
                    _playerInput = PalacePlayerInput.None;
                    return;
                }

                if (_playerInputCounter > 0)
                {
                    int angle = _entranceHorizontal > 0 ? 0x04 : 0x1c;
                    _context.Player.AdvanceCutsceneSimulatedInput(
                        Vector2I.Up,
                        angle,
                        _record.NormalSpeed,
                        _record.SlowSpeed);
                    _playerInputCounter--;
                }
                else
                {
                    _context.Player.AdvanceCutsceneSimulatedInput(
                        Vector2I.Up,
                        0x00,
                        _record.NormalSpeed,
                        _record.SlowSpeed);
                }
                _context.Transitions.CheckRoomExit(_context.Player);
                break;

            case PalacePlayerInput.Exit:
                if (_playerInputCounter >= _record.ExitIdleFrames &&
                    _playerInputCounter <
                        _record.ExitIdleFrames + _record.ExitDownFrames)
                {
                    // wScrollMode=$00 deliberately prevents a room scroll here.
                    _context.Player.AdvanceCutsceneSimulatedInput(
                        Vector2I.Down,
                        0x10,
                        _record.NormalSpeed,
                        _record.SlowSpeed);
                }
                else
                {
                    _context.Player.AdvanceCutsceneMovement(
                        Vector2.Zero, Vector2I.Zero);
                }
                _playerInputCounter++;
                break;
        }
    }

    private void UpdateSideGuards()
    {
        if (_sideGuards.Count == 0)
            return;

        if (OracleObjectPosition.HighByte(_context.Player.Position.Y) ==
            _record.SideGuardTriggerY)
        {
            foreach (SideGuardState guard in _sideGuards)
            {
                if (guard.Started ||
                    OracleObjectPosition.HighByte(guard.Actor.Position.Y) == 0x28)
                {
                    continue;
                }
                guard.Started = true;
                guard.Counter = _record.SideGuardMoveFrames;
                guard.Angle =
                    OracleObjectPosition.HighByte(guard.Actor.Position.X) == 0x48
                        ? 0x18
                        : 0x08;
            }
        }

        foreach (SideGuardState guard in _sideGuards)
        {
            if (!guard.Started || guard.Counter <= 1)
                continue;
            Vector2 precise = guard.Actor.Position;
            guard.Actor.SetStatePosition(OracleObjectMovement.Shared.ApplySpeed(
                ref precise, _record.NormalSpeed, guard.Angle));
            guard.Counter--;
        }
    }

    private void UpdateCorridorGuardScreenBoundary()
    {
        if (!_actors.TryGetValue(CorridorGuard, out NpcCharacter? guard) ||
            !guard.Active)
        {
            return;
        }

        Vector2 screenPosition = OracleObjectMath.NormalizeSourceScreenPosition(
            _context.Transitions.WorldToGameplayScreen(guard.Position));
        if (OracleObjectMath.IsInsideOriginalScreenBoundary(screenPosition))
            return;

        // soldierSubid05 checks objectCheckWithinScreenBoundary before
        // interactionRunScript on every state-1 update.
        guard.SetActive(false);
        _corridorRunner?.Clear();
    }

    private void UpdateRewardGuardFlight()
    {
        if (!_actors.TryGetValue(RewardGuard, out NpcCharacter? guard))
            return;

        if (_rewardFlightStage == RewardGuardFlightStage.Inactive &&
            _signal == 0x06)
        {
            guard.SetScriptAnimation(guard.BaseRecord.UpAnimation);
            _rewardCounter = _record.RewardJumpDelay;
            _rewardFlightStage = RewardGuardFlightStage.Delay;
            return;
        }

        switch (_rewardFlightStage)
        {
            case RewardGuardFlightStage.Delay:
                if (--_rewardCounter > 0)
                    return;
                _rewardZFixed = 0;
                _rewardSpeedZ = _record.RewardJumpSpeedZ;
                _context.Sound.PlaySound(OracleSoundEngine.SndJump);
                _rewardFlightStage = RewardGuardFlightStage.Airborne;
                break;

            case RewardGuardFlightStage.Airborne:
                if (!OracleObjectMath.UpdateSpeedZ(
                        ref _rewardZFixed,
                        ref _rewardSpeedZ,
                        _record.RewardJumpGravity))
                {
                    guard.SetScriptDrawOffset(new Vector2(0, _rewardZFixed / 256.0f));
                    return;
                }
                guard.SetScriptDrawOffset(Vector2.Zero);
                guard.SetScriptAnimation(guard.BaseRecord.DownAnimation);
                _rewardCounter = _record.RewardLandDelay;
                _rewardFlightStage = RewardGuardFlightStage.LandDelay;
                break;

            case RewardGuardFlightStage.LandDelay:
                if (--_rewardCounter > 0)
                    return;
                _rewardFlightStage = RewardGuardFlightStage.Flying;
                break;

            case RewardGuardFlightStage.Flying:
                MoveActorAtSpeed(
                    RewardGuard, _record.FlightSpeed, 0x10);
                if (guard.Position.Y > _context.Rooms.CurrentRoom.Height + 8)
                {
                    guard.SetActive(false);
                    _rewardFlightStage = RewardGuardFlightStage.Deleted;
                }
                break;
        }
    }

    private void LoadDirectExit()
    {
        _returnRequested = false;
        _directExit = true;
        _lanes.Clear();
        _singleRunner.Clear();
        _actors.Clear();
        _precisePositions.Clear();
        _genericGuards.Clear();
        _sideGuards.Clear();

        OracleRoomData loaded = _context.Rooms.LoadCutsceneRoom(
            _record.Group, _record.EntranceRoom);
        _context.RoomView.SetRoom(loaded.Texture);
        _context.Entities.LoadCutsceneRoom(
            _record.Group, loaded, includeTimePortals: false);
        _context.Transitions.ResetCamera();
        RestoreFade();
        _textboxFlags = 0;

        _context.Player.WarpTo(
            new Vector2(_record.ExitPlayerX, _record.ExitPlayerY),
            recordSafe: false);
        _context.Player.Face(Vector2I.Up);
        SetInputEnabled(enabled: false);

        Spawn(ExitGuard, _database.CreateDirectExitGuard(
            0x07,
            0x28,
            0x48,
            _record.TerminalTextId,
            _record.TerminalText));
        Spawn(OtherExitGuard, _database.CreateDirectExitGuard(
            0x02,
            0x28,
            0x58,
            _record.TerminalTextId,
            _record.TerminalText));
        _context.Sound.PlayRoomMusic(_record.Group, _record.EntranceRoom);
        _stage = DekuForestPalaceStage.Exit;
        _singleRunner.Start(_database.ExitGuardCommands);
    }

    private void FinishExitIntroduction()
    {
        _stage = DekuForestPalaceStage.GenericGuards;
        _menusDisabled = false;
        _playerInput = PalacePlayerInput.None;
        AddGenericGuard(RequireActor(ExitGuard));
        AddGenericGuard(RequireActor(OtherExitGuard));
    }

    private NpcCharacter Spawn(string name, NpcRecord record)
    {
        NpcCharacter actor = _context.Entities.Spawn<NpcCharacter>(
            new CutsceneNpcSpawn(record, $"DekuForestPalace{name}"));
        actor.SetAnimationRate(0.0f);
        actor.SetScriptVisible(true);
        _actors.Add(name, actor);
        _precisePositions.Add(name, actor.Position);
        return actor;
    }

    private void AddGenericGuard(NpcCharacter guard)
    {
        guard.SetScriptButtonSensitive(true);
        guard.SetBlocksLink(true);
        _genericGuards.Add(guard);
    }

    private NpcCharacter RequireActor(string actor)
    {
        if (!_actors.TryGetValue(actor, out NpcCharacter? npc))
        {
            throw new InvalidOperationException(
                $"Unknown Ambi palace command actor '{actor}'.");
        }
        return npc;
    }

    private void AnimateAllActors(bool corridor = false)
    {
        foreach (NpcCharacter actor in _actors.Values)
        {
            if (!actor.Active)
                continue;
            actor.AdvanceAnimationUpdates(corridor ? 2 : 1);
        }
    }

    private void RestoreFade()
    {
        _context.RoomView.SetBackgroundFade(Colors.Black, 0.0f);
        _context.Hud.SetHiddenStatusBarFade(Colors.Black, 0.0f);
    }

    private sealed class SideGuardState(NpcCharacter actor)
    {
        public NpcCharacter Actor { get; } = actor;
        public bool Started;
        public int Counter;
        public int Angle;
    }
}

internal enum DekuForestPalaceStage
{
    Inactive,
    GenericGuards,
    Entrance,
    Corridor,
    Throne,
    Exit
}

internal enum PalacePlayerInput
{
    None,
    Enter,
    Exit
}

internal enum RewardGuardFlightStage
{
    Inactive,
    Delay,
    Airborne,
    LandDelay,
    Flying,
    Deleted
}
