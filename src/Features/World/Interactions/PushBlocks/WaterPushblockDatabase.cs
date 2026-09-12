using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class WaterPushblockDatabase
{
    private readonly Lookup<int, WaterPushblockRecord> _placements = new();
    private readonly List<WaterPushblockRoomFlag> _roomFlags = [];
    private readonly Dictionary<string, int> _sounds = new(StringComparer.Ordinal);

    internal IReadOnlyList<WaterPushblockRoomFlag> RoomFlags => _roomFlags;
    internal IReadOnlyList<WaterPushblockRecord> GetRoom(int group, int room) =>
        _placements.ValuesOrEmpty(group * 0x100 + room);
    internal int Sound(string name) => _sounds.TryGetValue(name, out int id) ? id :
        throw new InvalidOperationException($"waterPushblock.s: missing sound {name}.");

    internal WaterPushblockDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/objects/water_pushblocks.tsv",
            new GeneratedTableSchema("water pushblocks", GeneratedTableKeySemantics.Grouped,
                ["group", "room", "order", "subid", "y", "x", "sprite", "tile-base", "palette", "animation", "source"],
                ["group", "room"], headerRequired: true));
        foreach (var row in table.Rows)
        {
            var record = new WaterPushblockRecord(row.Decimal(0, 0, 1), row.HexByte(1),
                row.UnsignedDecimal(2), row.HexByte(3), row.HexByte(4), row.HexByte(5),
                row.RequiredString(6), row.UnsignedDecimal(7), row.Decimal(8, 0, 7),
                row.RequiredString(9), row.RequiredString(10));
            if (record.SubId > 1) throw row.Invalid(3, "INTERAC_WATER_PUSHBLOCK $9e subid $00/$01");
            var records = _placements.GetOrAdd(record.Group * 0x100 + record.Room);
            if (records.Count > 0 && records[^1].Order >= record.Order)
                throw row.Invalid(2, "increasing source object order");
            records.Add(record);
        }
        table = GeneratedTable.Load("res://assets/oracle/objects/water_pushblock_rooms.tsv",
            new GeneratedTableSchema("water pushblock layout flags", GeneratedTableKeySemantics.Ordered,
                ["order", "group", "room", "xor-mask", "source"], headerRequired: true));
        foreach (var row in table.Rows)
        {
            if (row.UnsignedDecimal(0) != _roomFlags.Count) throw row.Invalid(0, "consecutive XOR order");
            _roomFlags.Add(new(row.Decimal(1, 0, 1), row.HexByte(2), (byte)row.HexByte(3), row.RequiredString(4)));
        }
        table = GeneratedTable.Load("res://assets/oracle/objects/water_pushblock_sounds.tsv",
            new GeneratedTableSchema("water pushblock sounds", GeneratedTableKeySemantics.Unique,
                ["name", "id", "source"], ["name"], headerRequired: true));
        foreach (var row in table.Rows) _sounds.Add(row.RequiredString(0), row.HexByte(1));
        if (GetRoom(1, 0x41).Count != 2 || _roomFlags.Count != 12 || _sounds.Count != 5)
            throw new InvalidOperationException("waterPushblock.s: incomplete $9e placements, layout flags or sounds.");
    }
}

internal readonly record struct WaterPushblockRecord(int Group, int Room, int Order, int SubId,
    int Y, int X, string Sprite, int TileBase, int Palette, string Animation, string Source);
internal readonly record struct WaterPushblockRoomFlag(int Group, int Room, byte Mask, string Source);
