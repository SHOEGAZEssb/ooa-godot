using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_CREATE_OBJECT_AT_EACH_TILEINDEX $c7:$04. It creates one
/// PART_RESPAWNABLE_BUSH $0f at every source tile $04, in layout order.
/// </summary>
internal sealed partial class RespawnableBushScannerRoomEntity :
    DungeonMechanicRoomEntity, IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly DungeonMechanicDatabaseRecord _record;
    private readonly OracleRoomData _room;

    public bool Finished { get; private set; }
    internal int BushCount { get; private set; }

    internal RespawnableBushScannerRoomEntity(
        DungeonMechanicDatabaseRecord record,
        OracleRoomData room)
        : base(record, $"RespawnableBushScanner_{record.Order}")
    {
        if (record is not { Id: 0xc7, SubId: 0x04, PackedPosition: 0x0f } ||
            (record.Parameter & 0xf0) != 0x10)
        {
            throw new ArgumentOutOfRangeException(nameof(record));
        }
        _record = record;
        _room = room;
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;
        if (_room.Layout.Length != 176)
        {
            throw new InvalidOperationException(
                $"INTERAC_CREATE_OBJECT_AT_EACH_TILEINDEX $c7:$04 in room " +
                $"{_record.Group:x1}:{_record.Room:x2} requires the original " +
                "176-byte large-room layout.");
        }

        int dropSubId = _record.Parameter & 0x0f;
        for (int index = 0; index < _room.Layout.Length; index++)
        {
            if (_room.Layout[index] != _record.SubId)
                continue;
            int packedPosition = (index / 16 << 4) | index % 16;
            spawns.Add(new RespawnableBushSpawn(packedPosition, dropSubId));
            BushCount++;
        }
        Finished = true;
    }
}
