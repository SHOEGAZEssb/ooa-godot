using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Runs room-entry interaction scripts whose sequencing spans Link, dialogue,
/// palettes, actor movement, followers, and hardcoded warps. Supported records
/// include the original Maku Tree disappearance, Ralph's room $39 portal
/// departure, and the possessed-Impa encounter in room $6a.
/// </summary>
public sealed class RoomEventController
{
    private enum Stage
    {
        None,
        IntroDelay,
        IntroText,
        PostIntro,
        Frown,
        Disappearance,
        AhhText,
        PostAhh,
        HelpText,
        FinishDelay
    }

    private enum RalphStage
    {
        None,
        WaitingForScroll,
        IntroDelay,
        Text,
        PostText,
        SetSpeed,
        SetAngle,
        StartMovement,
        Moving,
        MovementFinished,
        SetFlickerCounter,
        PlayPortalSound,
        BeginFlicker,
        Flickering
    }

    private enum ImpaStage
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

    private enum ImpaHelpStage
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

    private readonly RoomSession _rooms;
    private readonly RoomEntityManager _entities;
    private readonly RoomTransitionController _transitions;
    private readonly DialogueBox _dialogue;
    private readonly Player _player;
    private readonly RoomView _roomView;
    private readonly Func<Vector2, Vector2> _worldToScreen;
    private readonly Func<long> _animationTick;
    private readonly MakuTreeCutsceneDatabase _database;
    private readonly MakuTreeCutsceneDatabase.MakuTreeCutsceneRecord _record;
    private readonly RalphPortalEventDatabase.RalphPortalEventRecord _ralphRecord;
    private readonly ImpaIntroEventDatabase _impaDatabase;
    private readonly ImpaIntroEventDatabase.ImpaIntroEventRecord _impaRecord;
    private readonly ImpaIntroEventDatabase.ImpaHelpEventRecord _impaHelpRecord;

    private Stage _stage;
    private RalphStage _ralphStage;
    private OracleRoomData? _eventRoom;
    private NpcCharacter? _makuTree;
    private NpcCharacter? _ralph;
    private NpcCharacter? _impa;
    private OracleRoomData? _impaFollowRoom;
    private readonly System.Collections.Generic.List<FakeOctorokState> _fakeOctoroks = new();
    private readonly LinkPathEntry[] _linkPath = new LinkPathEntry[16];
    private int _linkPathIndex;
    private ImpaStage _impaStage;
    private ImpaHelpStage _impaHelpStage;
    private Vector2 _impaPrecisePosition;
    private bool _resetImpaFollowerAfterScroll;
    private Vector2I _impaFollowerScrollDirection;
    private double _frameAccumulator;
    private int _counter;
    private int _inputFrame;
    private int _paletteHeader;
    private bool _paletteCycling;

    public bool Active => _stage != Stage.None || _ralphStage != RalphStage.None ||
        _impaStage is not (ImpaStage.None or ImpaStage.Following) ||
        _impaHelpStage is ImpaHelpStage.Text or ImpaHelpStage.PostText or
            ImpaHelpStage.SimulatedInput;
    private bool HasEventState => Active || ImpaFollowing ||
        _impaHelpStage != ImpaHelpStage.None;
    internal int CurrentStage => (int)_stage;
    internal bool RalphWaitingForScroll => _ralphStage == RalphStage.WaitingForScroll;
    internal bool RalphFlickering => _ralphStage == RalphStage.Flickering;
    internal bool ImpaFollowing => _impaStage == ImpaStage.Following;
    internal bool ImpaHelpWaitingAtEdge => _impaHelpStage == ImpaHelpStage.WaitingAtEdge;
    internal int ImpaCurrentStage => (int)_impaStage;
    internal NpcCharacter? Impa => _impa;
    internal System.Collections.Generic.IReadOnlyList<NpcCharacter> FakeOctoroks
    {
        get
        {
            var actors = new System.Collections.Generic.List<NpcCharacter>(_fakeOctoroks.Count);
            foreach (FakeOctorokState state in _fakeOctoroks)
                actors.Add(state.Actor);
            return actors;
        }
    }
    internal int Counter => _counter;
    internal int InputFrame => _inputFrame;
    internal int PaletteHeader => _paletteHeader;
    internal bool Completed =>
        _rooms.SaveData.HasGlobalFlag(OracleSaveData.GlobalFlagMakuTreeDisappeared);
    internal bool RalphCompleted =>
        _rooms.SaveData.HasGlobalFlag(OracleSaveData.GlobalFlagRalphEnteredPortal);

    public RoomEventController(
        RoomSession rooms,
        RoomEntityManager entities,
        RoomTransitionController transitions,
        DialogueBox dialogue,
        Player player,
        RoomView roomView,
        Func<Vector2, Vector2> worldToScreen,
        Func<long> animationTick)
    {
        _rooms = rooms;
        _entities = entities;
        _transitions = transitions;
        _dialogue = dialogue;
        _player = player;
        _roomView = roomView;
        _worldToScreen = worldToScreen;
        _animationTick = animationTick;
        _database = new MakuTreeCutsceneDatabase();
        _record = _database.Record;
        _ralphRecord = new RalphPortalEventDatabase().Record;
        _impaDatabase = new ImpaIntroEventDatabase();
        _impaRecord = _impaDatabase.Record;
        _impaHelpRecord = _impaDatabase.HelpRecord;
        if (_ralphRecord.GlobalFlag != OracleSaveData.GlobalFlagRalphEnteredPortal)
        {
            throw new InvalidOperationException(
                $"Ralph portal event uses global flag ${_ralphRecord.GlobalFlag:x2}, expected $40.");
        }
        _entities.RoomEntitiesLoaded += OnRoomEntitiesLoaded;
        _transitions.ScrollingTransitionFinished += OnScrollingTransitionFinished;
    }

    public void Update(double delta)
    {
        if (!HasEventState)
            return;

        _frameAccumulator += delta * 60.0;
        if (_transitions.IsTransitioning)
        {
            // Impa sets Interaction.enabled bit 7 before following Link, so
            // both her interaction and checkUpdateFollowingLinkObject continue
            // to run while ordinary room objects are frozen during scrolling.
            while (ImpaFollowing && _transitions.ScrollActive &&
                _resetImpaFollowerAfterScroll && _frameAccumulator >= 1.0)
            {
                _frameAccumulator -= 1.0;
                UpdateFollowingImpa(_transitions.ScrollLinkPositionInDestination);
            }
            return;
        }

        while (HasEventState && _frameAccumulator >= 1.0)
        {
            _frameAccumulator -= 1.0;
            if (_stage != Stage.None)
                UpdateFrame();
            else if (_ralphStage != RalphStage.None)
                UpdateRalphFrame();
            else if (_impaStage != ImpaStage.None)
                UpdateImpaFrame();
            else
                UpdateImpaHelpFrame(Input.IsActionPressed("move_up"));
        }
    }

    private void OnRoomEntitiesLoaded(int group, OracleRoomData room)
    {
        if (ImpaFollowing && _transitions.ScrollActive && _impaFollowRoom is not null)
        {
            SuppressPlacedImpaIfCompleted(group, room);
            TransferFollowingImpa(group, room);
            return;
        }
        if (HasEventState)
            Cancel();

        if (group == _record.Group && room.Id == _record.Room)
        {
            StartMakuTreeEvent(room);
            return;
        }
        if (group == _ralphRecord.Group && room.Id == _ralphRecord.Room)
        {
            StartRalphEvent();
            return;
        }
        if (group == _impaRecord.Group && room.Id == _impaRecord.Room)
        {
            StartImpaEvent(room);
            return;
        }
        if (group == _impaHelpRecord.Group && room.Id == _impaHelpRecord.Room)
            StartImpaHelpEvent();
    }

    private void StartImpaHelpEvent()
    {
        if (_rooms.SaveData.HasRoomFlag(
            _impaHelpRecord.Group,
            _impaHelpRecord.Room,
            (byte)_impaHelpRecord.RoomFlag))
        {
            return;
        }
        _impaHelpStage = ImpaHelpStage.WaitingAtEdge;
        _counter = 0;
        _frameAccumulator = 0.0;
    }

    private void StartImpaEvent(OracleRoomData room)
    {
        _impa = null;
        foreach (NpcCharacter npc in _entities.Entities<NpcCharacter>())
        {
            if (npc.Record.Id == _impaRecord.InteractionId &&
                npc.Record.SubId == _impaRecord.SubId)
            {
                _impa = npc;
                break;
            }
        }
        if (_impa is null)
        {
            throw new InvalidOperationException(
                "Room 0:6a did not instantiate INTERAC_IMPA_IN_CUTSCENE $31:$00.");
        }

        if (_rooms.SaveData.HasRoomFlag(
            _impaRecord.Group, _impaRecord.Room, (byte)_impaRecord.RoomFlag))
        {
            _impa.SetActive(false);
            return;
        }

        _impa.SetSpritePalette(_impaDatabase.PossessedPalette);
        _impa.SetDirectionalAnimations(
            _impaRecord.UpAnimation,
            _impaRecord.RightAnimation,
            _impaRecord.DownAnimation,
            _impaRecord.LeftAnimation);
        _fakeOctoroks.Clear();
        foreach (ImpaIntroEventDatabase.FakeOctorokRecord record in _impaDatabase.Octoroks)
        {
            NpcCharacter actor = _entities.Spawn<NpcCharacter>(new CutsceneNpcSpawn(
                record.ToNpcRecord(_impaRecord.Group, _impaRecord.Room),
                $"FakeOctorok_{record.Index}"));
            actor.SetScriptAnimation(record.InitialAnimation);
            _fakeOctoroks.Add(new FakeOctorokState(record, actor));
        }

        _impaFollowRoom = room;
        _impaStage = ImpaStage.LinkInitialize;
        _counter = 0;
        _frameAccumulator = 0.0;
        _player.BeginCutsceneControl();
    }

    private void StartMakuTreeEvent(OracleRoomData room)
    {
        _makuTree = null;
        foreach (NpcCharacter npc in _entities.Entities<NpcCharacter>())
        {
            if (npc.Record.Id == _record.InteractionId && npc.Record.SubId == _record.SubId)
            {
                _makuTree = npc;
                break;
            }
        }
        if (_makuTree is null)
            throw new InvalidOperationException(
                "Room 0:38 did not instantiate INTERAC_MAKU_TREE $87:$00.");

        if (Completed)
        {
            _makuTree.SetActive(false);
            return;
        }

        _eventRoom = room;
        _stage = Stage.IntroDelay;
        _counter = _record.IntroDelayFrames;
        _inputFrame = 0;
        _paletteHeader = 0;
        _paletteCycling = false;
        _frameAccumulator = 0.0;
        _makuTree.AppendScriptGraphics(_record.ExtraSprite);
        _makuTree.SetScriptAnimation(_record.Animation0);
        _player.BeginCutsceneControl();
    }

    private void StartRalphEvent()
    {
        _ralph = null;
        foreach (NpcCharacter npc in _entities.Entities<NpcCharacter>())
        {
            if (npc.Record.Id == _ralphRecord.InteractionId &&
                npc.Record.SubId == _ralphRecord.SubId)
            {
                _ralph = npc;
                break;
            }
        }
        if (_ralph is null)
        {
            throw new InvalidOperationException(
                "Room 0:39 did not instantiate INTERAC_RALPH $37:$0d.");
        }

        // @initSubid0d deletes Ralph unless wScreenTransitionDirection is
        // DIR_RIGHT ($01). It also deletes him after the one-shot flag is set.
        Vector2I requiredDirection = DirectionFromOriginalValue(_ralphRecord.EntryDirection);
        if (RalphCompleted || !_transitions.ScrollActive ||
            _transitions.ScrollDirection != requiredDirection)
        {
            _ralph.SetActive(false);
            return;
        }

        _ralphStage = RalphStage.WaitingForScroll;
        _counter = 0;
        _frameAccumulator = 0.0;
    }

    private void UpdateFrame()
    {
        AdvanceSimulatedInput();
        if (_paletteCycling && (_entities.FrameCounter & 0x07) == 0)
        {
            _paletteHeader = (_paletteHeader + 1) & 0x03;
            _eventRoom!.SetTemporaryBackgroundPalette(
                _database.BackgroundPalettes, _paletteHeader);
            _roomView.QueueRedraw();
        }

        switch (_stage)
        {
            case Stage.IntroDelay:
                if (CountDown())
                    ShowText(_record.IntroText, Stage.IntroText);
                break;
            case Stage.IntroText:
                if (!_dialogue.IsOpen)
                    BeginWait(Stage.PostIntro, _record.PostIntroFrames);
                break;
            case Stage.PostIntro:
                if (CountDown())
                {
                    _makuTree!.SetScriptAnimation(_record.Animation4);
                    BeginWait(Stage.Frown, _record.FrownFrames);
                }
                break;
            case Stage.Frown:
                if (CountDown())
                {
                    _paletteCycling = true;
                    BeginWait(Stage.Disappearance, _record.DisappearanceFrames);
                }
                break;
            case Stage.Disappearance:
                if (CountDown())
                    ShowText(_record.AhhText, Stage.AhhText);
                break;
            case Stage.AhhText:
                if (!_dialogue.IsOpen)
                    BeginWait(Stage.PostAhh, _record.PostAhhFrames);
                break;
            case Stage.PostAhh:
                if (CountDown())
                    ShowText(_record.HelpText, Stage.HelpText);
                break;
            case Stage.HelpText:
                if (!_dialogue.IsOpen)
                    BeginWait(Stage.FinishDelay, _record.FinishDelayFrames);
                break;
            case Stage.FinishDelay:
                if (CountDown())
                    Finish();
                break;
        }
    }

    private void AdvanceSimulatedInput()
    {
        int rightStart = _record.InputIdleFrames;
        int rightEnd = rightStart + _record.InputRightFrames;
        int upStart = rightEnd + _record.InputStopFrames;
        int upEnd = upStart + _record.InputUpFrames;
        Vector2I direction = _inputFrame >= rightStart && _inputFrame < rightEnd
            ? Vector2I.Right
            : _inputFrame >= upStart && _inputFrame < upEnd
                ? Vector2I.Up
                : Vector2I.Zero;
        _player.AdvanceCutsceneInput(direction);
        _inputFrame++;
    }

    private void UpdateRalphFrame()
    {
        switch (_ralphStage)
        {
            case RalphStage.WaitingForScroll:
                // Destination objects do not update during a screen scroll.
                // On the first object update afterward, the script disables
                // input and installs its 40-frame counter.
                _player.BeginCutsceneControl();
                BeginRalphWait(RalphStage.IntroDelay, _ralphRecord.IntroDelayFrames);
                break;
            case RalphStage.IntroDelay:
                if (CountDown())
                {
                    _ralphStage = RalphStage.Text;
                    _dialogue.ShowMessage(
                        _ralphRecord.Text, _worldToScreen(_player.Position).Y);
                }
                break;
            case RalphStage.Text:
                if (!_dialogue.IsOpen)
                    BeginRalphWait(RalphStage.PostText, _ralphRecord.PostTextFrames);
                break;
            case RalphStage.PostText:
                if (CountDown())
                {
                    _ralph!.SetScriptAnimation(_ralphRecord.MovementAnimation);
                    _ralphStage = RalphStage.SetSpeed;
                }
                break;
            case RalphStage.SetSpeed:
                // setanimation, setspeed, setangle, and applyspeed each
                // consume one interaction-script update.
                _ralphStage = RalphStage.SetAngle;
                break;
            case RalphStage.SetAngle:
                _ralphStage = RalphStage.StartMovement;
                break;
            case RalphStage.StartMovement:
                BeginRalphWait(RalphStage.Moving, _ralphRecord.MovementCounter);
                break;
            case RalphStage.Moving:
                // interactionRunScript decrements counter2 first and applies
                // speed only while the result is nonzero. applyspeed $11 thus
                // advances Ralph 16 pixels, from x=$18 to the portal at $28.
                _counter--;
                if (_counter > 0)
                {
                    _ralph!.Position += RalphMovementDelta();
                }
                else
                {
                    // The generic counter2 path returns even when the
                    // decrement reaches zero. Script execution resumes on the
                    // following update.
                    _ralphStage = RalphStage.MovementFinished;
                }
                break;
            case RalphStage.MovementFinished:
                _ralph!.SetScriptAnimation(_ralphRecord.PortalAnimation);
                _ralphStage = RalphStage.SetFlickerCounter;
                break;
            case RalphStage.SetFlickerCounter:
                _counter = _ralphRecord.FlickerFrames;
                _ralphStage = RalphStage.PlayPortalSound;
                break;
            case RalphStage.PlayPortalSound:
                // SND_MYSTERY_SEED is deferred with the audio system, but its
                // script command still occupies this update.
                _ralphStage = RalphStage.BeginFlicker;
                break;
            case RalphStage.BeginFlicker:
                _ralphStage = RalphStage.Flickering;
                UpdateRalphFlickerFrame();
                break;
            case RalphStage.Flickering:
                UpdateRalphFlickerFrame();
                break;
        }
    }

    private void BeginRalphWait(RalphStage stage, int frames)
    {
        _ralphStage = stage;
        _counter = frames;
    }

    private void UpdateRalphFlickerFrame()
    {
        // objectFlickerVisibility with b=$01 uses wFrameCounter bit 0.
        _ralph!.Visible = (_entities.FrameCounter & 0x01) != 0;
        _counter--;
        if (_counter == 0)
            FinishRalphEvent();
    }

    private void UpdateImpaFrame()
    {
        UpdateFakeOctoroks();
        switch (_impaStage)
        {
            case ImpaStage.LinkInitialize:
                // linkCutscene1 state 0 occupies its own object update.
                _player.Face(Vector2I.Up);
                BeginImpaWait(ImpaStage.LinkInitialWait, _impaRecord.LinkWaitFrames);
                break;
            case ImpaStage.LinkInitialWait:
                if (CountDown())
                    _impaStage = ImpaStage.LinkHorizontal;
                break;
            case ImpaStage.LinkHorizontal:
                if (Mathf.IsEqualApprox(_player.Position.X, _impaRecord.TargetX))
                {
                    BeginImpaWait(ImpaStage.LinkCenterWait, _impaRecord.CenterWaitFrames);
                    break;
                }
                EnsureObjectSpeed(_impaRecord.LinkSpeed, 0x28, "Link's room 0:6a approach");
                int horizontal = _player.Position.X < _impaRecord.TargetX ? 1 : -1;
                _player.AdvanceCutsceneMovement(
                    new Vector2(horizontal, 0),
                    horizontal > 0 ? Vector2I.Right : Vector2I.Left);
                break;
            case ImpaStage.LinkCenterWait:
                if (CountDown())
                {
                    _counter = _impaRecord.ApproachFrames;
                    _player.Face(Vector2I.Up);
                    _impaStage = ImpaStage.LinkApproach;
                }
                break;
            case ImpaStage.LinkApproach:
                EnsureObjectSpeed(_impaRecord.LinkSpeed, 0x28, "Link's room 0:6a approach");
                _player.AdvanceCutsceneMovement(Vector2.Up, Vector2I.Up);
                _counter--;
                if (_counter == 0)
                    _impaStage = ImpaStage.SignalPending;
                break;
            case ImpaStage.SignalPending:
                // Impa and the fake Octoroks update before linkCutscene1 in
                // the original object order, so they observe cfd0=$01 on the
                // update after Link writes it.
                SignalFakeOctoroks();
                BeginImpaWait(ImpaStage.ImpaDelay, _impaRecord.ImpaWaitFrames);
                break;
            case ImpaStage.ImpaDelay:
                if (CountDown())
                {
                    _impaStage = ImpaStage.Text;
                    _dialogue.ShowMessage(
                        _impaRecord.Text, _worldToScreen(_player.Position).Y);
                }
                break;
            case ImpaStage.Text:
                if (!_dialogue.IsOpen)
                    BeginImpaWait(ImpaStage.PostText, _impaRecord.PostTextFrames);
                break;
            case ImpaStage.PostText:
                if (CountDown())
                    _impaStage = ImpaStage.SetSpeed;
                break;
            case ImpaStage.SetSpeed:
                // setspeed and movedown each stop this interaction-script
                // update; counter2 movement begins on the following update.
                EnsureObjectSpeed(_impaRecord.ImpaSpeed, 0x14, "Impa's room 0:6a movement");
                _impaStage = ImpaStage.StartMovement;
                break;
            case ImpaStage.StartMovement:
                _impa!.SetFacingDirection(Vector2I.Down);
                _impaPrecisePosition = _impa.Position;
                _counter = _impaRecord.ImpaMoveFrames;
                _impaStage = ImpaStage.Moving;
                break;
            case ImpaStage.Moving:
                _counter--;
                if (_counter > 0)
                {
                    // SPEED_080 is 0.5px/update, but only the high coordinate
                    // byte is rendered by the GBC object compositor.
                    _impaPrecisePosition += Vector2.Down * 0.5f;
                    _impa!.Position = ObjectPosition(_impaPrecisePosition);
                }
                else
                {
                    _impaStage = ImpaStage.MovementFinished;
                }
                break;
            case ImpaStage.MovementFinished:
                FinishImpaEncounter();
                break;
            case ImpaStage.Following:
                UpdateFollowingImpa();
                break;
        }
    }

    private void UpdateImpaHelpFrame(bool upPressed)
    {
        switch (_impaHelpStage)
        {
            case ImpaHelpStage.WaitingAtEdge:
                if (!upPressed || _player.Position.Y >= _impaHelpRecord.EdgeY)
                    return;
                _player.BeginCutsceneControl();
                _counter = _impaHelpRecord.PostTextFrames;
                _impaHelpStage = ImpaHelpStage.Text;
                _dialogue.ShowMessage(
                    _impaHelpRecord.Text,
                    _worldToScreen(_player.Position).Y,
                    _impaHelpRecord.TextboxPosition);
                break;
            case ImpaHelpStage.Text:
                if (_dialogue.IsOpen)
                    return;
                _impaHelpStage = ImpaHelpStage.PostText;
                AdvanceImpaHelpPostTextCounter();
                break;
            case ImpaHelpStage.PostText:
                AdvanceImpaHelpPostTextCounter();
                break;
            case ImpaHelpStage.SimulatedInput:
                _counter--;
                _player.AdvanceCutsceneInput(Vector2I.Up);
                _transitions.CheckRoomExit(_player);
                // Beginning the scroll synchronously loads room $6a, which
                // replaces this state with the Impa encounter in the room-
                // entities callback.
                if (_impaHelpStage != ImpaHelpStage.SimulatedInput)
                    return;
                if (_counter == 0)
                {
                    _impaHelpStage = ImpaHelpStage.None;
                    _player.EndCutsceneControl();
                }
                break;
        }
    }

    internal void TriggerImpaHelpForValidation() => UpdateImpaHelpFrame(upPressed: true);

    private void AdvanceImpaHelpPostTextCounter()
    {
        _counter--;
        if (_counter != 0)
            return;
        _rooms.SaveData.SetRoomFlag(
            _impaHelpRecord.Group,
            _impaHelpRecord.Room,
            (byte)_impaHelpRecord.RoomFlag);
        _counter = _impaHelpRecord.InputUpFrames;
        _impaHelpStage = ImpaHelpStage.SimulatedInput;
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
                    if (!WithinOriginalScreenBoundary(state.Actor.Position))
                    {
                        state.Actor.SetActive(false);
                        state.Stage = FakeOctorokStage.Finished;
                        break;
                    }
                    EnsureObjectSpeed(state.Record.Speed, 0x78, "fake Octorok escape");
                    state.Actor.Position += AngleDirection(state.Record.Angle) * 3.0f;
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

    private void FinishImpaEncounter()
    {
        _rooms.SaveData.SetRoomFlag(
            _impaRecord.Group, _impaRecord.Room, (byte)_impaRecord.RoomFlag);
        _player.EndCutsceneControl();
        _player.Face(Vector2I.Up);
        _impa!.Position = _player.Position;
        _impa.SetBlocksLink(false);
        _impa.SetFacingDirection(Vector2I.Up);
        _impa.UpdateDrawPriority(_player.Position);
        LinkPathEntry initial = new(Vector2I.Up, ObjectPosition(_player.Position));
        for (int index = 0; index < _linkPath.Length; index++)
            _linkPath[index] = initial;
        _linkPathIndex = 0;
        _impaFollowRoom = _rooms.CurrentRoom;
        _impaStage = ImpaStage.Following;
    }

    private void UpdateFollowingImpa() => UpdateFollowingImpa(_player.Position);

    private void UpdateFollowingImpa(Vector2 linkPosition)
    {
        LinkPathEntry current = new(
            _player.FacingVector,
            ObjectPosition(linkPosition));
        LinkPathEntry indexed = _linkPath[_linkPathIndex];
        if (indexed == current)
            return;

        _linkPathIndex = (_linkPathIndex + 1) & 0x0f;
        LinkPathEntry old = _linkPath[_linkPathIndex];
        _linkPath[_linkPathIndex] = current;
        _impa!.Position = old.Position;
        _impa.SetFacingDirection(old.Direction);
        _impa.UpdateDrawPriority(_player.Position);
    }

    private void TransferFollowingImpa(int group, OracleRoomData room)
    {
        NpcCharacter outgoing = _impa!;
        Vector2 offset = _transitions.ScrollDirection == Vector2I.Up
            ? new Vector2(0, _impaFollowRoom!.Height)
            : _transitions.ScrollDirection == Vector2I.Right
                ? new Vector2(-_impaFollowRoom!.Width, 0)
                : _transitions.ScrollDirection == Vector2I.Down
                    ? new Vector2(0, -_impaFollowRoom!.Height)
                    : new Vector2(_impaFollowRoom!.Width, 0);

        NpcDatabase.NpcRecord record = _impa!.Record with
        {
            Group = group,
            Room = room.Id,
            Y = (int)(_impa.Position.Y + offset.Y),
            X = (int)(_impa.Position.X + offset.X)
        };
        NpcCharacter incoming = _entities.Spawn<NpcCharacter>(new CutsceneNpcSpawn(
            record, "FollowingImpa"));
        incoming.SetSpritePalette(_impaDatabase.PossessedPalette);
        incoming.Position = _impa.Position + offset;
        incoming.SetFacingDirection(_impa.FacingVector);
        // objectSetReservedBit1 keeps one interaction slot alive across the
        // original reload. Our room lists require a destination-owned actor,
        // so retire the superseded outgoing rendering copy immediately.
        outgoing.SetActive(false);
        _impa = incoming;
        for (int index = 0; index < _linkPath.Length; index++)
            _linkPath[index] = _linkPath[index] with { Position = _linkPath[index].Position + offset };
        _resetImpaFollowerAfterScroll = true;
        _impaFollowerScrollDirection = _transitions.ScrollDirection;
        _impaFollowRoom = room;
    }

    private void OnScrollingTransitionFinished(Vector2I direction)
    {
        if (!ImpaFollowing || !_resetImpaFollowerAfterScroll)
            return;
        if (direction != _impaFollowerScrollDirection)
        {
            throw new InvalidOperationException(
                $"Following Impa expected scroll direction {_impaFollowerScrollDirection}, got {direction}.");
        }

        // resetFollowingLinkObjectPosition rebuilds w2LinkWalkPath backwards
        // from entry $0f so the follower begins exactly 16 pixels outside the
        // destination edge, as if Link had just walked in from that edge.
        Vector2I movementOffset = direction == Vector2I.Up ? Vector2I.Down
            : direction == Vector2I.Right ? Vector2I.Left
            : direction == Vector2I.Down ? Vector2I.Up
            : Vector2I.Right;
        Vector2 position = ObjectPosition(_player.Position);
        for (int index = _linkPath.Length - 1; index >= 0; index--)
        {
            position += movementOffset;
            _linkPath[index] = new LinkPathEntry(direction, position);
        }
        _impa!.Position = position;
        _impa.UpdateDrawPriority(_player.Position);
        _linkPathIndex = 0x0f;
        _resetImpaFollowerAfterScroll = false;
    }

    private void SuppressPlacedImpaIfCompleted(int group, OracleRoomData room)
    {
        if (group != _impaRecord.Group || room.Id != _impaRecord.Room ||
            !_rooms.SaveData.HasRoomFlag(
                _impaRecord.Group, _impaRecord.Room, (byte)_impaRecord.RoomFlag))
        {
            return;
        }
        foreach (NpcCharacter npc in _entities.Entities<NpcCharacter>())
        {
            if (npc.Record.Id == _impaRecord.InteractionId &&
                npc.Record.SubId == _impaRecord.SubId)
            {
                npc.SetActive(false);
            }
        }
    }

    private void BeginImpaWait(ImpaStage stage, int frames)
    {
        _impaStage = stage;
        _counter = frames;
    }

    private static Vector2 ObjectPosition(Vector2 position) => new(
        Mathf.Floor(position.X), Mathf.Floor(position.Y));

    private static bool WithinOriginalScreenBoundary(Vector2 position) =>
        position.Y >= -7 && position.Y < 136 &&
        position.X >= -7 && position.X < 168;

    private static Vector2 AngleDirection(int angle) => angle switch
    {
        0x00 => Vector2.Up,
        0x08 => Vector2.Right,
        0x10 => Vector2.Down,
        0x18 => Vector2.Left,
        _ => throw new InvalidOperationException(
            $"Unsupported cardinal object angle ${angle:x2}.")
    };

    private static void EnsureObjectSpeed(int actual, int expected, string context)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(
                $"Unsupported {context} speed ${actual:x2}; expected ${expected:x2}.");
        }
    }

    private Vector2 RalphMovementDelta()
    {
        if (_ralphRecord.Speed != 0x28)
            throw new InvalidOperationException(
                $"Unsupported Ralph object speed ${_ralphRecord.Speed:x2}; expected SPEED_100 ($28).");
        return _ralphRecord.Angle switch
        {
            0x00 => Vector2.Up,
            0x08 => Vector2.Right,
            0x10 => Vector2.Down,
            0x18 => Vector2.Left,
            _ => throw new InvalidOperationException(
                $"Unsupported cardinal object angle ${_ralphRecord.Angle:x2}.")
        };
    }

    private static Vector2I DirectionFromOriginalValue(int direction) => direction switch
    {
        0 => Vector2I.Up,
        1 => Vector2I.Right,
        2 => Vector2I.Down,
        3 => Vector2I.Left,
        _ => throw new InvalidOperationException(
            $"Unsupported wScreenTransitionDirection value ${direction:x2}.")
    };

    private bool CountDown()
    {
        if (_counter > 0)
            _counter--;
        return _counter == 0;
    }

    private void BeginWait(Stage stage, int frames)
    {
        _stage = stage;
        _counter = frames;
    }

    private void ShowText(string text, Stage stage)
    {
        _stage = stage;
        _dialogue.ShowMessage(
            text, _worldToScreen(_player.Position).Y, _record.TextboxPosition);
    }

    private void Finish()
    {
        _stage = Stage.None;
        _paletteCycling = false;
        _eventRoom!.ClearTemporaryBackgroundPalette(_animationTick());
        _roomView.QueueRedraw();
        _player.EndCutsceneControl();
        // makuTreeDisappearingCutsceneHandler sets GLOBALFLAG_0c and room bit
        // 0, while the interaction script increments wMakuTreeState.
        _rooms.SaveData.SetGlobalFlag(OracleSaveData.GlobalFlagMakuTreeDisappeared);
        _rooms.SaveData.SetMakuTreeState(Math.Min(_rooms.SaveData.MakuTreeState + 1, 0xff));
        // The cutscene handler sets bit 0 in room 0:38. For overworld rooms,
        // getAdjustedRoomGroup interprets ROOMFLAG_LAYOUTSWAP by loading the
        // corresponding group+2 tileset and layout on re-entry.
        _rooms.SetLayoutSwapped(_record.Group, _record.Room);

        var warp = new WarpDatabase.Warp(
            _record.Group,
            _record.Room,
            -1,
            0,
            _record.SourceTransition,
            _record.DestinationGroup,
            _record.DestinationRoom,
            _record.DestinationPosition,
            _record.DestinationParameter,
            _record.DestinationTransition);
        // wWarpTransition2=$83 takes the delayed (divisor 4) white fade path.
        _transitions.ApplyWarpWithDelayedFadeOut(_player, warp);
    }

    private void FinishRalphEvent()
    {
        _rooms.SaveData.SetGlobalFlag(_ralphRecord.GlobalFlag);
        _ralph!.SetActive(false);
        _ralphStage = RalphStage.None;
        _player.EndCutsceneControl();
    }

    private void Cancel()
    {
        _eventRoom?.ClearTemporaryBackgroundPalette(_animationTick());
        _player.EndCutsceneControl();
        foreach (FakeOctorokState state in _fakeOctoroks)
            state.Actor.SetActive(false);
        _fakeOctoroks.Clear();
        _eventRoom = null;
        _makuTree = null;
        _ralph = null;
        _impa = null;
        _impaFollowRoom = null;
        _resetImpaFollowerAfterScroll = false;
        _stage = Stage.None;
        _ralphStage = RalphStage.None;
        _impaStage = ImpaStage.None;
        _impaHelpStage = ImpaHelpStage.None;
        _paletteCycling = false;
        _frameAccumulator = 0.0;
    }
}
