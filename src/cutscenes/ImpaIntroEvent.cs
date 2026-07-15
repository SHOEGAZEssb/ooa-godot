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
    private readonly List<FakeOctorokState> _fakeOctoroks = new();
    private readonly LinkPathEntry[] _linkPath = new LinkPathEntry[16];
    private OracleRoomData? _followRoom;
    private Stage _stage;
    private HelpStage _helpStage;
    private Vector2 _precisePosition;
    private int _linkPathIndex;
    private int _counter;
    private bool _resetFollowerAfterScroll;
    private Vector2I _followerScrollDirection;

    public ImpaIntroEvent(RoomEventContext context)
    {
        _context = context;
        _record = _database.Record;
        _helpRecord = _database.HelpRecord;
        _context.Transitions.ScrollingTransitionFinished += OnScrollingTransitionFinished;
    }

    public bool HasState => _stage != Stage.None || _helpStage != HelpStage.None;
    public bool BlocksGameplay =>
        _stage is not (Stage.None or Stage.Following) ||
        _helpStage is HelpStage.Text or HelpStage.PostText or HelpStage.SimulatedInput;
    internal bool Following => _stage == Stage.Following;
    internal bool HelpWaitingAtEdge => _helpStage == HelpStage.WaitingAtEdge;
    internal bool UpdatesDuringTransition =>
        Following && _context.Transitions.ScrollActive && _resetFollowerAfterScroll;
    internal bool CanTransferFollowing =>
        Following && _context.Transitions.ScrollActive && _followRoom is not null && Actor is not null;
    internal int Counter => _counter;
    internal ImpaIntroEventDatabase Database => _database;
    internal NpcCharacter? Actor { get; set; }
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
            record, "FollowingImpa"));
        incoming.SetSpritePalette(_database.PossessedPalette);
        incoming.Position = Actor.Position + offset;
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
    }

    public void Cancel()
    {
        foreach (FakeOctorokState state in _fakeOctoroks)
            state.Actor.SetActive(false);
        _fakeOctoroks.Clear();
        Actor = null;
        _followRoom = null;
        _resetFollowerAfterScroll = false;
        _stage = Stage.None;
        _helpStage = HelpStage.None;
    }

    private void UpdateEncounterFrame()
    {
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
                UpdateFollower();
                break;
        }
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
        Actor!.Position = _context.Player.Position;
        Actor.SetBlocksLink(false);
        Actor.SetFacingDirection(Vector2I.Up);
        Actor.UpdateDrawPriority(_context.Player.Position);
        LinkPathEntry initial = new(
            Vector2I.Up,
            OracleObjectMath.ToPixelPosition(_context.Player.Position));
        for (int index = 0; index < _linkPath.Length; index++)
            _linkPath[index] = initial;
        _linkPathIndex = 0;
        _followRoom = _context.Rooms.CurrentRoom;
        _stage = Stage.Following;
    }

    private void UpdateFollowingActor(Vector2 linkPosition)
    {
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
