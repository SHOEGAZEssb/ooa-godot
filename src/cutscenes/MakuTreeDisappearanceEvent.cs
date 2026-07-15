using Godot;
using System;

namespace oracleofages;

/// <summary>Runs the one-shot Maku Tree disappearance in room $0:$38.</summary>
internal sealed class MakuTreeDisappearanceEvent : IRoomEvent
{
    private readonly RoomEventContext _context;
    private readonly MakuTreeCutsceneDatabase _database = new();
    private readonly MakuTreeCutsceneDatabase.MakuTreeCutsceneRecord _record;
    private readonly RoomEventTimeline _timeline = new();
    private OracleRoomData? _eventRoom;
    private NpcCharacter? _makuTree;
    private int _inputFrame;
    private int _paletteCycleIndex;
    private int _paletteHeader;
    private bool _paletteCycling;

    public MakuTreeDisappearanceEvent(RoomEventContext context)
    {
        _context = context;
        _record = _database.Record;
    }

    public bool HasState => _timeline.Active;
    public bool BlocksGameplay => HasState;
    internal int InputFrame => _inputFrame;
    internal int PaletteHeader => _paletteHeader;
    internal MakuTreeCutsceneDatabase Database => _database;
    internal bool Completed =>
        _context.Rooms.SaveData.HasGlobalFlag(OracleSaveData.GlobalFlagMakuTreeDisappeared);

    public bool Matches(int group, OracleRoomData room) =>
        group == _record.Group && room.Id == _record.Room;

    public void Start(OracleRoomData room)
    {
        _timeline.Clear();
        _makuTree = _context.RequireNpc(
            _record.Group,
            _record.Room,
            _record.InteractionId,
            _record.SubId,
            "INTERAC_MAKU_TREE");

        if (Completed)
        {
            // The hardcoded same-room warp has now replaced the unswapped
            // room at full white. Only retire its PALH_8f override here; the
            // original fade operates on the cutscene palette still in RAM.
            ClearEventRoomPalette();
            _makuTree.SetActive(false);
            return;
        }

        _eventRoom = room;
        _inputFrame = 0;
        _paletteCycleIndex = 0;
        _paletteHeader = _record.InitialPaletteHeader;
        _paletteCycling = false;
        _eventRoom.SetTemporaryBackgroundPalette(
            _database.BackgroundPalettes, _paletteHeader);
        _context.RoomView.QueueRedraw();
        _makuTree.AppendScriptGraphics(_record.ExtraSprite);
        _makuTree.SetScriptAnimation(_record.Animation0);
        _context.Player.BeginCutsceneControl();
        BuildTimeline();
    }

    public void UpdateFrame()
    {
        AdvanceSimulatedInput();
        if (_paletteCycling && (_context.Entities.FrameCounter & 0x07) == 0)
        {
            _paletteCycleIndex = (_paletteCycleIndex + 1) & 0x03;
            _paletteHeader = _paletteCycleIndex;
            _eventRoom!.SetTemporaryBackgroundPalette(
                _database.BackgroundPalettes, _paletteHeader);
            _context.RoomView.QueueRedraw();
        }

        _timeline.AdvanceFrame();
    }

    public void Cancel()
    {
        ClearEventRoomPalette();
        _makuTree = null;
        _timeline.Clear();
        _paletteCycling = false;
    }

    private void BuildTimeline()
    {
        _timeline.Clear();
        _timeline.Wait(
            _record.IntroDelayFrames,
            elapsed: () => ShowDialogue(_record.IntroText));
        _timeline.WaitUntil(() => !_context.DialogueOpen);
        _timeline.Wait(
            _record.PostIntroFrames,
            elapsed: () =>
            {
                _context.Sound.PlaySound(OracleSoundEngine.SndCtrlStopMusic);
                _makuTree!.SetScriptAnimation(_record.Animation4);
            });
        _timeline.Wait(
            _record.FrownFrames,
            elapsed: () =>
            {
                _context.Sound.PlaySound(OracleSoundEngine.SndMakuDisappear);
                _paletteCycling = true;
            });
        _timeline.Wait(
            _record.DisappearanceFrames,
            elapsed: () => ShowDialogue(_record.AhhText));
        _timeline.WaitUntil(
            () => !_context.DialogueOpen,
            completed: () =>
                _context.Sound.PlaySound(OracleSoundEngine.SndMakuDisappear));
        _timeline.Wait(
            _record.PostAhhFrames,
            elapsed: () => ShowDialogue(_record.HelpText));
        _timeline.WaitUntil(
            () => !_context.DialogueOpen,
            completed: () =>
                _context.Sound.PlaySound(OracleSoundEngine.SndMakuDisappear));
        _timeline.Wait(_record.FinishDelayFrames, elapsed: Finish);
    }

    private void ShowDialogue(string text) =>
        _context.ShowDialogue(text, _record.TextboxPosition);

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

    private void Finish()
    {
        _timeline.Clear();
        _paletteCycling = false;
        // The cutscene handler starts the transition's endless fade effect on
        // the same update that it sets GLOBALFLAG_0c and the hardcoded warp.
        // It does not reload the tileset palette first, so PALH_8f must remain
        // on the unswapped room until the fade reaches white and reloads it.
        _context.Sound.PlaySound(OracleSoundEngine.SndFadeOut);
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

    private void ClearEventRoomPalette()
    {
        _eventRoom?.ClearTemporaryBackgroundPalette(_context.AnimationTick());
        _eventRoom = null;
    }
}
