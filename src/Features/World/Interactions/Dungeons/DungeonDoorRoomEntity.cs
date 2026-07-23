using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Common shutter variants $1e:$04-$0b. Trigger-controlled doors observe one
/// wActiveTriggers bit; enemy shutters read the live room enemy count. Both
/// use the original mapping-level interleaving for opening and closing. An
/// enemy shutter whose full enemy stream is not implemented still handles the
/// crossed-entry substitution, but leaves that one route open for safe
/// backtracking instead of trapping Link or falsely solving the room.
/// </summary>
internal sealed partial class DungeonDoorRoomEntity : DungeonMechanicRoomEntity,
    IFixedRoomEntity, IRoomEntityLifetime, IBossShutterState
{

    private readonly DungeonMechanicDatabaseRecord _record;
    private readonly OracleRoomData _room;
    private readonly DungeonMechanicDatabase _data;
    private readonly Func<int> _roomEnemyCount;
    private readonly Func<int, bool> _triggerActive;
    private readonly Func<Vector2, Vector2> _worldToScreen;
    private readonly Func<long> _animationTick;
    private readonly Action<int> _playSound;
    private readonly bool _enteredThroughThisDoor;
    private readonly bool _controlledByTrigger;
    private readonly bool _enemyCompletionSupported;
    private DoorState _state;
    private int _counter;

    internal int SubId => _record.SubId;
    internal int PackedPosition => _record.PackedPosition;
    internal bool EnteredThroughThisDoor => _enteredThroughThisDoor;
    internal bool EnemyCompletionSupported => _enemyCompletionSupported;
    public bool BossIntroReady => _record.SubId < 0x08 ||
        _room.IsSolid(Position) && _state is
            DoorState.WaitingForEnemies or DoorState.SolveDelay or
            DoorState.ReadyToOpen;
    public bool Finished { get; private set; }

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
        bool enemyCompletionSupported)
        : base(record, $"DungeonDoor_{record.SubId:x2}_{record.Order}")
    {
        if (record.Id != 0x1e || record.SubId is < 0x04 or > 0x0b)
            throw new ArgumentOutOfRangeException(nameof(record));
        _record = record;
        _room = room;
        _data = data;
        _roomEnemyCount = roomEnemyCount;
        _triggerActive = triggerActive;
        _worldToScreen = worldToScreen;
        _animationTick = animationTick;
        _playSound = playSound;
        _enteredThroughThisDoor = IsEnteredShutter(record, placementContext);
        _controlledByTrigger = record.SubId <= 0x07;
        _enemyCompletionSupported = _controlledByTrigger || enemyCompletionSupported;

        // loadTilesetAndRoomLayout restores the source layout on every room
        // parse. replaceShutterForLinkEntering changes only the shutter on
        // Link's incoming packed position to floor before object parsing.
        // OracleWorldData caches mutable room instances, so reproduce either
        // source state explicitly before state 0 runs.
        _room.SetPositionTileAndCollision(
            Position,
            (byte)(_enteredThroughThisDoor
                ? _data.OpenTile
                : _data.ClosedTile(_record.SubId)),
            null,
            _animationTick());
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        switch (_state)
        {
            case DoorState.Initialize:
                _state = _controlledByTrigger
                    ? _enteredThroughThisDoor
                        ? DoorState.WaitingForLinkClear
                        : DoorState.WatchingTrigger
                    : _enemyCompletionSupported && _roomEnemyCount() == 0
                        ? DoorState.ReadyToOpen
                        : _enteredThroughThisDoor
                            ? DoorState.WaitingForLinkClear
                            : DoorState.WaitingForEnemies;
                return;

            case DoorState.WaitingForLinkClear:
                if (OverlapsLink(frame.Player))
                    return;
                frame.Player.MoveLocalRespawnOffShutter(
                    _room, PackedPosition, _record.SubId);
                if (_controlledByTrigger)
                {
                    _state = DoorState.WatchingTrigger;
                    DecideTriggerAction();
                    return;
                }
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
                    Finished = true;
                    return;
                }
                _state = DoorState.ReadyToClose;
                return;

            case DoorState.WatchingTrigger:
                DecideTriggerAction();
                return;

            case DoorState.ReadyToClose:
                if (_room.IsSolid(Position))
                {
                    _state = DoorState.WaitingForEnemies;
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
                    frame.Player.BeginFloorDoorRespawn();
                _room.SetPositionTileAndCollision(
                    Position, (byte)_data.ClosedTile(_record.SubId), null,
                    _animationTick());
                PlayDoorSoundIfVisible();
                if (_controlledByTrigger)
                {
                    _state = DoorState.WatchingTrigger;
                    DecideTriggerAction();
                }
                else
                {
                    _state = DoorState.WaitingForEnemies;
                }
                return;

            case DoorState.WaitingForEnemies:
                if (!_enemyCompletionSupported || _roomEnemyCount() != 0)
                    return;
                _playSound(_data.SolveSound);
                _counter = _data.SolveWait;
                _state = DoorState.SolveDelay;
                return;

            case DoorState.SolveDelay:
                _counter--;
                if (_counter == 0)
                    _state = DoorState.ReadyToOpen;
                return;

            case DoorState.ReadyToOpen:
                if (!_room.IsSolid(Position))
                {
                    if (_controlledByTrigger)
                        _state = DoorState.WatchingTrigger;
                    else
                        Finished = true;
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
                _room.SetPositionTileAndCollision(
                    Position, (byte)_data.OpenTile, null, _animationTick());
                PlayDoorSoundIfVisible();
                if (_controlledByTrigger)
                {
                    _state = DoorState.WatchingTrigger;
                    DecideTriggerAction();
                }
                else
                {
                    Finished = true;
                }
                return;

            default:
                throw new InvalidOperationException(
                    $"Dungeon door ${_record.SubId:x2} at ${PackedPosition:x2} " +
                    $"entered state {_state}.");
        }
    }

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }

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
            if (!_room.IsSolid(Position))
                return;
            _playSound(_data.SolveSound);
            _state = DoorState.ReadyToOpen;
            return;
        }

        if (!_room.IsSolid(Position))
            _state = DoorState.ReadyToClose;
    }

    private bool OverlapsLink(Player player)
    {
        bool vertical = _record.SubId is 0x08 or 0x0a;
        int radiusY = vertical ? 0x0a : 0x08;
        int radiusX = vertical ? 0x08 : 0x0a;
        Vector2 link = OracleObjectMath.ToPixelPosition(player.Position);
        Vector2 door = OracleObjectMath.ToPixelPosition(Position);
        return Mathf.Abs(link.Y - door.Y) < radiusY + NpcCharacter.LinkCollisionRadius &&
            Mathf.Abs(link.X - door.X) < radiusX + NpcCharacter.LinkCollisionRadius;
    }

    private static bool IsEnteredShutter(
        DungeonMechanicDatabaseRecord record,
        EnemyPlacementContext placementContext) =>
        DungeonShutterEntry.Matches(
            placementContext, record.PackedPosition, record.SubId & 0x03);
}
