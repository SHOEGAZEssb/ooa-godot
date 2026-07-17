using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Owns the possessed-Impa encounter, its preceding room-edge prompt, and the
/// following-Link behavior that persists across scrolling room transitions.
/// </summary>
internal sealed class ImpaIntroEvent : IRoomEvent, ICutsceneCommandHost
{
    private enum Stage
    {
        None,
        LinkInitialize,
        LinkInitialWait,
        LinkHorizontal,
        LinkCenterWait,
        LinkApproach,
        WaitingForScript,
        Following
    }

    private enum HelpStage
    {
        None,
        WaitingAtEdge,
        Script,
        SimulatedInput
    }

    internal enum StoneStage
    {
        None,
        Inert,
        Moved,
        WaitingForApproach,
        SpotJumpHold,
        SpotJumpAir,
        FirstLandingWait,
        FirstText,
        FirstTextPost,
        ApproachStone,
        AtStoneWait,
        SecondJumpHold,
        SecondJumpAir,
        SecondLandingWait,
        SignText,
        SignTextPost,
        LinkSelect,
        LinkFirstAxis,
        LinkAxisWait,
        LinkSecondAxis,
        LinkTargetWait,
        LinkFaceWait,
        PrePushScript,
        WaitingForPush,
        PushStarted,
        PostPushScript
    }

    private enum FakeOctorokStage
    {
        WaitingForSignal,
        SignalWait,
        FleeDelay,
        Moving,
        Finished
    }

    private sealed class FakeOctorokState(
        ImpaIntroEventDatabase.FakeOctorokRecord record,
        NpcCharacter actor)
    {
        public ImpaIntroEventDatabase.FakeOctorokRecord Record { get; } = record;
        public NpcCharacter Actor { get; } = actor;
        public FakeOctorokStage Stage { get; set; }
        public int Counter { get; set; }
    }

    private readonly record struct LinkPathEntry(Vector2I Direction, Vector2 Position);

    private readonly RoomEventContext _context;
    private readonly ImpaIntroEventDatabase _database = new();
    private readonly ImpaIntroEventDatabase.ImpaIntroEventRecord _record;
    private readonly ImpaIntroEventDatabase.ImpaHelpEventRecord _helpRecord;
    private readonly ImpaIntroEventDatabase.ImpaStoneEventRecord _stoneRecord;
    private readonly CutsceneCommandRunner _encounterRunner;
    private readonly CutsceneCommandRunner _helpRunner;
    private readonly CutsceneCommandRunner _stonePrePushRunner;
    private readonly CutsceneCommandRunner _stonePostPushRunner;
    private readonly List<FakeOctorokState> _fakeOctoroks = new();
    private readonly LinkPathEntry[] _linkPath = new LinkPathEntry[16];
    private OracleRoomData? _followRoom;
    private Stage _stage;
    private HelpStage _helpStage;
    private StoneStage _stoneStage;
    private Vector2 _precisePosition;
    private Vector2 _stonePrecisePosition;
    private int _linkPathIndex;
    private int _counter;
    private int _stonePushCounter;
    private int _stoneMoveCounter;
    private int _jumpZFixed;
    private int _jumpSpeedZ;
    private int _encounterSignal;
    private int _stoneSignal;
    private bool _resetFollowerAfterScroll;
    private bool _linkMovesXFirst;
    private bool _pushedRight;
    private bool _waitingNpcInitialized;
    private Vector2I _followerScrollDirection;

    public ImpaIntroEvent(RoomEventContext context)
    {
        _context = context;
        _record = _database.Record;
        _helpRecord = _database.HelpRecord;
        _stoneRecord = _database.StoneRecord;
        _encounterRunner = new CutsceneCommandRunner(this);
        _helpRunner = new CutsceneCommandRunner(this);
        _stonePrePushRunner = new CutsceneCommandRunner(this);
        _stonePostPushRunner = new CutsceneCommandRunner(this);
        _context.Transitions.ScrollingTransitionFinished += OnScrollingTransitionFinished;
    }

    public bool HasState => _stage != Stage.None || _encounterRunner.Active ||
        _helpStage != HelpStage.None || _helpRunner.Active ||
        _stonePrePushRunner.Active || _stonePostPushRunner.Active || StoneNeedsUpdates;
    public bool BlocksGameplay =>
        _encounterRunner.Active || _stage is not (Stage.None or Stage.Following) ||
        _helpStage is HelpStage.Script or HelpStage.SimulatedInput ||
        _stonePrePushRunner.Active || _stonePostPushRunner.Active || StoneBlocksGameplay;
    internal bool Following => _stage == Stage.Following;
    internal bool HelpWaitingAtEdge => _helpStage == HelpStage.WaitingAtEdge;
    internal bool UpdatesDuringTransition =>
        Following && _context.Transitions.ScrollActive && _resetFollowerAfterScroll;
    internal bool CanTransferFollowing =>
        Following && _context.Transitions.ScrollActive && _followRoom is not null && Actor is not null;
    internal int Counter => _stage == Stage.WaitingForScript
        ? _encounterRunner.Counter
        : _helpRunner.Active ? _helpRunner.Counter
        : _stonePrePushRunner.Active ? _stonePrePushRunner.Counter
        : _stonePostPushRunner.Active ? _stonePostPushRunner.Counter
        : _counter;
    internal int EncounterCommandIndex =>
        _encounterRunner.CurrentCommand?.Source.CommandIndex ?? -1;
    internal int StonePushCounter => _stonePushCounter;
    internal int StoneMoveCounter => _stoneMoveCounter;
    internal bool WaitingNpcInitialized =>
        _stoneStage == StoneStage.WaitingForPush && _waitingNpcInitialized;
    internal StoneStage CurrentStoneStage => _stoneStage;
    internal ImpaIntroEventDatabase Database => _database;
    internal NpcCharacter? Actor { get; set; }
    internal NpcCharacter? StoneActor { get; private set; }
    internal IReadOnlyList<NpcCharacter> FakeOctoroks
    {
        get
        {
            var actors = new List<NpcCharacter>(_fakeOctoroks.Count);
            foreach (FakeOctorokState state in _fakeOctoroks)
                actors.Add(state.Actor);
            return actors;
        }
    }

    public bool MatchesEncounter(int group, OracleRoomData room) =>
        group == _record.Group && room.Id == _record.Room;

    public bool MatchesHelp(int group, OracleRoomData room) =>
        group == _helpRecord.Group && room.Id == _helpRecord.Room;

    public bool MatchesStone(int group, OracleRoomData room) =>
        group == _stoneRecord.Actor.Group && room.Id == _stoneRecord.Actor.Room;

    public void StartHelp()
    {
        _helpRunner.Clear();
        if (_context.Rooms.SaveData.HasRoomFlag(
            _helpRecord.Group,
            _helpRecord.Room,
            (byte)_helpRecord.RoomFlag))
        {
            return;
        }
        _helpStage = HelpStage.WaitingAtEdge;
        _counter = 0;
    }

    public void StartEncounter(OracleRoomData room)
    {
        _encounterRunner.Clear();
        _encounterSignal = 0;
        Actor = _context.RequireNpc(
            _record.Group,
            _record.Room,
            _record.InteractionId,
            _record.SubId,
            "INTERAC_IMPA_IN_CUTSCENE");

        if (_context.Rooms.SaveData.HasRoomFlag(
            _record.Group, _record.Room, (byte)_record.RoomFlag))
        {
            Actor.SetActive(false);
            return;
        }

        Actor.SetSpritePalette(_database.PossessedPalette);
        Actor.SetDirectionalAnimations(
            _record.UpAnimation,
            _record.RightAnimation,
            _record.DownAnimation,
            _record.LeftAnimation);
        _fakeOctoroks.Clear();
        foreach (ImpaIntroEventDatabase.FakeOctorokRecord record in _database.Octoroks)
        {
            NpcCharacter actor = _context.Entities.Spawn<NpcCharacter>(new CutsceneNpcSpawn(
                record.ToNpcRecord(_record.Group, _record.Room),
                $"FakeOctorok_{record.Index}"));
            actor.SetScriptAnimation(record.InitialAnimation);
            _fakeOctoroks.Add(new FakeOctorokState(record, actor));
        }

        _followRoom = room;
        _stage = Stage.LinkInitialize;
        _counter = 0;
        _context.Player.BeginCutsceneControl();
        _encounterRunner.Start(_database.EncounterCommands);
    }

    public void StartStoneRoom()
    {
        _stonePrePushRunner.Clear();
        _stonePostPushRunner.Clear();
        _stoneSignal = 0;
        ImpaIntroEventDatabase.ImpaStoneActorRecord stone = _stoneRecord.Actor;
        byte roomFlags = _context.Rooms.SaveData.GetRoomFlags(stone.Group, stone.Room);
        bool moved = (roomFlags & (stone.LeftRoomFlag | stone.RightRoomFlag)) != 0;
        int x = moved
            ? (roomFlags & stone.LeftRoomFlag) != 0 ? stone.LeftX : stone.RightX
            : stone.InitialX;
        int y = moved ? stone.MovedY : stone.InitialY;

        RestoreStoneTiles();
        if (moved)
            ApplyMovedStoneTile(x);

        StoneActor = _context.Entities.Spawn<NpcCharacter>(new CutsceneNpcSpawn(
            stone.ToNpcRecord(y, x),
            moved ? "MovedTriforceStone" : "TriforceStone",
            Solid: !moved));
        StoneActor.SetSourceGrayscaleInverted(stone.SourceGrayscaleInverted);
        StoneActor.SetSpritePalette(_database.StonePalette);
        StoneActor.SetScriptAnimation(stone.Animation);
        StoneActor.SetCollisionRadii(stone.CollisionRadiusY, stone.CollisionRadiusX);
        // objectSetVisible83 keeps INTERAC_TRIFORCE_STONE at priority 3;
        // follower Impa is continuously assigned priority 1 or 2 relative to Link.
        StoneActor.SetFixedDrawPriority(NpcCharacter.FixedLowPriorityZIndex);
        _stonePrecisePosition = StoneActor.Position;
        _stonePushCounter = _stoneRecord.Timing.PushHoldFrames;
        _stoneMoveCounter = 0;
        _waitingNpcInitialized = false;
        _stoneStage = moved
            ? StoneStage.Moved
            : Following ? StoneStage.WaitingForApproach : StoneStage.Inert;
    }

    public void LeaveStoneRoom()
    {
        _stonePrePushRunner.Clear();
        _stonePostPushRunner.Clear();
        _stoneSignal = 0;
        StoneActor = null;
        _stoneStage = StoneStage.None;
        _stonePushCounter = 0;
        _stoneMoveCounter = 0;
        _waitingNpcInitialized = false;
    }

    public void UpdateFrame()
    {
        if (_stage != Stage.None || _encounterRunner.Active)
            UpdateEncounterFrame();
        else
            UpdateHelpFrame(Input.IsActionPressed("move_up"));
    }

    public void UpdateFollower() => UpdateFollowingActor(_context.Player.Position);

    public void UpdateDuringTransition() =>
        UpdateFollowingActor(_context.Transitions.ScrollLinkPositionInDestination);

    public void TransferFollowingActor(int group, OracleRoomData room)
    {
        NpcCharacter outgoing = Actor!;
        Vector2 offset = _context.Transitions.ScrollDirection == Vector2I.Up
            ? new Vector2(0, _followRoom!.Height)
            : _context.Transitions.ScrollDirection == Vector2I.Right
                ? new Vector2(-_followRoom!.Width, 0)
                : _context.Transitions.ScrollDirection == Vector2I.Down
                    ? new Vector2(0, -_followRoom!.Height)
                    : new Vector2(_followRoom!.Width, 0);

        NpcDatabase.NpcRecord record = Actor!.Record with
        {
            Group = group,
            Room = room.Id,
            Y = (int)(Actor.Position.Y + offset.Y),
            X = (int)(Actor.Position.X + offset.X)
        };
        NpcCharacter incoming = _context.Entities.Spawn<NpcCharacter>(new CutsceneNpcSpawn(
            record, "FollowingImpa", Talkable: true, Solid: true));
        incoming.SetSpritePalette(_database.PossessedPalette);
        incoming.Position = Actor.Position + offset;
        incoming.SetBlocksLink(false);
        incoming.SetFacingDirection(Actor.FacingVector);
        // objectSetReservedBit1 keeps one interaction slot alive across the
        // original reload. Our room lists require a destination-owned actor,
        // so retire the superseded outgoing rendering copy immediately.
        outgoing.SetActive(false);
        Actor = incoming;
        for (int index = 0; index < _linkPath.Length; index++)
        {
            _linkPath[index] = _linkPath[index] with
            {
                Position = _linkPath[index].Position + offset
            };
        }
        _resetFollowerAfterScroll = true;
        _followerScrollDirection = _context.Transitions.ScrollDirection;
        _followRoom = room;
    }

    public void SuppressPlacedActorIfCompleted(int group, OracleRoomData room)
    {
        if (!MatchesEncounter(group, room) ||
            !_context.Rooms.SaveData.HasRoomFlag(
                _record.Group, _record.Room, (byte)_record.RoomFlag))
        {
            return;
        }
        _context.DeactivateNpcs(_record.InteractionId, _record.SubId);
    }

    public void StopFollowing()
    {
        if (_stage == Stage.Following)
            _stage = Stage.None;
        _stonePrePushRunner.Clear();
        _stonePostPushRunner.Clear();
        _stoneSignal = 0;
        _stoneStage = StoneStage.None;
    }

    public void Cancel()
    {
        foreach (FakeOctorokState state in _fakeOctoroks)
            state.Actor.SetActive(false);
        _fakeOctoroks.Clear();
        Actor = null;
        StoneActor = null;
        _followRoom = null;
        _resetFollowerAfterScroll = false;
        _encounterRunner.Clear();
        _helpRunner.Clear();
        _stonePrePushRunner.Clear();
        _stonePostPushRunner.Clear();
        _encounterSignal = 0;
        _stoneSignal = 0;
        _stage = Stage.None;
        _helpStage = HelpStage.None;
        _stoneStage = StoneStage.None;
        _stonePushCounter = 0;
        _stoneMoveCounter = 0;
        _waitingNpcInitialized = false;
        _context.Player.SetCutscenePushing(false);
    }

    private void UpdateEncounterFrame()
    {
        EnsureImpaMusicOverride();
        // INTERAC_IMPA_IN_CUTSCENE runs before its parsed fake Octoroks and
        // linkCutscene1. Keep that object ordering authoritative here.
        _encounterRunner.AdvanceFrame();
        UpdateFakeOctoroks();
        switch (_stage)
        {
            case Stage.LinkInitialize:
                // linkCutscene1 state 0 occupies its own object update.
                _context.Player.Face(Vector2I.Up);
                BeginWait(Stage.LinkInitialWait, _record.LinkWaitFrames);
                break;
            case Stage.LinkInitialWait:
                if (CountDown())
                    _stage = Stage.LinkHorizontal;
                break;
            case Stage.LinkHorizontal:
                if (Mathf.IsEqualApprox(_context.Player.Position.X, _record.TargetX))
                {
                    BeginWait(Stage.LinkCenterWait, _record.CenterWaitFrames);
                    break;
                }
                EnsureObjectSpeed(_record.LinkSpeed, 0x28, "Link's room 0:6a approach");
                int horizontal = _context.Player.Position.X < _record.TargetX ? 1 : -1;
                _context.Player.AdvanceCutsceneMovement(
                    new Vector2(horizontal, 0),
                    horizontal > 0 ? Vector2I.Right : Vector2I.Left);
                break;
            case Stage.LinkCenterWait:
                if (CountDown())
                {
                    _counter = _record.ApproachFrames;
                    _context.Player.Face(Vector2I.Up);
                    _stage = Stage.LinkApproach;
                }
                break;
            case Stage.LinkApproach:
                EnsureObjectSpeed(_record.LinkSpeed, 0x28, "Link's room 0:6a approach");
                _context.Player.AdvanceCutsceneMovement(Vector2.Up, Vector2I.Up);
                _counter--;
                if (_counter == 0)
                {
                    // linkCutscene1 writes cfd0=$01 after Impa and the fake
                    // Octoroks have already updated. They observe it on the
                    // following original-engine update.
                    _encounterSignal = 0x01;
                    _context.Sound.PlaySound(OracleSoundEngine.SndClink);
                    _stage = Stage.WaitingForScript;
                }
                break;
            case Stage.WaitingForScript:
                break;
            case Stage.Following:
                bool directionalInput =
                    Input.IsActionPressed("move_up") ||
                    Input.IsActionPressed("move_right") ||
                    Input.IsActionPressed("move_down") ||
                    Input.IsActionPressed("move_left");
                UpdateStoneFrame(
                    directionalInput,
                    Input.IsActionPressed("move_down"),
                    Input.IsActionPressed("move_right"));
                break;
        }
    }

    internal void UpdateStoneFrame(
        bool pushing,
        bool downPressed = false,
        bool rightPressed = false)
    {
        ImpaIntroEventDatabase.ImpaStoneActorRecord stone = _stoneRecord.Actor;
        ImpaIntroEventDatabase.ImpaStoneTimingRecord timing = _stoneRecord.Timing;

        if (_stoneStage == StoneStage.PushStarted || _stonePostPushRunner.Active)
            _context.Player.SetCutscenePushing(true);

        // INTERAC_TRIFORCE_STONE calls objectPreventLinkFromPassing at the
        // start of substates 0 and 1. Once movement finishes, collision $0f
        // in the destination tile replaces the interaction-side resolver.
        if (StoneActor is not null && _stoneMoveCounter == 0 &&
            (_context.Rooms.SaveData.GetRoomFlags(stone.Group, stone.Room) &
                (stone.LeftRoomFlag | stone.RightRoomFlag)) == 0)
        {
            PreventLinkFromPassing(
                StoneActor, stone.CollisionRadiusY, stone.CollisionRadiusX);
        }

        if (_stoneStage is StoneStage.None or StoneStage.Inert or StoneStage.Moved)
        {
            UpdateFollower();
            return;
        }

        bool prePushScriptWasActive = _stonePrePushRunner.Active;
        if (prePushScriptWasActive)
        {
            _stonePrePushRunner.AdvanceFrame();
            // A completed script returns carry to native substate $0a, which
            // installs rungenericnpc but does not execute substate $0b until
            // the next interaction update.
            if (!_stonePrePushRunner.Active)
                return;
        }

        if (_stoneMoveCounter > 0 && _stoneStage != StoneStage.PushStarted)
            AdvanceStoneMovement();

        bool postPushScriptWasActive = _stonePostPushRunner.Active;
        if (postPushScriptWasActive)
        {
            _stonePostPushRunner.AdvanceFrame();
            // scriptend returns carry to native substate $0c, which restores
            // Link and following immediately without dispatching another
            // stone-event stage in this update.
            if (!_stonePostPushRunner.Active)
                return;
        }

        switch (_stoneStage)
        {
            case StoneStage.WaitingForApproach:
                if (_context.Player.Position.Y >= stone.ApproachY ||
                    _context.Player.Position.X >= stone.ApproachX)
                {
                    UpdateFollower();
                    return;
                }
                BeginStoneReaction();
                break;
            case StoneStage.SpotJumpHold:
                if (CountDown())
                    _stoneStage = StoneStage.SpotJumpAir;
                break;
            case StoneStage.SpotJumpAir:
                if (AdvanceImpaJump())
                {
                    _counter = timing.FirstLandingWait;
                    _stoneStage = StoneStage.FirstLandingWait;
                }
                break;
            case StoneStage.FirstLandingWait:
                if (CountDown())
                {
                    _counter = timing.FirstTextPostFrames;
                    _context.ShowDialogue(_stoneRecord.Texts.First.Message);
                    _stoneStage = StoneStage.FirstText;
                }
                break;
            case StoneStage.FirstText:
                if (!_context.DialogueOpen)
                    _stoneStage = StoneStage.FirstTextPost;
                break;
            case StoneStage.FirstTextPost:
                if (CountDown())
                {
                    Actor!.SetAnimationRate(3.0f);
                    _precisePosition = Actor.Position;
                    _stoneStage = StoneStage.ApproachStone;
                }
                break;
            case StoneStage.ApproachStone:
                AdvanceImpaTowardStone();
                break;
            case StoneStage.AtStoneWait:
                if (CountDown())
                {
                    _counter = timing.SecondHoldFrames;
                    _jumpZFixed = 0;
                    _jumpSpeedZ = timing.SecondJumpSpeedZ;
                    _stoneStage = StoneStage.SecondJumpHold;
                }
                break;
            case StoneStage.SecondJumpHold:
                if (CountDown())
                    _stoneStage = StoneStage.SecondJumpAir;
                break;
            case StoneStage.SecondJumpAir:
                if (AdvanceImpaJump())
                {
                    _counter = timing.SecondLandingWait;
                    _stoneStage = StoneStage.SecondLandingWait;
                }
                break;
            case StoneStage.SecondLandingWait:
                if (CountDown())
                {
                    _counter = timing.SignTextPostFrames;
                    _context.ShowDialogue(_stoneRecord.Texts.Sign.Message);
                    _stoneStage = StoneStage.SignText;
                }
                break;
            case StoneStage.SignText:
                if (!_context.DialogueOpen)
                    _stoneStage = StoneStage.SignTextPost;
                break;
            case StoneStage.SignTextPost:
                if (CountDown())
                {
                    // Native substate $09 writes cfd0=$02 and installs the
                    // script. linkCutscene2 observes the signal later in this
                    // same original-engine update; the script runs next time.
                    _stoneSignal = 0x02;
                    _precisePosition = Actor!.Position;
                    _stonePrePushRunner.Start(_database.StonePrePushCommands);
                    _stoneStage = StoneStage.LinkSelect;
                }
                break;
            case StoneStage.LinkSelect:
                _linkMovesXFirst = _context.Player.Position.Y > stone.TargetY;
                FaceLinkTowardStoneAxis(_linkMovesXFirst);
                _stoneStage = StoneStage.LinkFirstAxis;
                break;
            case StoneStage.LinkFirstAxis:
                if (AdvanceLinkTowardStoneAxis(_linkMovesXFirst))
                {
                    _counter = timing.LinkAxisWaitFrames;
                    _stoneStage = StoneStage.LinkAxisWait;
                }
                break;
            case StoneStage.LinkAxisWait:
                if (CountDown())
                {
                    FaceLinkTowardStoneAxis(!_linkMovesXFirst);
                    _stoneStage = StoneStage.LinkSecondAxis;
                }
                break;
            case StoneStage.LinkSecondAxis:
                if (AdvanceLinkTowardStoneAxis(!_linkMovesXFirst))
                {
                    _context.Player.Face(Vector2I.Up);
                    _counter = timing.LinkTargetWaitFrames;
                    _stoneStage = StoneStage.LinkTargetWait;
                }
                break;
            case StoneStage.LinkTargetWait:
                if (CountDown())
                {
                    _counter = timing.LinkFaceWaitFrames;
                    _stoneStage = StoneStage.LinkFaceWait;
                }
                break;
            case StoneStage.LinkFaceWait:
                if (CountDown())
                {
                    // linkCutscene2 writes cfd0=$03 after Impa's interaction
                    // has already run, so her memory gate opens next update.
                    _stoneSignal = 0x03;
                    _stoneStage = StoneStage.PrePushScript;
                }
                break;
            case StoneStage.PrePushScript:
                break;
            case StoneStage.WaitingForPush:
                UpdateWaitingImpaAsNpc();
                PreventLinkFromLeavingStoneRoom(downPressed, rightPressed);
                UpdateStonePushAttempt(pushing);
                break;
            case StoneStage.PushStarted:
                // On this update the stone reaches substate 1, Link initializes
                // linkCutscene6, and the earlier retained Impa slot observes
                // cfd0=$06 before loading her response script.
                AdvanceStoneMovement();
                _stoneSignal = 0x06;
                _precisePosition = Actor!.Position;
                _stonePostPushRunner.Start(_database.StonePostPushCommands);
                _stoneStage = StoneStage.PostPushScript;
                break;
            case StoneStage.PostPushScript:
                break;
        }
    }

    private bool StoneNeedsUpdates =>
        _stoneStage is not (StoneStage.None or StoneStage.Inert or StoneStage.Moved);

    private bool StoneBlocksGameplay =>
        _stoneStage is not (
            StoneStage.None or StoneStage.Inert or StoneStage.Moved or
            StoneStage.WaitingForApproach or StoneStage.WaitingForPush);

    private void BeginStoneReaction()
    {
        ImpaIntroEventDatabase.ImpaStoneActorRecord stone = _stoneRecord.Actor;
        ImpaIntroEventDatabase.ImpaStoneTimingRecord timing = _stoneRecord.Timing;
        _context.Player.BeginCutsceneControl();
        _context.Player.Face(DirectionToward(_context.Player.Position,
            new Vector2(stone.TargetX, stone.TargetY)));
        Actor!.SetBlocksLink(false);
        Actor.SetFacingDirection(DirectionToward(
            Actor.Position, new Vector2(stone.TargetX, stone.TargetY)));
        _precisePosition = Actor.Position;
        _jumpZFixed = 0;
        _jumpSpeedZ = timing.SpotJumpSpeedZ;
        _counter = timing.SpotHoldFrames;
        _stoneStage = StoneStage.SpotJumpHold;
        _context.Sound.PlaySound(_stoneRecord.Sounds.Spot);
    }

    private bool AdvanceImpaJump()
    {
        bool landed = OracleObjectMath.UpdateSpeedZ(
            ref _jumpZFixed,
            ref _jumpSpeedZ,
            _stoneRecord.Timing.Gravity);
        Actor!.SetScriptDrawOffset(landed
            ? Vector2.Zero
            : new Vector2(0, _jumpZFixed / 256.0f));
        return landed;
    }

    private void AdvanceImpaTowardStone()
    {
        ImpaIntroEventDatabase.ImpaStoneActorRecord stone = _stoneRecord.Actor;
        float speed = SpeedPerFrame(_stoneRecord.Timing.ApproachSpeed);
        Vector2 target = new(stone.TargetX, stone.TargetY);
        int angle = OracleObjectMath.AngleToward(_precisePosition, target);
        _precisePosition += OracleObjectMath.VectorFromAngle32(angle) * speed;
        Actor!.Position = OracleObjectMath.ToPixelPosition(_precisePosition);

        if (Mathf.Abs(Actor.Position.X - target.X) > stone.CloseRadius ||
            Mathf.Abs(Actor.Position.Y - target.Y) > stone.CloseRadius)
        {
            return;
        }

        _precisePosition = target;
        Actor.Position = target;
        Actor.SetAnimationRate(1.0f);
        Actor.SetFacingDirection(Vector2I.Up);
        _counter = _stoneRecord.Timing.StoneWaitFrames;
        _stoneStage = StoneStage.AtStoneWait;
    }

    private bool AdvanceLinkTowardStoneAxis(bool horizontal)
    {
        ImpaIntroEventDatabase.ImpaStoneActorRecord stone = _stoneRecord.Actor;
        Vector2 position = _context.Player.Position;
        float target = horizontal ? stone.LinkTargetX : stone.LinkTargetY;
        float coordinate = horizontal ? position.X : position.Y;
        if (Mathf.IsEqualApprox(coordinate, target))
            return true;

        float movement = SpeedPerFrame(_stoneRecord.Timing.LinkSpeed) *
            Math.Sign(target - coordinate);
        if (Mathf.Abs(target - coordinate) < Mathf.Abs(movement))
            movement = target - coordinate;
        Vector2 delta = horizontal ? new Vector2(movement, 0) : new Vector2(0, movement);
        _context.Player.AdvanceCutsceneMovement(delta,
            horizontal
                ? movement > 0 ? Vector2I.Right : Vector2I.Left
                : movement > 0 ? Vector2I.Down : Vector2I.Up);
        return false;
    }

    private void FaceLinkTowardStoneAxis(bool horizontal)
    {
        ImpaIntroEventDatabase.ImpaStoneActorRecord stone = _stoneRecord.Actor;
        Vector2 position = _context.Player.Position;
        _context.Player.Face(horizontal
            ? position.X < stone.LinkTargetX ? Vector2I.Right : Vector2I.Left
            : position.Y < stone.LinkTargetY ? Vector2I.Down : Vector2I.Up);
    }

    private void BeginWaitingForStonePush()
    {
        _context.Player.EndCutsceneControl();
        _context.Player.Face(Vector2I.Up);
        // The completed move-away script is already on animation $01. Setting
        // impaScript_waitForRockToBeMoved does not reset that animation, and
        // its rungenericnpc command is not executed until substate $0b's next
        // interaction update.
        Actor!.SetDialogue(0, string.Empty, canFace: false);
        Actor.SetBlocksLink(false);
        _waitingNpcInitialized = false;
        _stonePushCounter = _stoneRecord.Timing.PushHoldFrames;
        _stoneStage = StoneStage.WaitingForPush;
    }

    private void UpdateWaitingImpaAsNpc()
    {
        if (_waitingNpcInitialized)
        {
            // interactionAnimateAsNpc animates first, then resolves Link's
            // overlap and updates draw priority. Animation/priority are handled
            // by NpcCharacter.UpdateNpc immediately before this room event.
            PreventLinkFromPassing(
                Actor!, NpcCharacter.CollisionRadius, NpcCharacter.CollisionRadius);
            return;
        }

        // On the first substate-$0b update, interactionAnimateAsNpc runs before
        // genericNpcScript's initcollisions. Impa's interaction radii are still
        // zero here, so only Link's own $06 radius participates in that one
        // overlap check.
        PreventLinkFromPassing(Actor!, 0.0f, 0.0f);

        // rungenericnpc TX_010b redirects to genericNpcScript. Its first
        // command is initcollisions: retain animation $01, install $06/$06,
        // and become A-button sensitive without turning to face Link.
        Actor!.SetCollisionRadii(NpcCharacter.CollisionRadius, NpcCharacter.CollisionRadius);
        Actor.SetDialogue(
            _stoneRecord.Texts.Talk.Id,
            _stoneRecord.Texts.Talk.Message,
            canFace: false);
        Actor.SetBlocksLink(true);
        _waitingNpcInitialized = true;
    }

    private void PreventLinkFromLeavingStoneRoom(bool downPressed, bool rightPressed)
    {
        ImpaIntroEventDatabase.ImpaStoneActorRecord stone = _stoneRecord.Actor;
        Vector2 position = _context.Player.Position;
        if (position.Y >= stone.LeaveY && downPressed)
        {
            _context.Player.SetScriptedPosition(new Vector2(position.X, stone.LeaveY));
            _context.ShowDialogue(_stoneRecord.Texts.Leave.Message);
        }
        else if (position.X >= stone.LeaveX && rightPressed)
        {
            _context.Player.SetScriptedPosition(new Vector2(stone.LeaveX, position.Y));
            _context.ShowDialogue(_stoneRecord.Texts.Leave.Message);
        }
    }

    private void UpdateStonePushAttempt(bool pushing)
    {
        bool centeredPush = CanPushStone(pushing);
        _context.Player.SetCutscenePushing(centeredPush);
        if (!centeredPush)
        {
            _stonePushCounter = _stoneRecord.Timing.PushHoldFrames;
            return;
        }

        _stonePushCounter--;
        if (_stonePushCounter > 0)
            return;
        BeginStonePush();
    }

    private bool CanPushStone(bool pushing)
    {
        if (!pushing || StoneActor is null ||
            Input.IsActionPressed("attack") || Input.IsActionPressed("item"))
            return false;
        Vector2I facing = _context.Player.FacingVector;
        if (facing != Vector2I.Left && facing != Vector2I.Right)
            return false;
        Vector2 delta = StoneActor.Position - _context.Player.Position;
        bool centered = Mathf.Abs(delta.Y) <= 4.0f || Mathf.Abs(delta.X) <= 4.0f;
        return Mathf.Abs(delta.X) < 0x11 &&
            _context.Player.Position.Y < 0x2a && centered;
    }

    private void BeginStonePush()
    {
        // objectCheckLinkWithinDistance derives the stone angle from which
        // side Link occupies, rather than trusting his facing direction.
        _pushedRight = _context.Player.Position.X < StoneActor!.Position.X;
        _stonePrecisePosition = StoneActor!.Position;
        _stoneMoveCounter = _stoneRecord.Timing.StoneMoveFrames;
        Actor!.SetDialogue(0, string.Empty, canFace: false);
        Actor.SetBlocksLink(false);
        _waitingNpcInitialized = false;
        _context.Player.BeginCutsceneControl();
        _context.Player.Face(_pushedRight ? Vector2I.Right : Vector2I.Left);
        _context.Player.SetCutscenePushing(true);
        _stoneStage = StoneStage.PushStarted;
        _context.Sound.PlaySound(_stoneRecord.Sounds.Push);
    }

    private void AdvanceStoneMovement()
    {
        if (_stoneMoveCounter <= 0)
            return;
        Vector2 direction = _pushedRight ? Vector2.Right : Vector2.Left;

        // updateAllObjects runs Link's special object before interactions.
        // linkCutscene6 falls through from initialization to substate 0, so it
        // applies SPEED_80 on its first update. The stone then clamps Link by
        // collision high bytes before decrementing and applying SPEED_40.
        _context.Player.AdvanceCutsceneMovement(
            direction * SpeedPerFrame(_stoneRecord.Timing.LinkPushSpeed),
            _pushedRight ? Vector2I.Right : Vector2I.Left);
        _context.Player.SetCutscenePushing(true);
        PreventLinkFromPassing(
            StoneActor!,
            _stoneRecord.Actor.CollisionRadiusY,
            _stoneRecord.Actor.CollisionRadiusX);

        _stoneMoveCounter--;
        if (_stoneMoveCounter > 0)
        {
            _stonePrecisePosition += direction *
                SpeedPerFrame(_stoneRecord.Timing.StoneSpeed);
            StoneActor!.Position = OracleObjectMath.ToPixelPosition(_stonePrecisePosition);
        }

        if (_stoneMoveCounter == 0)
            FinishStoneMovement();
    }

    private void FinishStoneMovement()
    {
        ImpaIntroEventDatabase.ImpaStoneActorRecord stone = _stoneRecord.Actor;
        int x = _pushedRight ? stone.RightX : stone.LeftX;
        byte flag = (byte)(_pushedRight ? stone.RightRoomFlag : stone.LeftRoomFlag);
        StoneActor!.Position = new Vector2(x, stone.InitialY);
        _stonePrecisePosition = StoneActor.Position;
        _context.Rooms.SaveData.SetRoomFlag(stone.Group, stone.Room, flag);
        ApplyMovedStoneTile(x);
        StoneActor.SetBlocksLink(false);
        _context.Sound.PlaySound(_stoneRecord.Sounds.Stop);
        _context.Sound.PlaySound(_stoneRecord.Sounds.Solve);
    }

    private bool PreventLinkFromPassing(
        NpcCharacter blocker,
        float collisionRadiusY,
        float collisionRadiusX)
        => blocker.PreventPlayerPassing(
            _context.Player, collisionRadiusY, collisionRadiusX);

    private void RestoreStoneTiles()
    {
        ImpaIntroEventDatabase.ImpaStoneActorRecord stone = _stoneRecord.Actor;
        foreach (int x in new[] { stone.LeftX, stone.InitialX, stone.RightX })
        {
            Vector2 point = new(x, stone.MovedY);
            _context.Rooms.CurrentRoom.SetPositionTileAndCollision(
                point,
                _context.Rooms.CurrentRoom.GetOriginalMetatile(point),
                null,
                _context.AnimationTick());
        }
        _context.RoomView.QueueRedraw();
    }

    private void ApplyMovedStoneTile(int x)
    {
        ImpaIntroEventDatabase.ImpaStoneActorRecord stone = _stoneRecord.Actor;
        _context.Rooms.CurrentRoom.SetPositionTileAndCollision(
            new Vector2(x, stone.MovedY),
            (byte)stone.FinalLayoutTile,
            (byte)stone.FinalCollision,
            _context.AnimationTick());
        _context.RoomView.QueueRedraw();
    }

    private static float SpeedPerFrame(int speed) => speed / 40.0f;

    private static Vector2I DirectionToward(Vector2 origin, Vector2 target)
    {
        int angle = (OracleObjectMath.AngleToward(origin, target) + 4) & 0x18;
        Vector2 direction = OracleObjectMath.CardinalVector(angle);
        return new Vector2I(Mathf.RoundToInt(direction.X), Mathf.RoundToInt(direction.Y));
    }

    internal void UpdateHelpFrame(bool upPressed)
    {
        switch (_helpStage)
        {
            case HelpStage.WaitingAtEdge:
                if (!upPressed || _context.Player.Position.Y >= _helpRecord.EdgeY)
                    return;
                _context.Player.BeginCutsceneControl();
                _helpStage = HelpStage.Script;
                _helpRunner.Start(_database.HelpCommands);
                _helpRunner.AdvanceFrame();
                break;
            case HelpStage.Script:
                _helpRunner.AdvanceFrame();
                break;
            case HelpStage.SimulatedInput:
                _counter--;
                _context.Player.AdvanceCutsceneInput(Vector2I.Up);
                _context.Transitions.CheckRoomExit(_context.Player);
                // Beginning the scroll synchronously loads room $6a, which
                // replaces this state with the encounter in the room callback.
                if (_helpStage != HelpStage.SimulatedInput)
                    return;
                if (_counter == 0)
                {
                    _helpStage = HelpStage.None;
                    _context.Player.EndCutsceneControl();
                }
                break;
        }
    }

    private void UpdateFakeOctoroks()
    {
        foreach (FakeOctorokState state in _fakeOctoroks)
        {
            switch (state.Stage)
            {
                case FakeOctorokStage.WaitingForSignal:
                    if (_encounterSignal == 0x01)
                    {
                        state.Counter = state.Record.SignalWaitFrames;
                        state.Stage = FakeOctorokStage.SignalWait;
                    }
                    break;
                case FakeOctorokStage.SignalWait:
                    state.Counter--;
                    if (state.Counter == 0)
                    {
                        state.Actor.SetScriptAnimation(state.Record.FleeAnimation);
                        // impaOctorokCode calls interactionAnimate once, then
                        // interactionAnimate2Times in substates 2 and 3.
                        state.Actor.SetAnimationRate(3.0f);
                        state.Counter = state.Record.FleeCounter;
                        state.Stage = FakeOctorokStage.FleeDelay;
                    }
                    break;
                case FakeOctorokStage.FleeDelay:
                    state.Counter--;
                    if (state.Counter == 0)
                    {
                        // impaOctorokCode plays this before entering the
                        // boundary-checked movement substate.
                        _context.Sound.PlaySound(OracleSoundEngine.SndThrow);
                        state.Stage = FakeOctorokStage.Moving;
                    }
                    break;
                case FakeOctorokStage.Moving:
                    if (!OracleObjectMath.IsInsideOriginalScreenBoundary(state.Actor.Position))
                    {
                        state.Actor.SetActive(false);
                        state.Stage = FakeOctorokStage.Finished;
                        break;
                    }
                    EnsureObjectSpeed(state.Record.Speed, 0x78, "fake Octorok escape");
                    state.Actor.Position +=
                        OracleObjectMath.StrictCardinalVector(state.Record.Angle) * 3.0f;
                    break;
            }
        }
    }

    bool ICutsceneCommandHost.DialogueOpen => _context.DialogueOpen;
    bool ICutsceneCommandHost.IsLinkedGame =>
        _context.Rooms.SaveData.IsLinkedGame;
    int ICutsceneCommandHost.FrameCounter => _context.Entities.FrameCounter;
    ICutsceneCommandTraceSink? ICutsceneCommandHost.TraceSink =>
        _context.CommandTraceSink;
    bool ICutsceneCommandHost.HasActorBinding(CutsceneActorId actor) =>
        actor.Value == "Impa";

    void ICutsceneCommandHost.SetInputEnabled(bool enabled) =>
        throw new InvalidOperationException(
            $"impaScript0 does not set input enabled={enabled}.");

    void ICutsceneCommandHost.SetMenuEnabled(bool enabled)
    {
        if (!_helpRunner.Active || enabled)
        {
            throw new InvalidOperationException(
                $"The active Impa command stream cannot set menu enabled={enabled}.");
        }
        _context.Player.BeginCutsceneControl();
    }

    void ICutsceneCommandHost.SetDisabledObjects(int value)
    {
        if (!_helpRunner.Active || value is not (0x00 or 0x01))
        {
            throw new InvalidOperationException(
                $"The active Impa command stream cannot set wDisabledObjects=${value:x2}.");
        }
        if (value == 0x01)
            _context.Player.BeginCutsceneControl();
    }

    bool ICutsceneCommandHost.GateOpen(string gate) =>
        throw new InvalidOperationException(
            $"impaScript0 does not support named gate '{gate}'.");

    bool ICutsceneCommandHost.MemoryEquals(string binding, int value)
    {
        if (_stonePostPushRunner.Active && binding == "w1Link.angle")
            return (_pushedRight ? 0x08 : 0x18) == value;
        if (binding == "wTmpcfc0.genericCutscene.cfd0")
        {
            return _stonePrePushRunner.Active
                ? _stoneSignal == value
                : _encounterSignal == value;
        }
        throw new InvalidOperationException(
            $"The active Impa command stream cannot read unknown binding '{binding}'.");
    }

    void ICutsceneCommandHost.ShowText(int textId, string message)
    {
        if (_helpRunner.Active)
        {
            if (textId != _helpRecord.TextId || message != _helpRecord.Text)
            {
                throw new InvalidOperationException(
                    $"interaction6b_subid00 requested unknown or divergent TX_{textId:x4}.");
            }
            _context.ShowDialogue(message, _helpRecord.TextboxPosition);
            return;
        }

        if (_stonePrePushRunner.Active)
        {
            ImpaIntroEventDatabase.ImpaStoneText stoneText = textId switch
            {
                var id when id == _stoneRecord.Texts.Request.Id =>
                    _stoneRecord.Texts.Request,
                var id when id == _stoneRecord.Texts.Hesitation.Id =>
                    _stoneRecord.Texts.Hesitation,
                var id when id == _stoneRecord.Texts.Failure.Id =>
                    _stoneRecord.Texts.Failure,
                _ => throw new InvalidOperationException(
                    $"impaScript_moveAwayFromRock requested unknown TX_{textId:x4}.")
            };
            if (message != stoneText.Message)
            {
                throw new InvalidOperationException(
                    $"impaScript_moveAwayFromRock TX_{textId:x4} diverges from imported text.");
            }
            _context.ShowDialogue(message);
            return;
        }

        if (_stonePostPushRunner.Active)
        {
            ImpaIntroEventDatabase.ImpaStoneText thanks = _stoneRecord.Texts.Thanks;
            if (textId != thanks.Id || message != thanks.Message)
            {
                throw new InvalidOperationException(
                    $"impaScript_rockJustMoved requested unknown or divergent TX_{textId:x4}.");
            }
            _context.ShowDialogue(message);
            return;
        }

        string expected = textId switch
        {
            var id when id == _record.TextId => _record.Text,
            var id when id == _record.LinkedTextId => _record.LinkedText,
            _ => throw new InvalidOperationException(
                $"impaScript0 requested unknown TX_{textId:x4}.")
        };
        if (message != expected)
        {
            throw new InvalidOperationException(
                $"impaScript0 TX_{textId:x4} diverges from imported text.");
        }
        _context.ShowDialogue(message);
    }

    void ICutsceneCommandHost.SetActorAnimation(
        string actor,
        int animation,
        string encodedAnimation) =>
        RequireImpaCommandActor(actor).SetScriptAnimation(encodedAnimation);

    void ICutsceneCommandHost.SetActorMovementAnimation(
        string actor,
        int angle,
        string encodedAnimation)
    {
        NpcCharacter impa = RequireImpaCommandActor(actor);
        Vector2 direction = OracleObjectMath.StrictCardinalVector(angle);
        impa.SetFacingDirection(new Vector2I(
            Mathf.RoundToInt(direction.X), Mathf.RoundToInt(direction.Y)));
        _precisePosition = impa.Position;
    }

    void ICutsceneCommandHost.SetActorCollisionRadii(
        string actor,
        int radiusY,
        int radiusX) =>
        RequireImpaCommandActor(actor).SetCollisionRadii(radiusY, radiusX);

    void ICutsceneCommandHost.SetActorButtonSensitive(string actor) =>
        throw new InvalidOperationException(
            $"impaScript0 actor '{actor}' cannot become A-button sensitive.");

    void ICutsceneCommandHost.MoveActorAtSpeed(string actor, int speed, int angle)
    {
        if (_stonePostPushRunner.Active)
        {
            ImpaIntroEventDatabase.ImpaStoneTimingRecord timing = _stoneRecord.Timing;
            if (speed != timing.LeftCorrectionSpeed &&
                speed != timing.ResponseRightSpeed && speed != timing.FinalSpeed)
            {
                throw new InvalidOperationException(
                    $"Impa's room 0:59 post-push script used unexpected speed ${speed:x2}.");
            }
        }
        else
        {
            int expectedSpeed = _stonePrePushRunner.Active
                ? _stoneRecord.Timing.BackAwaySpeed
                : _record.ImpaSpeed;
            string context = _stonePrePushRunner.Active
                ? "Impa's room 0:59 pre-push retreat"
                : "Impa's room 0:6a movement";
            EnsureObjectSpeed(speed, expectedSpeed, context);
        }
        _precisePosition += OracleObjectMath.StrictCardinalVector(angle) *
            SpeedPerFrame(speed);
        RequireImpaCommandActor(actor).Position =
            OracleObjectMath.ToPixelPosition(_precisePosition);
    }

    void ICutsceneCommandHost.SetActorZ(string actor, int zFixed) =>
        throw new InvalidOperationException(
            $"impaScript0 actor '{actor}' cannot set Z to ${zFixed:x4}.");

    void ICutsceneCommandHost.SetActorVisible(string actor, bool visible) =>
        RequireImpaCommandActor(actor).Visible = visible;

    void ICutsceneCommandHost.WriteMemory(string binding, int value)
    {
        if (_stonePrePushRunner.Active &&
            binding == "wTmpcfc0.genericCutscene.cfd0" && value == 0x04)
        {
            _stoneSignal = value;
            return;
        }
        if (_stonePostPushRunner.Active &&
            binding == "wTmpcfc0.genericCutscene.cfd0" && value == 0x07)
        {
            _stoneSignal = value;
            // linkCutscene6 observes cfd0=$07 after Impa's interaction and
            // selects Link's non-pushing animation $02 in this same update.
            _context.Player.AdvanceCutsceneMovement(Vector2.Zero, Vector2I.Down);
            _context.Player.SetCutscenePushing(false);
            return;
        }
        throw new InvalidOperationException(
            $"The active Impa command stream cannot write '{binding}'=${value:x2}.");
    }

    void ICutsceneCommandHost.PlaySound(int sound) =>
        _context.Sound.PlaySound(sound);

    void ICutsceneCommandHost.SetGlobalFlag(int flag) =>
        throw new InvalidOperationException(
            $"impaScript0 cannot set global flag ${flag:x2}.");

    void ICutsceneCommandHost.OrRoomFlag(int flag)
    {
        if (_helpRunner.Active)
        {
            if (flag != _helpRecord.RoomFlag)
            {
                throw new InvalidOperationException(
                    $"interaction6b_subid00 requested room flag ${flag:x2}, " +
                    $"expected ${_helpRecord.RoomFlag:x2}.");
            }
            _context.Rooms.SaveData.SetRoomFlag(
                _helpRecord.Group, _helpRecord.Room, (byte)flag);
            return;
        }

        if (flag != _record.RoomFlag)
        {
            throw new InvalidOperationException(
                $"impaScript0 requested room flag ${flag:x2}, expected ${_record.RoomFlag:x2}.");
        }
        _context.Rooms.SaveData.SetRoomFlag(
            _record.Group, _record.Room, (byte)flag);
    }

    void ICutsceneCommandHost.RunNativeHandler(string handler)
    {
        if (_helpRunner.Active && handler == "installHelpSimulatedInput")
        {
            _counter = _helpRecord.InputUpFrames;
            _helpStage = HelpStage.SimulatedInput;
            return;
        }
        throw new InvalidOperationException(
            $"The active Impa command stream requested unknown native handler '{handler}'.");
    }

    void ICutsceneCommandHost.ScriptEnded()
    {
        if (_helpRunner.Active)
        {
            if (_helpStage != HelpStage.SimulatedInput ||
                !_context.Rooms.SaveData.HasRoomFlag(
                    _helpRecord.Group, _helpRecord.Room, (byte)_helpRecord.RoomFlag))
            {
                throw new InvalidOperationException(
                    "interaction6b_subid00 ended before installing simulated input " +
                    "and setting room flag $40.");
            }
            return;
        }

        if (_stonePrePushRunner.Active)
        {
            if (_stoneSignal != 0x04)
            {
                throw new InvalidOperationException(
                    "impaScript_moveAwayFromRock ended before writing cfd0=$04.");
            }
            BeginWaitingForStonePush();
            return;
        }

        if (_stonePostPushRunner.Active)
        {
            if (_stoneSignal != 0x07)
            {
                throw new InvalidOperationException(
                    "impaScript_rockJustMoved ended before writing cfd0=$07.");
            }
            _context.Player.SetCutscenePushing(false);
            _context.Player.EndCutsceneControl();
            _context.Player.Face(Vector2I.Down);
            Actor!.SetDialogue(0, string.Empty, canFace: false);
            BeginFollowing();
            _stoneStage = StoneStage.Moved;
            return;
        }

        if (!_context.Rooms.SaveData.HasRoomFlag(
            _record.Group, _record.Room, (byte)_record.RoomFlag))
        {
            throw new InvalidOperationException(
                "impaScript0 ended before setting room flag $40.");
        }
        _context.Player.EndCutsceneControl();
        _context.Player.Face(Vector2I.Up);
        BeginFollowing();
    }

    private NpcCharacter RequireImpaCommandActor(string actor)
    {
        if (actor != "Impa" || Actor is null)
            throw new InvalidOperationException(
                $"Unknown active Impa command-stream actor '{actor}'.");
        return Actor;
    }

    private void BeginFollowing()
    {
        Actor!.SetDirectionalAnimations(
            _record.UpAnimation,
            _record.RightAnimation,
            _record.DownAnimation,
            _record.LeftAnimation);
        Actor!.Position = _context.Player.Position;
        Actor.SetBlocksLink(false);
        Actor.SetFacingDirection(_context.Player.FacingVector);
        Actor.UpdateDrawPriority(_context.Player.Position);
        LinkPathEntry initial = new(
            _context.Player.FacingVector,
            OracleObjectMath.ToPixelPosition(_context.Player.Position));
        for (int index = 0; index < _linkPath.Length; index++)
            _linkPath[index] = initial;
        _linkPathIndex = 0;
        _followRoom = _context.Rooms.CurrentRoom;
        _stage = Stage.Following;
    }

    private void UpdateFollowingActor(Vector2 linkPosition)
    {
        EnsureImpaMusicOverride();
        LinkPathEntry current = new(
            _context.Player.FacingVector,
            OracleObjectMath.ToPixelPosition(linkPosition));
        LinkPathEntry indexed = _linkPath[_linkPathIndex];
        if (indexed == current)
            return;

        _linkPathIndex = (_linkPathIndex + 1) & 0x0f;
        LinkPathEntry old = _linkPath[_linkPathIndex];
        _linkPath[_linkPathIndex] = current;
        Actor!.Position = old.Position;
        Actor.SetFacingDirection(old.Direction);
        Actor.UpdateDrawPriority(_context.Player.Position);
    }

    private void EnsureImpaMusicOverride()
    {
        // impaSubid0 forces MUS_FAIRY_FOUNTAIN at volume 3 throughout
        // substates $00-$0d, except on the two screens where Nayru's music is
        // allowed to own the sequence.
        int room = _context.Rooms.CurrentRoom.Id;
        if (room is 0x39 or 0x49)
            return;
        _context.Sound.PlayMusicIfChanged(OracleSoundEngine.MusFairyFountain);
        _context.Sound.SetMusicVolume(3);
    }

    private void OnScrollingTransitionFinished(Vector2I direction)
    {
        if (!Following || !_resetFollowerAfterScroll)
            return;
        if (direction != _followerScrollDirection)
        {
            throw new InvalidOperationException(
                $"Following Impa expected scroll direction {_followerScrollDirection}, got {direction}.");
        }

        // resetFollowingLinkObjectPosition rebuilds w2LinkWalkPath backwards
        // from entry $0f so the follower begins exactly 16 pixels outside the
        // destination edge, as if Link had just walked in from that edge.
        Vector2I movementOffset = direction == Vector2I.Up ? Vector2I.Down
            : direction == Vector2I.Right ? Vector2I.Left
            : direction == Vector2I.Down ? Vector2I.Up
            : Vector2I.Right;
        Vector2 position = OracleObjectMath.ToPixelPosition(_context.Player.Position);
        for (int index = _linkPath.Length - 1; index >= 0; index--)
        {
            position += movementOffset;
            _linkPath[index] = new LinkPathEntry(direction, position);
        }
        Actor!.Position = position;
        Actor.UpdateDrawPriority(_context.Player.Position);
        _linkPathIndex = 0x0f;
        _resetFollowerAfterScroll = false;
    }

    private void BeginWait(Stage stage, int frames)
    {
        _stage = stage;
        _counter = frames;
    }

    private bool CountDown()
    {
        if (_counter > 0)
            _counter--;
        return _counter == 0;
    }

    private static void EnsureObjectSpeed(int actual, int expected, string context)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(
                $"Unsupported {context} speed ${actual:x2}; expected ${expected:x2}.");
        }
    }
}
