using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Owns the possessed-Impa encounter, its preceding room-edge prompt, and the
/// following-Link behavior that persists across scrolling room transitions.
/// </summary>
internal sealed class ImpaIntroEvent : IRoomEvent
{
    private enum Stage
    {
        None,
        LinkInitialize,
        LinkInitialWait,
        LinkHorizontal,
        LinkCenterWait,
        LinkApproach,
        SignalPending,
        ImpaDelay,
        Text,
        PostText,
        SetSpeed,
        StartMovement,
        Moving,
        MovementFinished,
        Following
    }

    private enum HelpStage
    {
        None,
        WaitingAtEdge,
        Text,
        PostText,
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
        RequestLead,
        RequestText,
        RequestPost,
        FirstBackAway,
        BetweenFirstBackAway,
        HesitationText,
        HesitationPost,
        SecondBackAway,
        BetweenSecondBackAway,
        FailureText,
        FailurePost,
        WaitingForPush,
        PushStarted,
        ReactionLead,
        LeftCorrection,
        RightBranchWait,
        CommonWait,
        ResponseRight,
        ResponseWait1,
        ResponseUp,
        ResponseWait2,
        PoseWait,
        ThanksText,
        ThanksPost,
        FinalMove,
        FinishFollowing
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
        _context.Transitions.ScrollingTransitionFinished += OnScrollingTransitionFinished;
    }

    public bool HasState => _stage != Stage.None || _helpStage != HelpStage.None ||
        StoneNeedsUpdates;
    public bool BlocksGameplay =>
        _stage is not (Stage.None or Stage.Following) ||
        _helpStage is HelpStage.Text or HelpStage.PostText or HelpStage.SimulatedInput ||
        StoneBlocksGameplay;
    internal bool Following => _stage == Stage.Following;
    internal bool HelpWaitingAtEdge => _helpStage == HelpStage.WaitingAtEdge;
    internal bool UpdatesDuringTransition =>
        Following && _context.Transitions.ScrollActive && _resetFollowerAfterScroll;
    internal bool CanTransferFollowing =>
        Following && _context.Transitions.ScrollActive && _followRoom is not null && Actor is not null;
    internal int Counter => _counter;
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
    }

    public void StartStoneRoom()
    {
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
        StoneActor = null;
        _stoneStage = StoneStage.None;
        _stonePushCounter = 0;
        _stoneMoveCounter = 0;
        _waitingNpcInitialized = false;
    }

    public void UpdateFrame()
    {
        if (_stage != Stage.None)
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
                    _stage = Stage.SignalPending;
                break;
            case Stage.SignalPending:
                // Impa and the fake Octoroks update before linkCutscene1 in
                // the original object order, so they observe cfd0=$01 on the
                // update after Link writes it.
                SignalFakeOctoroks();
                BeginWait(Stage.ImpaDelay, _record.ImpaWaitFrames);
                break;
            case Stage.ImpaDelay:
                if (CountDown())
                {
                    _stage = Stage.Text;
                    _context.ShowDialogue(_record.Text);
                }
                break;
            case Stage.Text:
                if (!_context.DialogueOpen)
                    BeginWait(Stage.PostText, _record.PostTextFrames);
                break;
            case Stage.PostText:
                if (CountDown())
                    _stage = Stage.SetSpeed;
                break;
            case Stage.SetSpeed:
                // setspeed and movedown each stop this interaction-script
                // update; counter2 movement begins on the following update.
                EnsureObjectSpeed(_record.ImpaSpeed, 0x14, "Impa's room 0:6a movement");
                _stage = Stage.StartMovement;
                break;
            case Stage.StartMovement:
                Actor!.SetFacingDirection(Vector2I.Down);
                _precisePosition = Actor.Position;
                _counter = _record.ImpaMoveFrames;
                _stage = Stage.Moving;
                break;
            case Stage.Moving:
                _counter--;
                if (_counter > 0)
                {
                    // SPEED_080 is 0.5px/update, but only the high coordinate
                    // byte is rendered by the GBC object compositor.
                    _precisePosition += Vector2.Down * 0.5f;
                    Actor!.Position = OracleObjectMath.ToPixelPosition(_precisePosition);
                }
                else
                {
                    _stage = Stage.MovementFinished;
                }
                break;
            case Stage.MovementFinished:
                FinishEncounter();
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

        if (_stoneStage is >= StoneStage.PushStarted and <= StoneStage.ResponseWait2)
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

        if (_stoneMoveCounter > 0 && _stoneStage != StoneStage.PushStarted)
            AdvanceStoneMovement();

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
                    _stoneStage = StoneStage.LinkSelect;
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
                    Actor!.SetScriptAnimation(_record.DownAnimation);
                    _counter = timing.RequestLeadFrames;
                    _stoneStage = StoneStage.RequestLead;
                }
                break;
            case StoneStage.RequestLead:
                if (CountDown())
                {
                    _context.ShowDialogue(_stoneRecord.Texts.Request.Message);
                    _stoneStage = StoneStage.RequestText;
                }
                break;
            case StoneStage.RequestText:
                if (!_context.DialogueOpen)
                {
                    _counter = timing.RequestPostFrames;
                    _stoneStage = StoneStage.RequestPost;
                }
                break;
            case StoneStage.RequestPost:
                if (CountDown())
                {
                    Actor!.SetScriptAnimation(_record.RightAnimation);
                    _precisePosition = Actor.Position;
                    _counter = timing.FirstBackAwayFrames;
                    _stoneStage = StoneStage.FirstBackAway;
                }
                break;
            case StoneStage.FirstBackAway:
                if (AdvanceImpaScriptMovement(Vector2.Left, timing.BackAwaySpeed))
                {
                    _counter = timing.BetweenFirstBackAwayFrames;
                    _stoneStage = StoneStage.BetweenFirstBackAway;
                }
                break;
            case StoneStage.BetweenFirstBackAway:
                if (CountDown())
                {
                    _context.ShowDialogue(_stoneRecord.Texts.Hesitation.Message);
                    _stoneStage = StoneStage.HesitationText;
                }
                break;
            case StoneStage.HesitationText:
                if (!_context.DialogueOpen)
                {
                    _counter = timing.HesitationPostFrames;
                    _stoneStage = StoneStage.HesitationPost;
                }
                break;
            case StoneStage.HesitationPost:
                if (CountDown())
                {
                    _counter = timing.SecondBackAwayFrames;
                    _stoneStage = StoneStage.SecondBackAway;
                }
                break;
            case StoneStage.SecondBackAway:
                if (AdvanceImpaScriptMovement(Vector2.Left, timing.BackAwaySpeed))
                {
                    _counter = timing.BetweenSecondBackAwayFrames;
                    _stoneStage = StoneStage.BetweenSecondBackAway;
                }
                break;
            case StoneStage.BetweenSecondBackAway:
                if (CountDown())
                {
                    _context.ShowDialogue(_stoneRecord.Texts.Failure.Message);
                    _stoneStage = StoneStage.FailureText;
                }
                break;
            case StoneStage.FailureText:
                if (!_context.DialogueOpen)
                {
                    _counter = timing.FailurePostFrames;
                    _stoneStage = StoneStage.FailurePost;
                }
                break;
            case StoneStage.FailurePost:
                if (CountDown())
                    BeginWaitingForStonePush();
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
                _counter = timing.ReactionLeadFrames;
                _stoneStage = StoneStage.ReactionLead;
                break;
            case StoneStage.ReactionLead:
                if (CountDown())
                {
                    _precisePosition = Actor!.Position;
                    if (_pushedRight)
                    {
                        _counter = timing.RightBranchWaitFrames;
                        _stoneStage = StoneStage.RightBranchWait;
                    }
                    else
                    {
                        _counter = timing.LeftCorrectionFrames;
                        _stoneStage = StoneStage.LeftCorrection;
                    }
                }
                break;
            case StoneStage.LeftCorrection:
                if (AdvanceImpaScriptMovement(Vector2.Down, timing.LeftCorrectionSpeed))
                {
                    _counter = timing.CommonWaitFrames;
                    _stoneStage = StoneStage.CommonWait;
                }
                break;
            case StoneStage.RightBranchWait:
                if (CountDown())
                {
                    _counter = timing.CommonWaitFrames;
                    _stoneStage = StoneStage.CommonWait;
                }
                break;
            case StoneStage.CommonWait:
                if (CountDown())
                {
                    _counter = timing.ResponseRightFrames;
                    _stoneStage = StoneStage.ResponseRight;
                }
                break;
            case StoneStage.ResponseRight:
                if (AdvanceImpaScriptMovement(Vector2.Right, timing.ResponseRightSpeed))
                {
                    _counter = timing.ResponseWait1Frames;
                    _stoneStage = StoneStage.ResponseWait1;
                }
                break;
            case StoneStage.ResponseWait1:
                if (!CountDown())
                    break;
                if (_pushedRight)
                    BeginImpaThanksPose();
                else
                {
                    _counter = timing.ResponseUpFrames;
                    _stoneStage = StoneStage.ResponseUp;
                }
                break;
            case StoneStage.ResponseUp:
                if (AdvanceImpaScriptMovement(Vector2.Up, timing.ResponseRightSpeed))
                {
                    _counter = timing.ResponseWait2Frames;
                    _stoneStage = StoneStage.ResponseWait2;
                }
                break;
            case StoneStage.ResponseWait2:
                if (CountDown())
                    BeginImpaThanksPose();
                break;
            case StoneStage.PoseWait:
                if (CountDown())
                {
                    _context.ShowDialogue(_stoneRecord.Texts.Thanks.Message);
                    _stoneStage = StoneStage.ThanksText;
                }
                break;
            case StoneStage.ThanksText:
                if (!_context.DialogueOpen)
                {
                    _counter = timing.ThanksPostFrames;
                    _stoneStage = StoneStage.ThanksPost;
                }
                break;
            case StoneStage.ThanksPost:
                if (CountDown())
                {
                    _precisePosition = Actor!.Position;
                    _counter = timing.FinalMoveFrames;
                    _stoneStage = StoneStage.FinalMove;
                }
                break;
            case StoneStage.FinalMove:
                if (AdvanceImpaScriptMovement(Vector2.Up, timing.FinalSpeed))
                    _stoneStage = StoneStage.FinishFollowing;
                break;
            case StoneStage.FinishFollowing:
                _context.Player.SetCutscenePushing(false);
                _context.Player.EndCutsceneControl();
                _context.Player.Face(Vector2I.Down);
                Actor!.SetDialogue(0, string.Empty, canFace: false);
                BeginFollowing();
                _stoneStage = StoneStage.Moved;
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

    private bool AdvanceImpaScriptMovement(Vector2 direction, int speed)
    {
        _counter--;
        if (_counter <= 0)
            return true;
        _precisePosition += direction * SpeedPerFrame(speed);
        Actor!.Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        return false;
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

    private void BeginImpaThanksPose()
    {
        _context.Player.AdvanceCutsceneMovement(Vector2.Zero, Vector2I.Down);
        _context.Player.SetCutscenePushing(false);
        Actor!.SetScriptAnimation(_record.UpAnimation);
        _counter = _stoneRecord.Timing.PoseWaitFrames;
        _stoneStage = StoneStage.PoseWait;
    }

    private bool PreventLinkFromPassing(
        NpcCharacter blocker,
        float collisionRadiusY,
        float collisionRadiusX)
    {
        Vector2 link = _context.Player.Position;
        Vector2 obstacle = blocker.Position;
        float radiusY = collisionRadiusY + NpcCharacter.LinkCollisionRadius;
        float radiusX = collisionRadiusX + NpcCharacter.LinkCollisionRadius;
        float differenceY = Mathf.Abs(link.Y - obstacle.Y);
        float differenceX = Mathf.Abs(link.X - obstacle.X);
        if (differenceY >= radiusY || differenceX >= radiusX)
            return false;

        // preventObjectHFromPassingObjectD resolves the axis with less overlap;
        // ties are horizontal because its final comparison uses CP without EQ.
        float overlapY = radiusY - differenceY;
        float overlapX = radiusX - differenceX;
        bool horizontal = overlapY >= overlapX;
        float linkCoordinate = horizontal ? link.X : link.Y;
        float obstacleCoordinate = horizontal ? obstacle.X : obstacle.Y;
        int side = linkCoordinate > obstacleCoordinate ? 1 : -1;
        int coordinate = Mathf.FloorToInt(obstacleCoordinate) +
            side * Mathf.RoundToInt(horizontal ? radiusX : radiusY);
        _context.Player.SetScriptedCoordinateHigh(horizontal, coordinate);
        return true;
    }

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
                _counter = _helpRecord.PostTextFrames;
                _helpStage = HelpStage.Text;
                _context.ShowDialogue(_helpRecord.Text, _helpRecord.TextboxPosition);
                break;
            case HelpStage.Text:
                if (_context.DialogueOpen)
                    return;
                _helpStage = HelpStage.PostText;
                AdvanceHelpPostTextCounter();
                break;
            case HelpStage.PostText:
                AdvanceHelpPostTextCounter();
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

    private void AdvanceHelpPostTextCounter()
    {
        _counter--;
        if (_counter != 0)
            return;
        _context.Rooms.SaveData.SetRoomFlag(
            _helpRecord.Group,
            _helpRecord.Room,
            (byte)_helpRecord.RoomFlag);
        _counter = _helpRecord.InputUpFrames;
        _helpStage = HelpStage.SimulatedInput;
    }

    private void UpdateFakeOctoroks()
    {
        foreach (FakeOctorokState state in _fakeOctoroks)
        {
            switch (state.Stage)
            {
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
                        state.Stage = FakeOctorokStage.Moving;
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

    private void SignalFakeOctoroks()
    {
        foreach (FakeOctorokState state in _fakeOctoroks)
        {
            if (state.Stage != FakeOctorokStage.WaitingForSignal)
                continue;
            state.Counter = state.Record.SignalWaitFrames;
            state.Stage = FakeOctorokStage.SignalWait;
        }
    }

    private void FinishEncounter()
    {
        _context.Rooms.SaveData.SetRoomFlag(
            _record.Group, _record.Room, (byte)_record.RoomFlag);
        _context.Player.EndCutsceneControl();
        _context.Player.Face(Vector2I.Up);
        BeginFollowing();
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
