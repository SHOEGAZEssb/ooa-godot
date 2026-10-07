using System;
using System.Collections.Generic;
using Godot;

namespace oracleofages;

internal sealed class DungeonChestPatternDatabase
{
    private readonly Lookup<(int Id,int SubId),DungeonChestPatternCell> _patterns = new();
    internal DungeonChestPatternDatabase()
    {
        Load("res://assets/oracle/objects/crown_chest_patterns.tsv",0x21,[0x13,0x14,0x15],16);
        Load("res://assets/oracle/objects/mermaid_chest_patterns.tsv",0x90,[1,3],9);
    }
    private void Load(string path,int id,int[] supportedSubIds,int count)
    {
        var table = GeneratedTable.Load(path,
            new GeneratedTableSchema("Dungeon pattern chest conditions",GeneratedTableKeySemantics.Unique,
                ["subid","order","position","minimum-tile","maximum-tile","source"],
                ["subid","order"],headerRequired:true));
        foreach (var row in table.Rows)
        {
            int subid = row.HexByte(0), minimum = row.HexByte(3), maximum = row.HexByte(4);
            if (Array.IndexOf(supportedSubIds,subid) < 0 || maximum < minimum)
                throw row.Invalid(0,$"supported INTERAC ${id:x2} chest pattern and ordered tile range");
            var cells = _patterns.GetOrAdd((id,subid));
            if (row.UnsignedDecimal(1) != cells.Count) throw row.Invalid(1,"source-ordered pattern cells");
            cells.Add(new(row.HexByte(2),minimum,maximum,row.RequiredString(5)));
        }
        if (table.Rows.Count != count) throw new InvalidOperationException($"{path}: requires {count} source pattern cells.");
    }
    internal IReadOnlyList<DungeonChestPatternCell> Cells(int subid) => Cells(0x21,subid);
    internal IReadOnlyList<DungeonChestPatternCell> Cells(int id,int subid) => _patterns.ValuesOrEmpty((id,subid));
    internal bool Matches(int subid,OracleRoomData room) => Matches(0x21,subid,room);
    internal bool Matches(int id,int subid,OracleRoomData room)
    {
        var cells = Cells(id,subid);
        if (cells.Count == 0) throw new InvalidOperationException($"Missing INTERAC ${id:x2}:${subid:x2} chest pattern.");
        foreach (var cell in cells)
        {
            int tile = room.GetMetatile(new Vector2((cell.Position & 15) * 16 + 8,(cell.Position >> 4) * 16 + 8));
            if (tile < cell.MinimumTile || tile > cell.MaximumTile) return false;
        }
        return true;
    }
}

internal readonly record struct DungeonChestPatternCell(int Position,int MinimumTile,int MaximumTile,string Source);
