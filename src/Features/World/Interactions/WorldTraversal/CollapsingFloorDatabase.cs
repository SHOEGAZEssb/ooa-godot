using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

internal sealed class CollapsingFloorDatabase
{
    private readonly List<CollapsingFloorRecord> _records = new();
    internal CollapsingFloorDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/objects/collapsing_floor.tsv",
            new GeneratedTableSchema("$dc:$0b collapsing floor", GeneratedTableKeySemantics.Grouped,
                ["group", "room", "order", "y", "x", "radius", "wait", "interval", "tile", "clink", "rumble", "tiles", "dest-group", "dest-room", "dest-transition", "dest-position"],
                ["group", "room"], headerRequired: true));
        foreach (var row in table.Rows)
        {
            int[] tiles = row.RequiredString(11).Split(',').Select(s => Convert.ToInt32(s, 16)).ToArray();
            if (tiles.Length < 2 || tiles[^1] != 0 || tiles[..^1].Any(t => t <= 0 || t > 0xff))
                throw row.Invalid(11, "a terminated $dc:$0b tile mini-script");
            _records.Add(new(row.Decimal(0, 0, 7), row.HexByte(1), row.UnsignedDecimal(2),
                row.HexByte(3), row.HexByte(4), row.HexByte(5), row.Decimal(6, 1, 255),
                row.Decimal(7, 1, 255), row.HexByte(8), row.Decimal(9, 0, 255), row.Decimal(10, 0, 255), tiles,
                row.Decimal(12, 0, 7), row.HexByte(13), row.HexByte(14), row.HexByte(15)));
        }
    }
    internal IEnumerable<CollapsingFloorRecord> InRoom(int group, int room) =>
        _records.Where(r => r.Group == group && r.Room == room);

    internal bool TryGetHoleWarp(int group, int room, int position, out Warp warp)
    {
        var record = InRoom(group, room).SingleOrDefault();
        warp = record is null ? default : new Warp(group, room, position, 0, 2,
            record.DestinationGroup, record.DestinationRoom, record.DestinationPosition, 0,
            record.DestinationTransition, DirectFadeOut: true);
        return record is not null;
    }
}

internal sealed record CollapsingFloorRecord(int Group, int Room, int Order, int Y, int X,
    int Radius, int Wait, int Interval, int Tile, int Clink, int Rumble, int[] Tiles,
    int DestinationGroup, int DestinationRoom, int DestinationTransition, int DestinationPosition);
