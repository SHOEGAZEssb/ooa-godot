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
    private readonly OracleSoundEngine _sound;
    private Func<int, int, bool>? _supportsEvent;
    private Action<int, int>? _triggerEvent;
    private int _pushCounter { get => _rooms.TilePushCounter; set => _rooms.TilePushCounter = unchecked((byte)value); }

    public event Action<string>? MessageRequested;

    internal OverworldKeyholeDatabase Database => _database;
    internal int RemainingPushFrames => _pushCounter;
    internal bool InformativeTextShown => _rooms.TileInfoTextShown(0x5109);

    internal OverworldKeyholeController(
        RoomSession rooms,
        InventoryState inventory,
        RoomEntityManager entities,
        OverworldKeyholeDatabase database,
        OracleSoundEngine sound)
    {
        _rooms = rooms;
        _inventory = inventory;
        _entities = entities;
        _database = database;
        _sound = sound;
    }

    internal void SetEventHandler(
        Func<int, int, bool> supportsEvent,
        Action<int, int> triggerEvent)
    {
        _supportsEvent = supportsEvent;
        _triggerEvent = triggerEvent;
    }

    public bool UpdatePushAttempt(
        Vector2 linkPosition,
        Vector2I facing,
        Vector2 movementInput)
    {
        int group = _rooms.ActiveGroup;
        int roomId = _rooms.CurrentRoom.Id;
        // The opened keyhole handler returns before touching the contact
        // clock, even with neutral input. Ordinary front tiles still reset it.
        if (_rooms.SaveData.HasRoomFlag(group, roomId, _database.Constants.RoomFlag) &&
            TryGetKeyhole(linkPosition, facing, out _, out _))
            return false;
        if (!InteractableTilePushGeometry.TryGetCardinalInput(
                movementInput, out _) ||
            !InteractableTilePushGeometry.IsAlignedForPush(linkPosition) ||
            !TryGetKeyhole(linkPosition, facing, out _, out Vector2 center))
        {
            ResetPushCounter();
            return false;
        }
        // checkFacingBottomOfTile rejects side/top contact without touching
        // the shared byte; only a failed pushing gate resets the wait.
        if (facing != Vector2I.Up) return false;

        // Like nextToKeyDoor, nextToOverworldKeyhole decrements the global
        // pushing counter twice until it reaches zero.
        int counter = _pushCounter;
        bool expired = PushingAgainstTileCounter.DecrementTwiceToZero(ref counter);
        _pushCounter = counter;
        if (!expired)
            return false;

        if (!_database.TryGet(group,roomId,out OverworldKeyholeDatabaseRecord record) ||
            !_inventory.HasTreasure(record.Treasure))
        {
            if (_rooms.PrepareTileInfoMessage(0x5109) is { } message)
                MessageRequested?.Invoke(message);
            ResetPushCounter();
            return _entities.TextActiveSource();
        }

        if (_triggerEvent is null || _supportsEvent is null || !_supportsEvent(group,roomId))
        {
            throw new InvalidOperationException(
                $"Keyhole {group:x}:{roomId:x2} has no associated event handler.");
        }

        // The original checks the named-key treasure flag without calling
        // giveTreasure's inverse, so the key remains in inventory.
        _sound.PlaySound(_database.Constants.OpenSound);
        _rooms.SaveData.SetRoomFlag(group, roomId, _database.Constants.RoomFlag);
        // The room controller reads this shared signal without clearing it.
        OracleRuntimeState memory = _entities.RuntimeState;
        memory.SetWramByte(WramAddress.wTmpcfc0,
            (byte)(memory.ReadWramByte(WramAddress.wTmpcfc0) | 1));
        _triggerEvent(group, roomId);
        if (_entities.InteractionSlotAvailable)
            _entities.Spawn<OverworldKeyUseEffect>(new OverworldKeyUseSpawn(center,record,_database.Constants));
        else
        {
            // Native getFreeInteractionSlot leaves HL=$e040 on failure.
            // The unchecked caller changes L to id/subid, then increments
            // $e041 and writes subid/var03 at $e042/$e043. Echo RAM aliases
            // these to audio's channel2/3/4 sweep bytes; no sprite is created.
            _sound.SetNativeChannelPitchSlide(2,unchecked((byte)(_sound.Channel(2).PitchSlide + 1)));
            _sound.SetNativeChannelPitchSlide(3,(byte)record.SubId);
            _sound.SetNativeChannelPitchSlide(4,(byte)record.SubId);
        }
        // nextToOverworldKeyhole sets carry after acquiring $81 control.
        return true;
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
    }
}
