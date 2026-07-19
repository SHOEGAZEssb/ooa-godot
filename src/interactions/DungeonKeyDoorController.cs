using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Implements nextToKeyDoor for the imported dungeon tiles $70-$73. A door
/// consumes the active dungeon's small key, records both sides of the dungeon
/// layout adjacency, and uses the ordinary six-update interleaved-door frame.
/// </summary>
public partial class DungeonKeyDoorController : Node
{
    private readonly RoomSession _rooms;
    private readonly InventoryState _inventory;
    private readonly RoomEntityManager _entities;
    private readonly TreasureDatabase.TreasureObjectVisualRecord _keyVisual;
    private readonly Func<long> _animationTick;
    private readonly Action<int> _playSound;
    private int _pushCounter;
    private int _candidatePosition = -1;
    private Vector2I _candidateDirection;
    private bool _opening;
    private int _openingCounter;
    private double _openingTicks;
    private Vector2 _doorCenter;
    private DungeonKeyDoorDatabase.Record _door;

    public event Action<string>? MessageRequested;

    internal bool Opening => _opening;
    internal int RemainingPushFrames => _pushCounter;
    internal int OpeningCounter => _openingCounter;

    public DungeonKeyDoorController(
        RoomSession rooms,
        InventoryState inventory,
        RoomEntityManager entities,
        TreasureDatabase treasures,
        Func<long> animationTick,
        Action<int> playSound)
    {
        _rooms = rooms;
        _inventory = inventory;
        _entities = entities;
        _keyVisual = treasures.GetObjectVisual(0x42);
        _animationTick = animationTick;
        _playSound = playSound;
        _pushCounter = DefaultPushCounter;
        _rooms.RoomChanged += (_, _) => Cancel();
    }

    private int DefaultPushCounter => _door.PushCounter > 0
        ? _door.PushCounter
        : 20;

    public void UpdatePushAttempt(
        Vector2 linkPosition,
        Vector2I facing,
        Vector2 movementInput)
    {
        if (_opening)
            return;

        if (!TryGetCardinalInput(movementInput, out Vector2I direction) ||
            direction != facing || !IsAlignedForPush(linkPosition) ||
            !TryGetDoor(linkPosition, direction, out int position,
                out Vector2 center, out DungeonKeyDoorDatabase.Record door))
        {
            ResetPushCounter();
            return;
        }

        if (_candidatePosition != position || _candidateDirection != direction)
        {
            _candidatePosition = position;
            _candidateDirection = direction;
            _pushCounter = door.PushCounter;
        }

        // nextToKeyDoor calls decPushingAgainstTileCounter, then decrements
        // the same byte once more when it is still nonzero. The imported
        // initial value is 20, so a continuously pushed door activates on
        // the tenth original update.
        _pushCounter--;
        if (_pushCounter != 0)
        {
            _pushCounter--;
            if (_pushCounter != 0)
                return;
        }

        TryOpen(center, door);
    }

    public override void _PhysicsProcess(double delta) => Advance(delta);

    internal void Advance(double delta)
    {
        if (!_opening || delta <= 0.0)
            return;

        _openingTicks += delta * OracleSoundEngine.UpdatesPerSecond;
        while (_opening && _openingTicks >= 1.0)
        {
            _openingTicks -= 1.0;
            _openingCounter--;
            if (_openingCounter != 0)
                continue;

            _rooms.CurrentRoom.SetPositionTileAndCollision(
                _doorCenter, _door.OpenTile, null, _animationTick());
            _playSound(_door.DoorSound);
            _opening = false;
        }
    }

    internal void Cancel()
    {
        _opening = false;
        _openingCounter = 0;
        _openingTicks = 0.0;
        ResetPushCounter();
    }

    private bool TryGetDoor(
        Vector2 linkPosition,
        Vector2I direction,
        out int position,
        out Vector2 center,
        out DungeonKeyDoorDatabase.Record door)
    {
        OracleRoomData room = _rooms.CurrentRoom;
        Vector2 frontPoint = linkPosition + FrontTileOffset(direction);
        position = room.GetPackedPosition(frontPoint);
        int tileX = position & 0x0f;
        int tileY = position >> 4;
        center = new Vector2(
            tileX * OracleRoomData.MetatileSize + 8,
            tileY * OracleRoomData.MetatileSize + 8);
        byte tile = room.GetMetatile(frontPoint);
        door = default;
        return tile != 0xff && _rooms.KeyDoors.TryGet(tile, out door) &&
            door.Direction == direction;
    }

    private void TryOpen(
        Vector2 center,
        DungeonKeyDoorDatabase.Record door)
    {
        int dungeon = _rooms.CurrentDungeonIndex;
        if (!_inventory.TryUseDungeonSmallKey(dungeon))
        {
            MessageRequested?.Invoke(door.NoKeyMessage);
            ResetPushCounter();
            return;
        }

        int group = _rooms.ActiveGroup;
        int room = _rooms.CurrentRoom.Id;
        if (!_rooms.TryGetNeighbor(door.Direction, out int neighbor))
        {
            throw new InvalidOperationException(
                $"Small-key door ${door.ClosedTile:x2} in room {group:x1}:{room:x2} " +
                "has no matching dungeon-layout neighbor.");
        }

        _rooms.SaveData.SetRoomFlag(group, room, door.RoomFlag);
        _rooms.SaveData.SetRoomFlag(group, neighbor, door.OppositeRoomFlag);
        _entities.Spawn<DungeonKeyUseEffect>(
            new DungeonKeyUseSpawn(center, _keyVisual));

        _doorCenter = center;
        _door = door;
        _rooms.CurrentRoom.SetInterleavedMetatile(
            center, door.OpenTile, door.ClosedTile,
            DirectionIndex(door.Direction), _animationTick());
        _playSound(door.DoorSound);
        _openingCounter = door.DoorFrameWait;
        _openingTicks = 0.0;
        _opening = true;
        ResetPushCounter();
    }

    private void ResetPushCounter()
    {
        _pushCounter = DefaultPushCounter;
        _candidatePosition = -1;
        _candidateDirection = Vector2I.Zero;
    }

    private static bool TryGetCardinalInput(Vector2 input, out Vector2I direction)
    {
        const float threshold = 0.01f;
        bool horizontal = Mathf.Abs(input.X) > threshold;
        bool vertical = Mathf.Abs(input.Y) > threshold;
        if (horizontal == vertical)
        {
            direction = Vector2I.Zero;
            return false;
        }
        direction = horizontal
            ? (input.X > 0 ? Vector2I.Right : Vector2I.Left)
            : (input.Y > 0 ? Vector2I.Down : Vector2I.Up);
        return true;
    }

    private static bool IsAlignedForPush(Vector2 position)
    {
        static bool AxisIsAwayFromCorner(float coordinate)
        {
            int withinTile = Mathf.PosMod(Mathf.FloorToInt(coordinate),
                OracleRoomData.MetatileSize);
            return withinTile is >= 3 and <= 13;
        }

        return AxisIsAwayFromCorner(position.Y) ||
            AxisIsAwayFromCorner(position.X);
    }

    private static Vector2 FrontTileOffset(Vector2I direction) => direction switch
    {
        var d when d == Vector2I.Up => new Vector2(0, -4),
        var d when d == Vector2I.Right => new Vector2(7, 0),
        var d when d == Vector2I.Down => new Vector2(0, 8),
        _ => new Vector2(-8, 0)
    };

    private static int DirectionIndex(Vector2I direction) => direction switch
    {
        var d when d == Vector2I.Up => 0,
        var d when d == Vector2I.Right => 1,
        var d when d == Vector2I.Down => 2,
        _ => 3
    };
}
