using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Runs room-entry interaction scripts whose sequencing spans Link, dialogue,
/// palettes, actor movement, and hardcoded warps. Supported records begin with
/// the original Maku Tree disappearance and Ralph's room $39 portal departure.
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

    private Stage _stage;
    private RalphStage _ralphStage;
    private OracleRoomData? _eventRoom;
    private NpcCharacter? _makuTree;
    private NpcCharacter? _ralph;
    private double _frameAccumulator;
    private int _counter;
    private int _inputFrame;
    private int _paletteHeader;
    private bool _paletteCycling;

    public bool Active => _stage != Stage.None || _ralphStage != RalphStage.None;
    internal int CurrentStage => (int)_stage;
    internal bool RalphWaitingForScroll => _ralphStage == RalphStage.WaitingForScroll;
    internal bool RalphFlickering => _ralphStage == RalphStage.Flickering;
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
        if (_ralphRecord.GlobalFlag != OracleSaveData.GlobalFlagRalphEnteredPortal)
        {
            throw new InvalidOperationException(
                $"Ralph portal event uses global flag ${_ralphRecord.GlobalFlag:x2}, expected $40.");
        }
        _entities.RoomEntitiesLoaded += OnRoomEntitiesLoaded;
    }

    public void Update(double delta)
    {
        if (!Active || _transitions.IsTransitioning)
            return;

        _frameAccumulator += delta * 60.0;
        while (Active && _frameAccumulator >= 1.0)
        {
            _frameAccumulator -= 1.0;
            if (_stage != Stage.None)
                UpdateFrame();
            else
                UpdateRalphFrame();
        }
    }

    private void OnRoomEntitiesLoaded(int group, OracleRoomData room)
    {
        if (Active)
            Cancel();

        if (group == _record.Group && room.Id == _record.Room)
        {
            StartMakuTreeEvent(room);
            return;
        }
        if (group == _ralphRecord.Group && room.Id == _ralphRecord.Room)
            StartRalphEvent();
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
        _eventRoom = null;
        _makuTree = null;
        _ralph = null;
        _stage = Stage.None;
        _ralphStage = RalphStage.None;
        _paletteCycling = false;
        _frameAccumulator = 0.0;
    }
}
