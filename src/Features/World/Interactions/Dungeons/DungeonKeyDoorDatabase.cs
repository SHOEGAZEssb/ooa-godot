using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Imported small-key and boss-key door behavior from interactableTilesTable and the
/// matching standard room-flag substitutions. Directional flags are retained
/// for both sides of a dungeon-layout adjacency.
/// </summary>
internal sealed class DungeonKeyDoorDatabase
{

    private readonly Dictionary<byte, DungeonKeyDoorDatabaseRecord> _records = new();

    internal int Count => _records.Count;

    internal DungeonKeyDoorDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/dungeon_key_doors.tsv",
            new GeneratedTableSchema(
                "dungeon key doors",
                GeneratedTableKeySemantics.Unique,
                [
                    "closed-tile", "direction", "key-kind", "key-graphic",
                    "open-tile", "room-flag",
                    "opposite-room-flag", "push-counter", "door-frame-wait",
                    "door-sound", "key-sound", "no-key-text-id", "no-key-utf8-base64"
                ],
                ["closed-tile"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            DungeonKeyDoorDatabaseRecord record = new DungeonKeyDoorDatabaseRecord(
                (byte)row.HexByte(0),
                row.RequiredString(1) switch
                {
                    "up" => Vector2I.Up,
                    "right" => Vector2I.Right,
                    "down" => Vector2I.Down,
                    "left" => Vector2I.Left,
                    _ => throw row.Invalid(1, "one of up, right, down, left")
                },
                row.RequiredString(2) switch
                {
                    "small" => false,
                    "boss" => true,
                    _ => throw row.Invalid(2, "small or boss")
                },
                row.HexByte(3),
                (byte)row.HexByte(4),
                (byte)row.HexByte(5),
                (byte)row.HexByte(6),
                row.UnsignedDecimal(7),
                row.UnsignedDecimal(8),
                row.UnsignedDecimal(9),
                row.UnsignedDecimal(10),
                row.HexWord(11),
                row.Base64Utf8(12));
            if (!_records.TryAdd(record.ClosedTile, record))
                throw new InvalidOperationException(
                    $"Duplicate dungeon-key door tile ${record.ClosedTile:x2}.");
        }

        if (_records.Count != 8 ||
            !_records.TryGetValue(0x70, out DungeonKeyDoorDatabaseRecord up) ||
            !_records.TryGetValue(0x71, out DungeonKeyDoorDatabaseRecord right) ||
            !_records.TryGetValue(0x72, out DungeonKeyDoorDatabaseRecord down) ||
            !_records.TryGetValue(0x73, out DungeonKeyDoorDatabaseRecord left) ||
            !_records.TryGetValue(0x74, out DungeonKeyDoorDatabaseRecord bossUp) ||
            !_records.TryGetValue(0x75, out DungeonKeyDoorDatabaseRecord bossRight) ||
            !_records.TryGetValue(0x76, out DungeonKeyDoorDatabaseRecord bossDown) ||
            !_records.TryGetValue(0x77, out DungeonKeyDoorDatabaseRecord bossLeft) ||
            up.Direction != Vector2I.Up || up.RoomFlag != 0x01 ||
            right.Direction != Vector2I.Right || right.RoomFlag != 0x02 ||
            down.Direction != Vector2I.Down || down.RoomFlag != 0x04 ||
            left.Direction != Vector2I.Left || left.RoomFlag != 0x08 ||
            left.OppositeRoomFlag != 0x02 ||
            left.OpenTile != 0xa0 || left.PushCounter != 20 ||
            left.DoorFrameWait != 6 || left.DoorSound != 0x70 ||
            left.UsesBossKey || left.KeyGraphic != 0x42 ||
            left.KeySound != 0x5e || left.NoKeyTextId != 0x5100 ||
            !bossUp.UsesBossKey || bossUp.Direction != Vector2I.Up ||
            !bossRight.UsesBossKey || bossRight.Direction != Vector2I.Right ||
            !bossDown.UsesBossKey || bossDown.Direction != Vector2I.Down ||
            !bossLeft.UsesBossKey || bossLeft.Direction != Vector2I.Left ||
            bossRight.KeyGraphic != 0x43 || bossRight.NoKeyTextId != 0x5101 ||
            string.IsNullOrWhiteSpace(left.NoKeyMessage))
        {
            throw new InvalidOperationException(
                "Imported dungeon-key door $70-$77 contract is incomplete.");
        }
    }

    internal bool TryGet(byte tile, out DungeonKeyDoorDatabaseRecord record) =>
        _records.TryGetValue(tile, out record);

    internal void ApplyOpenedDoorState(
        OracleRoomData room,
        byte roomFlags,
        long animationTick)
    {
        var substitutions = new Dictionary<byte, byte>();
        foreach (DungeonKeyDoorDatabaseRecord record in _records.Values)
        {
            if ((roomFlags & record.RoomFlag) != 0)
                substitutions.Add(record.ClosedTile, record.OpenTile);
        }
        room.ApplyMetatileSubstitutions(substitutions, animationTick);
    }

}
