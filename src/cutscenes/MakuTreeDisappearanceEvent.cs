using Godot;
using System;

namespace oracleofages;

/// <summary>Runs the one-shot Maku Tree disappearance in room $0:$38.</summary>
internal sealed class MakuTreeDisappearanceEvent : IRoomEntryEvent, ICutsceneCommandHost
{
    private const string MakuTreeActor = "MakuTree";
    private const string PaletteFadeDoneGate = "palette-fade-done";
    private const string CutsceneTriggerBinding = "wCutsceneTrigger";
    private const string CutsceneStateBinding = "wTmpcfc0.genericCutscene.state";
    private readonly RoomEventContext _context;
    private readonly MakuTreeCutsceneDatabase _database = new();
    private readonly MakuTreeCutsceneDatabase.MakuTreeCutsceneRecord _record;
    private readonly CutsceneCommandRunner _runner;
    private OracleRoomData? _eventRoom;
    private NpcCharacter? _makuTree;
    private int _inputFrame;
    private int _paletteCycleIndex;
    private int _paletteHeader;
    private bool _paletteCycling;
    private bool _finishPending;

    public MakuTreeDisappearanceEvent(RoomEventContext context)
    {
        _context = context;
        _record = _database.Record;
        _runner = new CutsceneCommandRunner(this);
    }

    public bool HasState => _runner.Active || _finishPending;
    public bool BlocksGameplay => HasState;
    internal int InputFrame => _inputFrame;
    internal int PaletteHeader => _paletteHeader;
    internal int Counter => _runner.Counter;
    internal int CurrentCommandIndex =>
        _runner.CurrentCommand?.Source.CommandIndex ?? -1;
    internal MakuTreeCutsceneDatabase Database => _database;
    internal bool Completed =>
        _context.Rooms.SaveData.HasGlobalFlag(OracleSaveData.GlobalFlagMakuTreeDisappeared);

    public bool Matches(int group, OracleRoomData room) =>
        group == _record.Group && room.Id == _record.Room;

    public void Start(OracleRoomData room)
    {
        _runner.Clear();
        _finishPending = false;
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
        // RoomEntityManager runs before the interaction script in this port.
        // Freeze its generic animation path so this event can reproduce the
        // original interactionRunScript -> interactionAnimate ordering.
        _makuTree.SetAnimationRate(0.0f);
        _context.Player.BeginCutsceneControl();
        _runner.Start(_database.Commands);
    }

    public void UpdateFrame()
    {
        // makuTreeDisappearingCutsceneHandler runs before updateAllObjects.
        // Its state-1 branch therefore executes one update after the script
        // writes wTmpcfc0.genericCutscene.state.
        if (_finishPending)
        {
            Finish();
            _makuTree?.AdvanceAnimationUpdates(1);
            return;
        }

        if (_paletteCycling && (_context.Entities.FrameCounter & 0x07) == 0)
        {
            _paletteCycleIndex = (_paletteCycleIndex + 1) & 0x03;
            _paletteHeader = _paletteCycleIndex;
            _eventRoom!.SetTemporaryBackgroundPalette(
                _database.BackgroundPalettes, _paletteHeader);
            _context.RoomView.QueueRedraw();
        }

        AdvanceSimulatedInput();
        _runner.AdvanceFrame();
        _makuTree?.AdvanceAnimationUpdates(1);
    }

    public void Cancel()
    {
        ClearEventRoomPalette();
        if (_makuTree is not null)
            _makuTree.SetAnimationRate(1.0f);
        _makuTree = null;
        _runner.Clear();
        _paletteCycling = false;
        _finishPending = false;
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

    private void Finish()
    {
        _finishPending = false;
        _paletteCycling = false;
        // The cutscene handler starts the transition's endless fade effect on
        // the same update that it sets GLOBALFLAG_0c and the hardcoded warp.
        // It does not reload the tileset palette first, so PALH_8f must remain
        // on the unswapped room until the fade reaches white and reloads it.
        _context.Sound.PlaySound(OracleSoundEngine.SndFadeOut);
        _context.Player.EndCutsceneControl();
        // makuTreeDisappearingCutsceneHandler sets GLOBALFLAG_0c and room bit
        // 0. The preceding typed native command has already reproduced the
        // interaction script's incMakuTreeState call.
        _context.Rooms.SaveData.SetGlobalFlag(OracleSaveData.GlobalFlagMakuTreeDisappeared);
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

    RoomEventContext ICutsceneCommandHost.Context => _context;
    bool ICutsceneCommandHost.HasActorBinding(CutsceneActorId actor) =>
        actor.Value == "MakuTree";

    void ICutsceneCommandHost.SetInputEnabled(bool enabled) =>
        throw new InvalidOperationException(
            $"Maku Tree script does not support setting input enabled={enabled}.");

    void ICutsceneCommandHost.SetMenuEnabled(bool enabled)
    {
        if (enabled)
        {
            throw new InvalidOperationException(
                "makuTree_subid01Script_body cannot enable the menu.");
        }

        // disablemenu only sets wMenuDisabled. BeginCutsceneControl is already
        // active so simulated input owns Link while this script is running.
    }

    void ICutsceneCommandHost.SetDisabledObjects(int value) =>
        throw new InvalidOperationException(
            $"Maku Tree script does not support wDisabledObjects=${value:x2}.");

    bool ICutsceneCommandHost.GateOpen(string gate)
    {
        if (gate != PaletteFadeDoneGate)
            throw new InvalidOperationException($"Unknown Maku Tree script gate '{gate}'.");

        // Room events are frozen while the room transition fade is active.
        // The first script update after that transition therefore satisfies
        // checkpalettefadedone exactly as the original interaction does.
        return !_context.Transitions.IsTransitioning;
    }

    bool ICutsceneCommandHost.MemoryEquals(string binding, int value) =>
        throw new InvalidOperationException(
            $"Maku Tree script cannot read '{binding}'=${value:x2}.");

    void ICutsceneCommandHost.ShowText(int textId, string message)
    {
        string expected = textId switch
        {
            0x0564 => _record.IntroText,
            0x0540 => _record.AhhText,
            0x0541 => _record.HelpText,
            _ => throw new InvalidOperationException(
                $"Maku Tree command stream requested unknown TX_{textId:x4}.")
        };
        if (message != expected)
        {
            throw new InvalidOperationException(
                $"Maku Tree TX_{textId:x4} text diverges from imported metadata.");
        }
        _context.ShowDialogue(message, _record.TextboxPosition);
    }

    void ICutsceneCommandHost.SetActorAnimation(
        string actor,
        int animation,
        string encodedAnimation) =>
        RequireMakuTree(actor).SetScriptAnimation(encodedAnimation);

    void ICutsceneCommandHost.SetActorMovementAnimation(
        string actor,
        int angle,
        string encodedAnimation) =>
        throw new InvalidOperationException(
            $"Maku Tree actor '{actor}' cannot use movement animation ${angle:x2}.");

    void ICutsceneCommandHost.SetActorCollisionRadii(
        string actor,
        int radiusY,
        int radiusX) =>
        RequireMakuTree(actor).SetCollisionRadii(radiusY, radiusX);

    void ICutsceneCommandHost.SetActorButtonSensitive(string actor)
    {
        // makeabuttonsensitive registers the interaction with the original
        // engine's talk-object list. This event owns dialogue dispatch here,
        // but actor validation keeps malformed imported records from passing.
        _ = RequireMakuTree(actor);
    }

    void ICutsceneCommandHost.MoveActorAtSpeed(string actor, int speed, int angle) =>
        throw new InvalidOperationException(
            $"Maku Tree actor '{actor}' cannot move at ${speed:x2}/${angle:x2}.");

    void ICutsceneCommandHost.SetActorZ(string actor, int zFixed) =>
        throw new InvalidOperationException(
            $"Maku Tree actor '{actor}' cannot set Z to ${zFixed:x4}.");

    void ICutsceneCommandHost.SetActorVisible(string actor, bool visible) =>
        RequireMakuTree(actor).Visible = visible;

    void ICutsceneCommandHost.WriteMemory(string binding, int value)
    {
        switch (binding, value)
        {
            case (CutsceneTriggerBinding, 0x07):
                _paletteCycling = true;
                break;
            case (CutsceneStateBinding, 0x01):
                _finishPending = true;
                break;
            default:
                throw new InvalidOperationException(
                    $"Maku Tree script cannot write '{binding}'=${value:x2}.");
        }
    }

    void ICutsceneCommandHost.SetGlobalFlag(int flag) =>
        throw new InvalidOperationException(
            $"Maku Tree script cannot directly set global flag ${flag:x2}.");

    void ICutsceneCommandHost.OrRoomFlag(int flag) =>
        throw new InvalidOperationException(
            $"Maku Tree script cannot OR room flag ${flag:x2}.");

    void ICutsceneCommandHost.RunNativeHandler(string handler)
    {
        if (handler != "incMakuTreeState")
            throw new InvalidOperationException($"Unknown Maku Tree native handler '{handler}'.");

        _context.Rooms.SaveData.SetMakuTreeState(
            Math.Min(_context.Rooms.SaveData.MakuTreeState + 1, 0xff));
    }

    void ICutsceneCommandHost.ScriptEnded()
    {
        if (!_finishPending)
        {
            throw new InvalidOperationException(
                "Maku Tree script ended without scheduling cutscene-handler state 1.");
        }
    }

    private NpcCharacter RequireMakuTree(string actor)
    {
        if (actor != MakuTreeActor || _makuTree is null)
            throw new InvalidOperationException($"Unknown Maku Tree command actor '{actor}'.");
        return _makuTree;
    }

    private void ClearEventRoomPalette()
    {
        _eventRoom?.ClearTemporaryBackgroundPalette(_context.AnimationTick());
        _eventRoom = null;
    }
}
