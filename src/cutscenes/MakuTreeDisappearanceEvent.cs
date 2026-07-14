using Godot;
using System;

namespace oracleofages;

/// <summary>Runs the one-shot Maku Tree disappearance in room $0:$38.</summary>
internal sealed class MakuTreeDisappearanceEvent : IRoomEvent
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

    private readonly RoomEventContext _context;
    private readonly MakuTreeCutsceneDatabase _database = new();
    private readonly MakuTreeCutsceneDatabase.MakuTreeCutsceneRecord _record;
    private Stage _stage;
    private OracleRoomData? _eventRoom;
    private NpcCharacter? _makuTree;
    private int _counter;
    private int _inputFrame;
    private int _paletteHeader;
    private bool _paletteCycling;

    public MakuTreeDisappearanceEvent(RoomEventContext context)
    {
        _context = context;
        _record = _database.Record;
    }

    public bool HasState => _stage != Stage.None;
    public bool BlocksGameplay => HasState;
    internal int InputFrame => _inputFrame;
    internal int PaletteHeader => _paletteHeader;
    internal bool Completed =>
        _context.Rooms.SaveData.HasGlobalFlag(OracleSaveData.GlobalFlagMakuTreeDisappeared);

    public bool Matches(int group, OracleRoomData room) =>
        group == _record.Group && room.Id == _record.Room;

    public void Start(OracleRoomData room)
    {
        _makuTree = null;
        foreach (NpcCharacter npc in _context.Entities.Entities<NpcCharacter>())
        {
            if (npc.Record.Id == _record.InteractionId && npc.Record.SubId == _record.SubId)
            {
                _makuTree = npc;
                break;
            }
        }
        if (_makuTree is null)
        {
            throw new InvalidOperationException(
                "Room 0:38 did not instantiate INTERAC_MAKU_TREE $87:$00.");
        }

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
        _makuTree.AppendScriptGraphics(_record.ExtraSprite);
        _makuTree.SetScriptAnimation(_record.Animation0);
        _context.Player.BeginCutsceneControl();
    }

    public void UpdateFrame()
    {
        AdvanceSimulatedInput();
        if (_paletteCycling && (_context.Entities.FrameCounter & 0x07) == 0)
        {
            _paletteHeader = (_paletteHeader + 1) & 0x03;
            _eventRoom!.SetTemporaryBackgroundPalette(
                _database.BackgroundPalettes, _paletteHeader);
            _context.RoomView.QueueRedraw();
        }

        switch (_stage)
        {
            case Stage.IntroDelay:
                if (CountDown())
                    ShowText(_record.IntroText, Stage.IntroText);
                break;
            case Stage.IntroText:
                if (!_context.Dialogue.IsOpen)
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
                if (!_context.Dialogue.IsOpen)
                    BeginWait(Stage.PostAhh, _record.PostAhhFrames);
                break;
            case Stage.PostAhh:
                if (CountDown())
                    ShowText(_record.HelpText, Stage.HelpText);
                break;
            case Stage.HelpText:
                if (!_context.Dialogue.IsOpen)
                    BeginWait(Stage.FinishDelay, _record.FinishDelayFrames);
                break;
            case Stage.FinishDelay:
                if (CountDown())
                    Finish();
                break;
        }
    }

    public void Cancel()
    {
        _eventRoom?.ClearTemporaryBackgroundPalette(_context.AnimationTick());
        _eventRoom = null;
        _makuTree = null;
        _stage = Stage.None;
        _paletteCycling = false;
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
        _context.Player.AdvanceCutsceneInput(direction);
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
        _context.Dialogue.ShowMessage(
            text,
            _context.WorldToScreen(_context.Player.Position).Y,
            _record.TextboxPosition);
    }

    private void Finish()
    {
        _stage = Stage.None;
        _paletteCycling = false;
        _eventRoom!.ClearTemporaryBackgroundPalette(_context.AnimationTick());
        _context.RoomView.QueueRedraw();
        _context.Player.EndCutsceneControl();
        // makuTreeDisappearingCutsceneHandler sets GLOBALFLAG_0c and room bit
        // 0, while the interaction script increments wMakuTreeState.
        _context.Rooms.SaveData.SetGlobalFlag(OracleSaveData.GlobalFlagMakuTreeDisappeared);
        _context.Rooms.SaveData.SetMakuTreeState(
            Math.Min(_context.Rooms.SaveData.MakuTreeState + 1, 0xff));
        // The cutscene handler sets bit 0 in room 0:38. For overworld rooms,
        // getAdjustedRoomGroup interprets ROOMFLAG_LAYOUTSWAP by loading the
        // corresponding group+2 tileset and layout on re-entry.
        _context.Rooms.SetLayoutSwapped(_record.Group, _record.Room);

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
        _context.Transitions.ApplyWarpWithDelayedFadeOut(_context.Player, warp);
    }
}
