using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Implements nextToKeyDoor for imported small-key tiles $70-$73 and boss-key
/// tiles $74-$77. A door checks the active dungeon's corresponding key,
/// records both sides of the dungeon-layout adjacency, and uses the ordinary
/// six-update interleaved-door frame. Only a small key is consumed.
/// </summary>
public partial class DungeonKeyDoorController : Node
{
    private readonly RoomSession _rooms;
    private readonly InventoryState _inventory;
    private readonly RoomEntityManager _entities;
    private readonly TreasureDatabase _treasures;
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
        _treasures = treasures;
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

        if (!InteractableTilePushGeometry.TryGetCardinalInput(
                movementInput, out Vector2I direction) ||
            direction != facing ||
            !InteractableTilePushGeometry.IsAlignedForPush(linkPosition) ||
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
        Vector2 frontPoint = linkPosition +
            InteractableTilePushGeometry.FrontTileOffset(direction);
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
        bool hasKey = door.UsesBossKey
            ? _inventory.HasDungeonBossKey(dungeon)
            : _inventory.TryUseDungeonSmallKey(dungeon);
        if (!hasKey)
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
                $"Dungeon-key door ${door.ClosedTile:x2} in room {group:x1}:{room:x2} " +
                "has no matching dungeon-layout neighbor.");
        }

        _rooms.SaveData.SetRoomFlag(group, room, door.RoomFlag);
        _rooms.SaveData.SetRoomFlag(group, neighbor, door.OppositeRoomFlag);
        _entities.Spawn<DungeonKeyUseEffect>(
            new DungeonKeyUseSpawn(
                center, _treasures.GetObjectVisual(door.KeyGraphic)));

        _doorCenter = center;
        _door = door;
        _rooms.CurrentRoom.SetInterleavedMetatile(
            center, door.OpenTile, door.ClosedTile,
            InteractableTilePushGeometry.DirectionIndex(door.Direction), _animationTick());
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

}
