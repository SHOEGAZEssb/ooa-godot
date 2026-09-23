using System;
using System.Collections.Generic;
using Godot;

namespace oracleofages;

internal sealed class DungeonChestPatternDatabase
{
    private readonly Lookup<int,DungeonChestPatternCell> _patterns = new();
    internal DungeonChestPatternDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/objects/crown_chest_patterns.tsv",
            new GeneratedTableSchema("Dungeon pattern chest conditions",GeneratedTableKeySemantics.Unique,
                ["subid","order","position","minimum-tile","maximum-tile","source"],
                ["subid","order"],headerRequired:true));
        foreach (var row in table.Rows)
        {
            int subid = row.HexByte(0), minimum = row.HexByte(3), maximum = row.HexByte(4);
            if (subid is < 0x13 or > 0x15 || maximum < minimum)
                throw row.Invalid(0,"supported INTERAC $21 chest pattern and ordered tile range");
            var cells = _patterns.GetOrAdd(subid);
            if (row.UnsignedDecimal(1) != cells.Count) throw row.Invalid(1,"source-ordered pattern cells");
            cells.Add(new(row.HexByte(2),minimum,maximum,row.RequiredString(5)));
        }
        if (table.Rows.Count != 16) throw new InvalidOperationException("Crown chest patterns require sixteen source cells.");
    }
    internal IReadOnlyList<DungeonChestPatternCell> Cells(int subid) => _patterns.ValuesOrEmpty(subid);
    internal bool Matches(int subid,OracleRoomData room)
    {
        var cells = Cells(subid);
        if (cells.Count == 0) throw new InvalidOperationException($"Missing INTERAC $21:${subid:x2} chest pattern.");
        foreach (var cell in cells)
        {
            int tile = room.GetMetatile(new Vector2((cell.Position & 15) * 16 + 8,(cell.Position >> 4) * 16 + 8));
            if (tile < cell.MinimumTile || tile > cell.MaximumTile) return false;
        }
        return true;
    }
}

internal readonly record struct DungeonChestPatternCell(int Position,int MinimumTile,int MaximumTile,string Source);
