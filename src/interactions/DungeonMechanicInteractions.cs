using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal abstract partial class DungeonMechanicRoomEntity : Node2D, IRoomEntity
{
    public Node2D Node => this;

    protected DungeonMechanicRoomEntity(
        DungeonMechanicDatabase.Record record,
        string name)
    {
        Name = name;
        Position = PositionFromPacked(record.PackedPosition);
    }

    public void SetTransitionDrawOffset(Vector2 offset)
    {
        // These interactions are invisible and mutate the room tilemap only.
        // Destination room entities are frozen until scrolling completes.
    }

    private static Vector2 PositionFromPacked(int packedPosition) => new(
        (packedPosition & 0x0f) * OracleRoomData.MetatileSize + 8,
        (packedPosition >> 4) * OracleRoomData.MetatileSize + 8);
}

/// <summary>
/// Native $13:$01 handler. It temporarily contributes one to the original
/// wNumEnemies count and releases all enemies 30 updates after its block moves.
/// </summary>
internal sealed partial class PushBlockTriggerRoomEntity : DungeonMechanicRoomEntity,
    IFixedRoomEntity, IRoomEntityLifetime, IRoomEnemyCounterEntity
{
    private readonly OracleRoomData _room;
    private readonly DungeonMechanicDatabase _data;
    private readonly Func<int> _roomEnemyCount;
    private readonly Func<long> _animationTick;
    private int _state;
    private int _counter;
    private byte _originalTile;
    private byte _originalCollision;

    internal int PackedPosition { get; }
    public bool Finished { get; private set; }
    public bool CountsAsEnemy => _state != 0 && !Finished;

    internal PushBlockTriggerRoomEntity(
        DungeonMechanicDatabase.Record record,
        OracleRoomData room,
        DungeonMechanicDatabase data,
        Func<int> roomEnemyCount,
        Func<long> animationTick)
        : base(record, $"PushBlockTrigger_{record.Order}")
    {
        if (record is not { Id: 0x13, SubId: 0x01 })
            throw new ArgumentOutOfRangeException(nameof(record));
        _room = room;
        _data = data;
        _roomEnemyCount = roomEnemyCount;
        _animationTick = animationTick;
        PackedPosition = record.PackedPosition;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        switch (_state)
        {
            case 0:
                _state = 1;
                _originalTile = _room.GetMetatile(Position);
                _originalCollision = _room.GetTerrainInfo(Position).Collision;
                _room.SetPositionTileAndCollision(
                    Position, (byte)_data.PushableBlock, _originalCollision,
                    _animationTick(), preserveRenderedTile: true);
                return;

            case 1:
                // subid $01 waits until wNumEnemies is no greater than one;
                // this interaction itself is that one synthetic enemy.
                if (_roomEnemyCount() > 1)
                    return;
                _state = 2;
                _room.SetPositionTileAndCollision(
                    Position, _originalTile, _originalCollision,
                    _animationTick(), preserveRenderedTile: true);
                return;

            case 2:
                if (_room.GetMetatile(Position) == _originalTile)
                    return;
                _state = 3;
                _counter = _data.PushDelay;
                return;

            case 3:
                _counter--;
                if (_counter == 0)
                    Finished = true;
                return;

            default:
                throw new InvalidOperationException(
                    $"Push-block trigger at ${PackedPosition:x2} entered state {_state}.");
        }
    }

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}

/// <summary>
/// Common enemy-shutter variants $1e:$08-$0b. The interaction reads the live
/// room enemy count, runs the original solve delay, and uses mapping-level
/// interleaving before the final open metatile write.
/// </summary>
internal sealed partial class DungeonDoorRoomEntity : DungeonMechanicRoomEntity,
    IFixedRoomEntity, IRoomEntityLifetime
{
    private enum DoorState
    {
        Initialize,
        WaitingForLinkClear,
        ReadyToClose,
        ClosingInterleaved,
        WaitingForEnemies,
        SolveDelay,
        ReadyToOpen,
        OpeningInterleaved
    }

    private readonly DungeonMechanicDatabase.Record _record;
    private readonly OracleRoomData _room;
    private readonly DungeonMechanicDatabase _data;
    private readonly Func<int> _roomEnemyCount;
    private readonly Func<Vector2, Vector2> _worldToScreen;
    private readonly Func<long> _animationTick;
    private readonly Action<int> _playSound;
    private readonly bool _enteredThroughThisDoor;
    private DoorState _state;
    private int _counter;

    internal int SubId => _record.SubId;
    internal int PackedPosition => _record.PackedPosition;
    public bool Finished { get; private set; }

    internal DungeonDoorRoomEntity(
        DungeonMechanicDatabase.Record record,
        OracleRoomData room,
        DungeonMechanicDatabase data,
        Func<int> roomEnemyCount,
        Func<Vector2, Vector2> worldToScreen,
        Func<long> animationTick,
        Action<int> playSound,
        EnemyPlacementContext placementContext)
        : base(record, $"DungeonDoor_{record.SubId:x2}_{record.Order}")
    {
        if (record.Id != 0x1e || record.SubId is < 0x08 or > 0x0b)
            throw new ArgumentOutOfRangeException(nameof(record));
        _record = record;
        _room = room;
        _data = data;
        _roomEnemyCount = roomEnemyCount;
        _worldToScreen = worldToScreen;
        _animationTick = animationTick;
        _playSound = playSound;
        _enteredThroughThisDoor = IsEnteredShutter(record, placementContext);

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
                _state = _roomEnemyCount() == 0
                    ? DoorState.ReadyToOpen
                    : _enteredThroughThisDoor
                        ? DoorState.WaitingForLinkClear
                        : DoorState.WaitingForEnemies;
                return;

            case DoorState.WaitingForLinkClear:
                if (OverlapsLink(frame.Player))
                    return;
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
                _room.SetPositionTileAndCollision(
                    Position, (byte)_data.ClosedTile(_record.SubId), null,
                    _animationTick());
                PlayDoorSoundIfVisible();
                _state = DoorState.WaitingForEnemies;
                return;

            case DoorState.WaitingForEnemies:
                if (_roomEnemyCount() != 0)
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
                Finished = true;
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
        DungeonMechanicDatabase.Record record,
        EnemyPlacementContext placementContext)
    {
        if (placementContext.Kind != EnemyPlacementEntryKind.Scrolling ||
            placementContext.EntryPackedPosition != record.PackedPosition)
        {
            return false;
        }

        int incomingDoorDirection = placementContext.ScrollDirection switch
        {
            var direction when direction == Vector2I.Up => 2,
            var direction when direction == Vector2I.Right => 3,
            var direction when direction == Vector2I.Down => 0,
            var direction when direction == Vector2I.Left => 1,
            _ => throw new ArgumentOutOfRangeException(
                nameof(placementContext), placementContext.ScrollDirection,
                "Scroll direction must be cardinal.")
        };
        return record.SubId - 0x08 == incomingDoorDirection;
    }
}
