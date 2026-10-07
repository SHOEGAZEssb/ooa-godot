using System;
using System.Collections.Generic;

namespace oracleofages;

// commonTileSubstitutions.s:replaceSwitchTiles precedes object parsing.
internal sealed class SwitchTileReplacementDatabase
{
    private readonly Lookup<(int Group,int Room),Replacement> _byRoom = new();
    internal int RecordCount { get; }

    internal SwitchTileReplacementDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/metadata/switch_tile_replacements.tsv",
            new GeneratedTableSchema("switch tile replacements",GeneratedTableKeySemantics.Grouped,
                ["group","room","mask","position","tile","source"],["group","room"],headerRequired:true));
        foreach (var row in table.Rows)
        {
            int group = row.Decimal(0,4,5), room = row.HexByte(1);
            byte mask = (byte)row.HexByte(2);
            string source = row.RequiredString(5);
            if (room == 0 || mask == 0) throw row.Invalid(1,$"{source}: nonterminal room and nonzero switch mask");
            _byRoom.Add((group,room),new(mask,row.HexByte(3),(byte)row.HexByte(4),source));
            RecordCount++;
        }
        if (RecordCount != 12) throw new InvalidOperationException("commonTileSubstitutions.s: expected12 Ages switch replacements.");
    }

    internal void Apply(int group,OracleRoomData room,byte switchState,long tick)
    {
        var records = _byRoom.ValuesOrEmpty((group,room.Id));
        if (records.Count == 0 || switchState == 0) return;
        var writes = new Dictionary<int,byte>();
        foreach (var row in records)
        {
            if ((switchState&row.Mask) == 0) continue;
            if ((row.Position&15) >= room.WidthInTiles || (row.Position>>4) >= room.HeightInTiles)
                throw new InvalidOperationException($"{row.Source}: invalid position${row.Position:x2} in room${group:x1}:${room.Id:x2}.");
            writes[row.Position] = row.Tile;
        }
        if (writes.Count != 0) room.ApplyRoomInitializationChanges(writes,tick);
    }

    private readonly record struct Replacement(byte Mask,int Position,byte Tile,string Source);
}
