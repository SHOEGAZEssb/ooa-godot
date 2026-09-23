using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

internal sealed class PuzzleTrapResetDatabase
{
    private readonly Lookup<int,PuzzleTrapResetRecord> _records = new();
    internal PuzzleTrapResetDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/objects/puzzle_trap_resets.tsv",
            new GeneratedTableSchema("INTERAC $90:$1f trap reset",GeneratedTableKeySemantics.Unique,
                ["group","room","order","interval","delay","offsets","dest-group","dest-room",
                    "source-transition","dest-position","dest-transition","source"],
                ["group","room","order"],headerRequired:true));
        foreach (var row in table.Rows)
        {
            int group = row.Decimal(0,0,7), room = row.HexByte(1);
            byte[] offsets = row.RequiredString(5).Split(',').Select(value => Convert.ToByte(value,16)).ToArray();
            if (offsets.Length != 8) throw row.Invalid(5,"eight ordered packed-position probes");
            var warp = new Warp(group,room,-1,0,row.HexByte(8),row.Decimal(6,0,7),row.HexByte(7),
                row.HexByte(9),0,row.HexByte(10));
            _records.GetOrAdd((group << 8) | room).Add(new(row.UnsignedDecimal(2),
                row.Decimal(3,1,255),row.Decimal(4,1,255),Array.AsReadOnly(offsets),warp,row.RequiredString(11)));
        }
        if (table.Rows.Count != 1) throw new InvalidOperationException("INTERAC $90:$1f requires its sole source placement.");
    }
    internal IReadOnlyList<PuzzleTrapResetRecord> GetRoomRecords(int group,int room) =>
        _records.ValuesOrEmpty((group << 8) | room);
}

internal readonly record struct PuzzleTrapResetRecord(int Order,int Interval,int Delay,
    IReadOnlyList<byte> Offsets,Warp Warp,string Source);
