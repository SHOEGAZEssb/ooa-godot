using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Imported small-key door tile behavior from interactableTilesTable and the
/// matching standard room-flag substitutions. Directional flags are retained
/// for both sides of a dungeon-layout adjacency.
/// </summary>
internal sealed class DungeonKeyDoorDatabase
{
    internal readonly record struct Record(
        byte ClosedTile,
        Vector2I Direction,
        byte OpenTile,
        byte RoomFlag,
        byte OppositeRoomFlag,
        int PushCounter,
        int DoorFrameWait,
        int DoorSound,
        int KeySound,
        int NoKeyTextId,
        string NoKeyMessage);

    private readonly Dictionary<byte, Record> _records = new();

    internal int Count => _records.Count;

    internal DungeonKeyDoorDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/dungeon_key_doors.tsv",
            new GeneratedTableSchema(
                "dungeon small-key doors",
                GeneratedTableKeySemantics.Unique,
                [
                    "closed-tile", "direction", "open-tile", "room-flag",
                    "opposite-room-flag", "push-counter", "door-frame-wait",
                    "door-sound", "key-sound", "no-key-text-id", "no-key-utf8-base64"
                ],
                ["closed-tile"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            var record = new Record(
                (byte)row.HexByte(0),
                row.RequiredString(1) switch
                {
                    "up" => Vector2I.Up,
                    "right" => Vector2I.Right,
                    "down" => Vector2I.Down,
                    "left" => Vector2I.Left,
                    _ => throw row.Invalid(1, "one of up, right, down, left")
                },
                (byte)row.HexByte(2),
                (byte)row.HexByte(3),
                (byte)row.HexByte(4),
                row.UnsignedDecimal(5),
                row.UnsignedDecimal(6),
                row.UnsignedDecimal(7),
                row.UnsignedDecimal(8),
                row.HexWord(9),
                row.Base64Utf8(10));
            if (!_records.TryAdd(record.ClosedTile, record))
                throw new InvalidOperationException(
                    $"Duplicate small-key door tile ${record.ClosedTile:x2}.");
        }

        if (_records.Count != 4 ||
            !_records.TryGetValue(0x70, out Record up) ||
            !_records.TryGetValue(0x71, out Record right) ||
            !_records.TryGetValue(0x72, out Record down) ||
            !_records.TryGetValue(0x73, out Record left) ||
            up.Direction != Vector2I.Up || up.RoomFlag != 0x01 ||
            right.Direction != Vector2I.Right || right.RoomFlag != 0x02 ||
            down.Direction != Vector2I.Down || down.RoomFlag != 0x04 ||
            left.Direction != Vector2I.Left || left.RoomFlag != 0x08 ||
            left.OppositeRoomFlag != 0x02 ||
            left.OpenTile != 0xa0 || left.PushCounter != 20 ||
            left.DoorFrameWait != 6 || left.DoorSound != 0x70 ||
            left.KeySound != 0x5e || left.NoKeyTextId != 0x5100 ||
            string.IsNullOrWhiteSpace(left.NoKeyMessage))
        {
            throw new InvalidOperationException(
                "Imported small-key door $70-$73 contract is incomplete.");
        }
    }

    internal bool TryGet(byte tile, out Record record) =>
        _records.TryGetValue(tile, out record);

    internal void ApplyOpenedDoorState(
        OracleRoomData room,
        byte roomFlags,
        long animationTick)
    {
        var substitutions = new Dictionary<byte, byte>();
        foreach (Record record in _records.Values)
        {
            if ((roomFlags & record.RoomFlag) != 0)
                substitutions.Add(record.ClosedTile, record.OpenTile);
        }
        room.ApplyMetatileSubstitutions(substitutions, animationTick);
    }

}
