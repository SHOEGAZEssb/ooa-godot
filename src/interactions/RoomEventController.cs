using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Runs room-entry interaction scripts whose sequencing spans Link, dialogue,
/// palettes, and a hardcoded warp. The first supported record is the original
/// INTERAC_MAKU_TREE ($87) disappearance event in present room $38.
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

    private Stage _stage;
    private OracleRoomData? _eventRoom;
    private NpcCharacter? _makuTree;
    private double _frameAccumulator;
    private int _counter;
    private int _inputFrame;
    private int _paletteHeader;
    private bool _paletteCycling;
    private bool _completed;

    public bool Active => _stage != Stage.None;
    internal int CurrentStage => (int)_stage;
    internal int Counter => _counter;
    internal int InputFrame => _inputFrame;
    internal int PaletteHeader => _paletteHeader;
    internal bool Completed => _completed;

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
            UpdateFrame();
        }
    }

    private void OnRoomEntitiesLoaded(int group, OracleRoomData room)
    {
        if (Active)
            Cancel();

        if (group != _record.Group || room.Id != _record.Room)
            return;

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

        if (_completed)
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
        _completed = true;
        _stage = Stage.None;
        _paletteCycling = false;
        _eventRoom!.ClearTemporaryBackgroundPalette(_animationTick());
        _roomView.QueueRedraw();
        _player.EndCutsceneControl();
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

    private void Cancel()
    {
        _eventRoom?.ClearTemporaryBackgroundPalette(_animationTick());
        _player.EndCutsceneControl();
        _eventRoom = null;
        _makuTree = null;
        _stage = Stage.None;
        _paletteCycling = false;
        _frameAccumulator = 0.0;
    }
}
