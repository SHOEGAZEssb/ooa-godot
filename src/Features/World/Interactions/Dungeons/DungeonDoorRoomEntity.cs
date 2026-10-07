using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Common shutter variants $1e:$04-$0b/$10-$17. Trigger-controlled doors observe one
/// wActiveTriggers bit; enemy shutters read the live room enemy count. Both
/// use the original mapping-level interleaving for opening and closing. An
/// enemy shutter whose full enemy stream is not implemented still handles the
/// crossed-entry substitution, but leaves that one route open for safe
/// backtracking instead of trapping Link or falsely solving the room.
/// </summary>
internal sealed partial class DungeonDoorRoomEntity : DungeonMechanicRoomEntity,
    IFixedRoomEntity, IRoomEntityLifetime, IBossShutterState,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IAlwaysUpdateDuringScreenTransitionRoomEntity
{

    private readonly DungeonMechanicDatabaseRecord _record;
    private readonly OracleRoomData _room;
    private readonly DungeonMechanicDatabase _data;
    private readonly Func<int> _roomEnemyCount;
    private readonly Func<int, bool> _triggerActive;
    private readonly Func<Vector2, Vector2> _worldToScreen;
    private readonly Func<long> _animationTick;
    private readonly Action<int> _playSound;
    private readonly Func<byte,byte,bool> _setTile;
    private readonly bool _enteredThroughThisDoor;
    private readonly bool _controlledByTrigger;
    private readonly bool _closeAfterEntry;
    private readonly bool _controlledByTorches;
    private readonly Func<int> _torchesLit;
    private readonly Action _flipEntryScratch;
    private readonly bool _enemyCompletionSupported;
    private readonly Action<bool>? _shutterSignal;
    private readonly Func<bool> _textActive;
    private readonly Func<IRoomEntity, bool> _isOutgoing;
    private readonly Func<bool> _paletteFadeActive;
    private DoorState _state;
    private int _counter;
    private int _angle;
    private int _speed;
    private Vector2 _precisePosition;
    internal byte Counter2Alias { get; private set; }
    internal void WriteCounter2Alias(int value) => Counter2Alias = unchecked((byte)value);

    internal int SubId => _record.SubId;
    internal int PackedPosition => _record.PackedPosition;
    internal bool EnteredThroughThisDoor => _enteredThroughThisDoor;
    internal bool EnemyCompletionSupported => _enemyCompletionSupported;
    // The native boss gate counts open shutters, not script commands left
    // to execute on an already closed, initialized non-entry shutter.
    public bool BossIntroReady => _record.SubId < 0x08 ||
        _room.IsSolid(Position) && _state != DoorState.Initialize;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => _state == DoorState.Initialize;
    public bool UpdatesDuringRoomEntityFreeze => _state == DoorState.Initialize;

    internal DungeonDoorRoomEntity(
        DungeonMechanicDatabaseRecord record,
        OracleRoomData room,
        DungeonMechanicDatabase data,
        Func<int> roomEnemyCount,
        Func<int, bool> triggerActive,
        Func<Vector2, Vector2> worldToScreen,
        Func<long> animationTick,
        Action<int> playSound,
        EnemyPlacementContext placementContext,
        bool enemyCompletionSupported,
        Func<byte,byte,bool> setTile,
        Action<bool>? shutterSignal = null,
        Func<bool>? textActive = null,
        Func<IRoomEntity, bool>? isOutgoing = null,
        Func<bool>? paletteFadeActive = null,
        Func<int>? torchesLit = null,
        Action? flipEntryScratch = null)
        : base(record, $"DungeonDoor_{record.SubId:x2}_{record.Order}")
    {
        if (record.Id != InteractionId.DoorController || record.SubId is not (>= 0x04 and <= 0x0b or >= 0x10 and <= 0x17))
            throw new ArgumentOutOfRangeException(nameof(record));
        _record = record;
        _room = room;
        _data = data;
        _roomEnemyCount = roomEnemyCount;
        _triggerActive = triggerActive;
        _worldToScreen = worldToScreen;
        _animationTick = animationTick;
        _playSound = playSound;
        _setTile = setTile;
        _enteredThroughThisDoor = DungeonShutterEntry.Matches(
            placementContext,record.PackedPosition,(data.DoorAngle(record.SubId)>>1)&3);
        _controlledByTrigger = record.SubId <= 0x07;
        _closeAfterEntry = record.SubId is >= 0x10 and <= 0x13;
        _controlledByTorches = record.SubId >= 0x14;
        _torchesLit = torchesLit ?? (_controlledByTorches
            ? throw new ArgumentException($"INTERAC$1e:${record.SubId:x2} requires its room's torch owner.",nameof(torchesLit))
            : () => 0);
        _flipEntryScratch = flipEntryScratch ?? (_closeAfterEntry
            ? throw new ArgumentException($"INTERAC$1e:${record.SubId:x2} requires shared wTmpcfc0 ownership.",nameof(flipEntryScratch))
            : () => { });
        _enemyCompletionSupported = _controlledByTrigger || enemyCompletionSupported;
        _shutterSignal = shutterSignal;
        _textActive = textActive ?? (() => false);
        _isOutgoing = isOutgoing ?? (_ => false);
        _paletteFadeActive = paletteFadeActive ?? (() => false);

        // loadTilesetAndRoomLayout restores the source layout on every room
        // parse. replaceShutterForLinkEntering changes only the shutter on
        // Link's incoming packed position to floor before object parsing.
        // OracleWorldData caches mutable room instances, so reproduce either
        // source state explicitly before state 0 runs.
        _precisePosition = Position;
        // Entry/torch scripts observe the original substituted layout, even
        // when another solid tile means their closing animation is skipped.
        if (record.SubId < 0x10) _room.SetPositionTileAndCollision(
            Position,
            (byte)(_enteredThroughThisDoor
                ? _data.OpenTile
                : _data.ClosedTile(_record.SubId)),
            null,
            _animationTick());
    }

    public void UpdateDuringScreenTransition(RoomEntityFrame frame)
    {
        // Scroll mode$08 dispatches only state0 (this object has no bit$80).
        // enabled$02 deletion precedes returnIfScrollMode01Unset. Incoming
        // state0 returns there; initialized outgoing objects await bulk clear.
        if (_state == DoorState.Initialize && _isOutgoing(this)) Finished = true;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        // doorController.s runs during the latch ($01), but pauses every
        // state while the Switch Hook lifts, swaps and lowers ($02).
        if (frame.SwitchHookState == 2) return;
        // Native state2 checks wPaletteThread_mode before either substate.
        // Closing (state3) and script execution have no palette-thread gate.
        if (_state is DoorState.ReadyToOpen or DoorState.OpeningInterleaved &&
            _paletteFadeActive()) return;
        if (_state == DoorState.Initialize)
        {
            Counter2Alias = 0; // interactionSetScript clears both script counters.
            if (_room.GetTerrainInfo(Position).Collision == 0) _shutterSignal?.Invoke(true);
            _state = DoorState.SetRadii;
        }
        // updateInteractions admits state0 even under text/disable masks.
        // interactionRunScript then checks death/text before any command.
        // Native animation states2/3 do not use that script gate.
        if (ScriptPaused(frame.Player) && _state is not (DoorState.ReadyToOpen or
            DoorState.OpeningInterleaved or DoorState.ReadyToClose or DoorState.ClosingInterleaved)) return;
        if (_state is not (DoorState.ReadyToOpen or DoorState.OpeningInterleaved or
            DoorState.ReadyToClose or DoorState.ClosingInterleaved or DoorState.SolveDelay or DoorState.TorchDelay) &&
            WaitForCounter2()) return;
        switch (_state)
        {
            case DoorState.SetRadii:
                // State0 falls through to the script's setcollisionradii.
                // Each subsequent command yields unless it returns carry.
                _state = DoorState.SetAngle;
                return;

            case DoorState.SetAngle:
                _angle = _data.DoorAngle(_record.SubId);
                _state = _controlledByTorches ? DoorState.SetSpeed : DoorState.InitialBranch;
                return;

            case DoorState.SetSpeed:
                _speed = _data.DoorTorchCount(_record.SubId);
                _state = DoorState.InitialBranch;
                return;

            case DoorState.InitialBranch:
                _state = _controlledByTrigger || _closeAfterEntry || _controlledByTorches ? DoorState.WaitingForLinkClear
                    : _enemyCompletionSupported && _roomEnemyCount() == 0
                        ? DoorState.SelectOpening : DoorState.CallLinkWait;
                return;

            case DoorState.CallLinkWait:
                _state = DoorState.WaitingForLinkClear;
                return;

            case DoorState.WaitingForLinkClear:
                if (OverlapsLink(frame.Player))
                    return;
                _state = DoorState.UpdateRespawn;
                return;

            case DoorState.UpdateRespawn:
                frame.Player.MoveLocalRespawnOffShutter(
                    _room, PackedPosition,(_data.DoorAngle(_record.SubId)>>1)&3);
                // asm15 continues to retscript, which yields to the caller.
                _state = _controlledByTrigger ? DoorState.WatchingTrigger
                    : _closeAfterEntry || _controlledByTorches ? DoorState.SelectClosing : DoorState.CheckEnemiesBeforeClose;
                return;

            case DoorState.CheckEnemiesBeforeClose:
                // Without the room's complete enemy stream, closing the only
                // crossed route would strand Link behind a puzzle that cannot
                // be solved. Retain that one substituted floor tile for safe
                // backtracking; all non-entry shutters remain closed.
                if (!_enemyCompletionSupported)
                {
                    _state = DoorState.WaitingForEnemies;
                    return;
                }
                // The script checks the enemy count only after Link clears the
                // doorway. If it reached zero while the entry shutter was
                // already open, there is nothing left to close or solve.
                if (_roomEnemyCount() == 0)
                {
                    _state = DoorState.ScriptEnd;
                    return;
                }
                _state = DoorState.SelectClosing;
                return;

            case DoorState.ScriptEnd:
                Finished = true;
                return;

            case DoorState.FlipEntryScratch:
                _flipEntryScratch();
                _state = DoorState.ScriptEnd;
                return;

            case DoorState.WatchingTorches:
                DecideTorchAction();
                return;

            case DoorState.BeginTorchDelay:
                _counter = _data.DoorTorchWait;
                _state = DoorState.TorchDelay;
                return;

            case DoorState.TorchDelay:
                if (_counter != 0 && --_counter != 0) return;
                if (WaitForCounter2()) return;
                _playSound(_data.SolveSound);
                _state = DoorState.SelectOpening;
                return;

            case DoorState.SelectClosing:
                _state = DoorState.ReadyToClose;
                return;

            case DoorState.PlayTriggerSolve:
                _playSound(_data.SolveSound);
                _state = DoorState.SelectOpening;
                return;

            case DoorState.SelectOpening:
                _state = DoorState.ReadyToOpen;
                return;

            case DoorState.WatchingTrigger:
                DecideTriggerAction();
                return;

            case DoorState.ReadyToClose:
                // doorController state3/substate0 explicitly accepts
                // TILEINDEX_SOMARIA_BLOCK ($da) before testing collision.
                if (_room.GetMetatile(Position) != 0xda && _room.IsSolid(Position))
                {
                    ResumeScriptAfterAnimation(opening: false, frame.Player);
                    return;
                }
                PlayDoorSoundIfVisible();
                int closingTile = _data.ClosedTile(_record.SubId);
                _room.SetInterleavedMetatile(
                    Position, (byte)_data.OpenTile, (byte)closingTile,
                    closingTile & 0x03, _animationTick());
                _counter = _data.DoorFrameWait;
                _state = DoorState.ClosingInterleaved;
                return;

            case DoorState.ClosingInterleaved:
                _counter--;
                if (_counter != 0)
                    return;
                if (_room.GetPackedPosition(frame.Player.Position) == PackedPosition)
                    frame.Player.RequestForcedRespawn();
                _shutterSignal?.Invoke(false);
                _setTile((byte)PackedPosition,(byte)_data.ClosedTile(_record.SubId));
                PlayDoorSoundIfVisible();
                ResumeScriptAfterAnimation(opening: false, frame.Player);
                return;

            case DoorState.WaitingForEnemies:
                if (!_enemyCompletionSupported || _roomEnemyCount() != 0)
                    return;
                _state = DoorState.PlayEnemySolve;
                return;

            case DoorState.PlayEnemySolve:
                _playSound(_data.SolveSound);
                _state = DoorState.BeginSolveDelay;
                return;

            case DoorState.BeginSolveDelay:
                _counter = _data.SolveWait;
                _state = DoorState.SolveDelay;
                return;

            case DoorState.SolveDelay:
                // interactionRunScript ticks counter1 first, then counter2
                // on the same update that counter1 reaches zero.
                if (_counter != 0 && --_counter != 0) return;
                if (WaitForCounter2()) return;
                _state = DoorState.ReadyToOpen;
                return;

            case DoorState.ReadyToOpen:
                if (!_room.IsSolid(Position))
                {
                    ResumeScriptAfterAnimation(opening: true, frame.Player);
                    return;
                }
                PlayDoorSoundIfVisible();
                int closedTile = _data.ClosedTile(_record.SubId);
                _room.SetInterleavedMetatile(
                    Position, (byte)_data.OpenTile, (byte)closedTile,
                    closedTile & 0x03, _animationTick());
                _counter = _data.DoorFrameWait;
                _state = DoorState.OpeningInterleaved;
                return;

            case DoorState.OpeningInterleaved:
                _counter--;
                if (_counter != 0)
                    return;
                _shutterSignal?.Invoke(true);
                // doorController.s @setTileAndPlaySound still resumes its
                // script and plays the cue when the shared queue rejects it.
                _setTile((byte)PackedPosition,(byte)_data.OpenTile);
                PlayDoorSoundIfVisible();
                ResumeScriptAfterAnimation(opening: true, frame.Player);
                return;

            default:
                throw new InvalidOperationException(
                    $"Dungeon door ${_record.SubId:x2} at ${PackedPosition:x2} " +
                    $"entered state {_state}.");
        }
    }

    private bool ScriptPaused(Player player) => player.IsDying || _textActive();

    private bool WaitForCounter2()
    {
        if (Counter2Alias == 0) return false;
        // The source returns even on 1->0, moving only when still nonzero.
        Counter2Alias--;
        if (Counter2Alias != 0)
            Position = OracleObjectMovement.Shared.ApplySpeed(ref _precisePosition,_speed,_angle);
        return true;
    }

    private void ResumeScriptAfterAnimation(bool opening, Player player)
    {
        // @gotoState1 immediately runs the saved script pointer. Trigger
        // scriptjump + asm15 continue through the jump table in this update;
        // enemy scripts execute checknoenemies or scriptend instead.
        _state = _controlledByTrigger ? DoorState.WatchingTrigger
            : _closeAfterEntry ? DoorState.FlipEntryScratch
            : _controlledByTorches && !opening ? DoorState.WatchingTorches
            : opening ? DoorState.ScriptEnd : DoorState.WaitingForEnemies;
        if (ScriptPaused(player) || WaitForCounter2()) return;
        if (_controlledByTrigger)
        {
            _state = DoorState.WatchingTrigger;
            DecideTriggerAction();
        }
        else if (_closeAfterEntry)
        {
            _flipEntryScratch();
            _state = DoorState.ScriptEnd;
        }
        else if (_controlledByTorches && !opening) DecideTorchAction();
        else if (opening) Finished = true;
        else _state = _enemyCompletionSupported && _roomEnemyCount() == 0
            ? DoorState.PlayEnemySolve : DoorState.WaitingForEnemies;
    }

    private void PlayDoorSoundIfVisible()
    {
        if (OracleObjectMath.IsInsideOriginalScreenBoundary(
            _worldToScreen(Position)))
        {
            _playSound(_data.DoorSound);
        }
    }

    private void DecideTriggerAction()
    {
        bool active = _triggerActive(_record.Parameter & 0x07);
        if (active)
        {
            // scriptHelp checks the directional wRoomLayout byte, not
            // collision: another solid tile must not be opened as a door.
            if (_room.GetMetatile(Position) != _data.ClosedTile(_record.SubId))
                return;
            _state = DoorState.PlayTriggerSolve;
            return;
        }

        // The inactive branch tests the entire wRoomCollisions byte.
        if (_room.GetTerrainInfo(Position).Collision == 0)
            _state = DoorState.SelectClosing;
    }

    private void DecideTorchAction()
    {
        // Source helper compares equality, including surplus lit torches.
        if (_torchesLit() == _speed) _state = DoorState.BeginTorchDelay;
    }

    private bool OverlapsLink(Player player)
    {
        int radiusY = _data.DoorRadiusY(_record.SubId);
        int radiusX = _data.DoorRadiusX(_record.SubId);
        // commonScripts sets the same directional radii for trigger and
        // enemy shutters. checknotcollidedwithlink_ignorez uses byte XY,
        // includes the negative radius edge, and deliberately ignores Z.
        return RoomEntityManager.ObjectCollisionXYOverlaps(
            new Rect2(Position-new Vector2(radiusX,radiusY),new Vector2(radiusX*2,radiusY*2)),
            new Rect2(player.Position-Vector2.One*NpcCharacter.LinkCollisionRadius,
                Vector2.One*(NpcCharacter.LinkCollisionRadius*2)));
    }

}

internal enum DoorState
{
    Initialize,
    SetRadii,
    SetAngle,
    InitialBranch,
    CallLinkWait,
    WaitingForLinkClear,
    UpdateRespawn,
    CheckEnemiesBeforeClose,
    ScriptEnd,
    WatchingTrigger,
    SelectClosing,
    PlayTriggerSolve,
    SelectOpening,
    ReadyToClose,
    ClosingInterleaved,
    WaitingForEnemies,
    PlayEnemySolve,
    BeginSolveDelay,
    SolveDelay,
    ReadyToOpen,
    OpeningInterleaved,
    SetSpeed,
    FlipEntryScratch,
    WatchingTorches,
    BeginTorchDelay,
    TorchDelay
}
