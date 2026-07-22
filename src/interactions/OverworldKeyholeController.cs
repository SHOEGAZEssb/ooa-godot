using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Implements nextToOverworldKeyhole for imported collision-table parameter
/// $06. Named keys are checked but retained; their room-specific event is
/// signalled only after the doubled 20-to-zero push counter completes.
/// </summary>
public partial class OverworldKeyholeController : Node
{
    private readonly RoomSession _rooms;
    private readonly InventoryState _inventory;
    private readonly RoomEntityManager _entities;
    private readonly OverworldKeyholeDatabase _database;
    private readonly Action<int> _playSound;
    private Func<int, int, bool>? _supportsEvent;
    private Action<int, int>? _triggerEvent;
    private int _pushCounter;
    private int _candidatePosition = -1;
    private bool _informativeTextShown;

    public event Action<string>? MessageRequested;

    internal OverworldKeyholeDatabase Database => _database;
    internal int RemainingPushFrames => _pushCounter;
    internal bool InformativeTextShown => _informativeTextShown;

    internal OverworldKeyholeController(
        RoomSession rooms,
        InventoryState inventory,
        RoomEntityManager entities,
        OverworldKeyholeDatabase database,
        Action<int> playSound)
    {
        _rooms = rooms;
        _inventory = inventory;
        _entities = entities;
        _database = database;
        _playSound = playSound;
        _pushCounter = database.Constants.PushCounter;
        _rooms.RoomChanged += (_, _) => Cancel();
    }

    internal void SetEventHandler(
        Func<int, int, bool> supportsEvent,
        Action<int, int> triggerEvent)
    {
        _supportsEvent = supportsEvent;
        _triggerEvent = triggerEvent;
    }

    public void UpdatePushAttempt(
        Vector2 linkPosition,
        Vector2I facing,
        Vector2 movementInput)
    {
        int group = _rooms.ActiveGroup;
        int roomId = _rooms.CurrentRoom.Id;
        if (_supportsEvent is null || !_supportsEvent(group, roomId) ||
            !_database.TryGet(group, roomId, out OverworldKeyholeDatabase.Record record) ||
            _rooms.SaveData.HasRoomFlag(group, roomId, _database.Constants.RoomFlag))
        {
            ResetPushCounter();
            return;
        }

        if (!InteractableTilePushGeometry.TryGetCardinalInput(
                movementInput, out Vector2I direction) ||
            direction != facing || direction != Vector2I.Up ||
            !InteractableTilePushGeometry.IsAlignedForPush(linkPosition) ||
            !TryGetKeyhole(linkPosition, direction, out int position, out Vector2 center))
        {
            ResetPushCounter();
            return;
        }

        if (_candidatePosition != position)
        {
            _candidatePosition = position;
            _pushCounter = _database.Constants.PushCounter;
        }

        // Like nextToKeyDoor, nextToOverworldKeyhole decrements the global
        // pushing counter twice until it reaches zero.
        _pushCounter--;
        if (_pushCounter != 0)
        {
            _pushCounter--;
            if (_pushCounter != 0)
                return;
        }

        if (!_inventory.HasTreasure(record.Treasure))
        {
            if (!_informativeTextShown)
            {
                _informativeTextShown = true;
                MessageRequested?.Invoke(_database.Constants.NoKeyMessage);
            }
            ResetPushCounter();
            return;
        }

        if (_triggerEvent is null)
        {
            throw new InvalidOperationException(
                $"Keyhole {group:x}:{roomId:x2} has no associated event handler.");
        }

        // The original checks the named-key treasure flag without calling
        // giveTreasure's inverse, so the key remains in inventory.
        _playSound(_database.Constants.OpenSound);
        _rooms.SaveData.SetRoomFlag(group, roomId, _database.Constants.RoomFlag);
        _triggerEvent(group, roomId);
        _entities.Spawn<OverworldKeyUseEffect>(
            new OverworldKeyUseSpawn(center, record, _database.Constants));
        ResetPushCounter();
    }

    internal void Cancel()
    {
        _informativeTextShown = false;
        ResetPushCounter();
    }

    private bool TryGetKeyhole(
        Vector2 linkPosition,
        Vector2I direction,
        out int position,
        out Vector2 center)
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
        return tile != 0xff &&
            _database.IsKeyholeTile(room.ActiveCollisions, tile);
    }

    private void ResetPushCounter()
    {
        _pushCounter = _database.Constants.PushCounter;
        _candidatePosition = -1;
    }
}
