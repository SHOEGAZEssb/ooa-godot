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
        : this(record.PackedPosition, name)
    {
    }

    protected DungeonMechanicRoomEntity(int packedPosition, string name)
    {
        Name = name;
        Position = PositionFromPacked(packedPosition);
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
/// Common PART_BUTTON $09 handler. Subid bit 7 selects a reusable pressure
/// button; bits 0-2 select the shared wActiveTriggers bit.
/// </summary>
internal sealed partial class GroundButtonRoomEntity : DungeonMechanicRoomEntity,
    IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly DungeonMechanicDatabase.Record _record;
    private readonly OracleRoomData _room;
    private readonly DungeonMechanicDatabase _data;
    private readonly Action<int, bool> _setTrigger;
    private readonly Func<long> _animationTick;
    private readonly Action<int> _playSound;
    private bool _initialized;
    private bool _pressed;
    private bool _latchedBelowObject;
    private int _releaseCounter;

    internal int SubId => _record.SubId;
    internal int PackedPosition => _record.PackedPosition;
    internal int TriggerBit => _record.SubId & 0x07;
    internal bool Reusable => (_record.SubId & 0x80) != 0;
    internal bool Pressed => _pressed;
    internal int ReleaseCounter => _releaseCounter;
    public bool Finished { get; private set; }

    internal GroundButtonRoomEntity(
        DungeonMechanicDatabase.Record record,
        OracleRoomData room,
        DungeonMechanicDatabase data,
        Action<int, bool> setTrigger,
        Func<long> animationTick,
        Action<int> playSound)
        : base(record, $"GroundButton_{record.SubId:x2}_{record.Order}")
    {
        if (record.Id != 0x09)
            throw new ArgumentOutOfRangeException(nameof(record));
        _record = record;
        _room = room;
        _data = data;
        _setTrigger = setTrigger;
        _animationTick = animationTick;
        _playSound = playSound;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        // State 0 copies subid bits 0-2 to var03 and returns without checking
        // pressure until the following update.
        if (!_initialized)
        {
            _initialized = true;
            return;
        }

        byte tile = _room.GetMetatile(Position);
        if (_latchedBelowObject)
        {
            if (tile != _data.ButtonTile && tile != _data.PressedButtonTile)
                return;
            SetButtonTile((byte)_data.PressedButtonTile);
            Finished = true;
            return;
        }

        if (TouchesLink(frame.Player))
        {
            if (_pressed)
                return;
            Press();
            if (tile == _data.ButtonTile || tile == _data.PressedButtonTile)
            {
                SetButtonTile((byte)_data.PressedButtonTile);
                if (!Reusable)
                    Finished = true;
            }
            else if (!Reusable)
            {
                // setTileInRoomLayoutBuffer leaves the object above the
                // button visible. Keep a tiny helper alive until the runtime
                // push-block controller restores the underlying tile.
                _latchedBelowObject = true;
            }
            return;
        }

        bool somethingOnButton = tile != _data.ButtonTile &&
            tile != _data.PressedButtonTile;
        if (somethingOnButton)
        {
            if (_pressed)
                return;
            Press();
            if (Reusable)
            {
                _releaseCounter = _data.ButtonObjectReleaseDelay;
            }
            else
            {
                _latchedBelowObject = true;
            }
            return;
        }

        if (_releaseCounter != 0)
        {
            // A stationary object hides the button tile. The original writes
            // $0d to wRoomLayoutBuffer, so it is revealed pressed when the
            // object moves; the runtime represents that reveal explicitly.
            if (tile == _data.ButtonTile)
                SetButtonTile((byte)_data.PressedButtonTile);
            _releaseCounter--;
            if (_releaseCounter != 0)
                return;
        }

        if (!_pressed)
            return;
        SetButtonTile((byte)_data.ButtonTile);
        _setTrigger(TriggerBit, false);
        _pressed = false;
        _playSound(_data.ButtonSound);
    }

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }

    private bool TouchesLink(Player player)
    {
        if (!player.IsGroundedForFloorButton)
            return false;
        Vector2 link = OracleObjectMath.ToPixelPosition(player.Position);
        Vector2 button = OracleObjectMath.ToPixelPosition(Position);
        return Mathf.Abs(link.Y - button.Y) <
                _data.ButtonRadiusY + NpcCharacter.LinkCollisionRadius &&
            Mathf.Abs(link.X - button.X) <
                _data.ButtonRadiusX + NpcCharacter.LinkCollisionRadius;
    }

    private void Press()
    {
        _setTrigger(TriggerBit, true);
        _pressed = true;
        _playSound(_data.ButtonSound);
    }

    private void SetButtonTile(byte tile) => _room.SetPositionTileAndCollision(
        Position, tile, null, _animationTick());
}

/// <summary>
/// Trigger-chest consumers used by dungeon script $20:$00 and dungeon event
/// $21:$17. The former plays the solve cue, creates a puff, waits 15 updates,
/// installs a permanent chest, and deletes. The latter mirrors trigger state
/// immediately and restores the source layout tile when pressure is released.
/// </summary>
internal sealed partial class TriggerChestRoomEntity : DungeonMechanicRoomEntity,
    IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly DungeonMechanicDatabase.Record _record;
    private readonly OracleRoomData _room;
    private readonly DungeonMechanicDatabase _data;
    private readonly Func<int> _triggerState;
    private readonly Func<bool> _itemFlagSet;
    private readonly Func<long> _animationTick;
    private readonly Action<int> _playSound;
    private readonly byte _originalTile;
    private bool _initialized;
    private int _counter;

    internal int Id => _record.Id;
    internal int SubId => _record.SubId;
    internal int PackedPosition => _record.PackedPosition;
    internal int TriggerParameter => _record.Parameter;
    internal DungeonMechanicDatabase.TriggerPredicate Predicate => _record.Predicate;
    internal int Counter => _counter;
    internal byte OriginalTile => _originalTile;
    public bool Finished { get; private set; }

    internal TriggerChestRoomEntity(
        DungeonMechanicDatabase.Record record,
        OracleRoomData room,
        DungeonMechanicDatabase data,
        Func<int> triggerState,
        Func<bool> itemFlagSet,
        Func<long> animationTick,
        Action<int> playSound)
        : base(record, $"TriggerChest_{record.Id:x2}_{record.Order}")
    {
        if (record is not ({ Id: 0x20, SubId: 0x00 } or
            { Id: 0x21, SubId: 0x17 }))
        {
            throw new ArgumentOutOfRangeException(nameof(record));
        }
        _record = record;
        _room = room;
        _data = data;
        _triggerState = triggerState;
        _itemFlagSet = itemFlagSet;
        _animationTick = animationTick;
        _playSound = playSound;
        _originalTile = room.GetOriginalMetatile(Position);
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (_record.Id == 0x21)
        {
            UpdateRetractable(spawns);
            return;
        }

        // INTERAC_DUNGEON_SCRIPT executes stopifitemflagset once when its
        // script is selected, then remains parked at its trigger predicate.
        if (!_initialized)
        {
            _initialized = true;
            if (_itemFlagSet())
            {
                Finished = true;
                return;
            }
        }

        if (_counter != 0)
        {
            _counter--;
            if (_counter == 0)
            {
                SetTile((byte)_data.ChestTile);
                Finished = true;
            }
            return;
        }

        if (!TriggerMatches())
            return;

        // spawnChestAfterPuff requests the solve cue before allocating
        // INTERAC_PUFF, then wait 15 reaches zero and executes settilehere in
        // that same update.
        _playSound(_data.SolveSound);
        spawns.Add(new PuzzlePuffSpawn(Position, _data.PuffSound));
        _counter = _data.ChestWait;
    }

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }

    private void UpdateRetractable(ICollection<RoomEntitySpawn> spawns)
    {
        // $21:$17 checks ROOMFLAG_ITEM on every update, so opening the dynamic
        // chest retires this controller before it can restore the source tile.
        if (_itemFlagSet())
        {
            Finished = true;
            return;
        }

        byte tile = _room.GetMetatile(Position);
        if (TriggerMatches())
        {
            if (tile == _data.ChestTile)
                return;
            SetTile((byte)_data.ChestTile);
            spawns.Add(new PuzzlePuffSpawn(Position, _data.PuffSound));
            _playSound(_data.SolveSound);
            return;
        }

        if (tile != _data.ChestTile)
            return;
        SetTile(_originalTile);
        spawns.Add(new PuzzlePuffSpawn(Position, _data.PuffSound));
    }

    private bool TriggerMatches() => _record.Predicate switch
    {
        DungeonMechanicDatabase.TriggerPredicate.BitSet
            when _record.Parameter is >= 0 and <= 7 =>
                (_triggerState() & (1 << _record.Parameter)) != 0,
        DungeonMechanicDatabase.TriggerPredicate.Exact =>
            _triggerState() == _record.Parameter,
        _ => throw new InvalidOperationException(
            $"Unsupported trigger predicate for ${_record.Id:x2}:" +
            $"${_record.SubId:x2} in room {_record.Group:x1}:{_record.Room:x2}.")
    };

    private void SetTile(byte tile) => _room.SetPositionTileAndCollision(
        Position, tile, null, _animationTick());
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
                // loadTilesetAndRoomLayout restores the source buffer before
                // this object initializes. OracleWorldData caches mutable room
                // instances, so read that source layout explicitly instead of
                // treating a stale temporary `$1d sentinel as the real block.
                _originalTile = _room.GetOriginalMetatile(Position);
                _originalCollision = _room.GetCollision(_originalTile);
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

internal static class DungeonShutterEntry
{
    internal const int FirstNormalShutterTile = 0x78;
    internal const int LastNormalShutterTile = 0x7b;

    internal static bool Matches(
        EnemyPlacementContext placementContext,
        int packedPosition,
        int doorDirection)
    {
        if (placementContext.Kind != EnemyPlacementEntryKind.Scrolling ||
            placementContext.EntryPackedPosition != packedPosition)
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
        return doorDirection == incomingDoorDirection;
    }
}

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
    private enum DoorState
    {
        Initialize,
        WaitingForLinkClear,
        WatchingTrigger,
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
        DungeonMechanicDatabase.Record record,
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
        DungeonMechanicDatabase.Record record,
        EnemyPlacementContext placementContext) =>
        DungeonShutterEntry.Matches(
            placementContext, record.PackedPosition, record.SubId & 0x03);
}
