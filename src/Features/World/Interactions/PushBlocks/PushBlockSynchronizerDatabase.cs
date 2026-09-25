using System;
using System.Collections.Generic;

namespace oracleofages;

// Source placements and search operands for the native $bd dispatcher.
internal sealed class PushBlockSynchronizerDatabase
{
    private readonly Lookup<int, PushBlockSynchronizerRecord> _records = new();
    internal byte ScanStart { get; }
    internal byte ExcludedTile { get; }
    internal IReadOnlyList<byte> DestinationOffsets { get; }

    internal PushBlockSynchronizerDatabase()
    {
        var placements = GeneratedTable.Load("res://assets/oracle/objects/pushblock_synchronizers.tsv",
            new GeneratedTableSchema("INTERAC $bd placements", GeneratedTableKeySemantics.Unique,
                ["group","room","order","subid","source"], ["group","room","order"], headerRequired:true));
        foreach (var row in placements.Rows)
        {
            int group = row.Decimal(0,0,7), room = row.HexByte(1);
            if (row.HexByte(3) != 0) throw row.Invalid(3,"INTERAC $bd subid $00");
            _records.GetOrAdd((group << 8) | room).Add(new(row.UnsignedDecimal(2),row.RequiredString(4)));
        }
        if (placements.Rows.Count != 2) throw new InvalidOperationException("INTERAC $bd requires both source placements.");
        var rules = GeneratedTable.Load("res://assets/oracle/objects/pushblock_synchronizer_rules.tsv",
            new GeneratedTableSchema("INTERAC $bd search rules", GeneratedTableKeySemantics.Unique,
                ["scan-start","excluded-tile","up-offset","right-offset","down-offset","left-offset","source"],
                ["scan-start"], headerRequired:true));
        GeneratedTableRow rule = rules.SingleRow();
        ScanStart = (byte)rule.HexByte(0);
        ExcludedTile = (byte)rule.HexByte(1);
        DestinationOffsets = Array.AsReadOnly(new byte[] {
            (byte)rule.HexByte(2),(byte)rule.HexByte(3),(byte)rule.HexByte(4),(byte)rule.HexByte(5) });
        _ = rule.RequiredString(6);
    }

    internal IReadOnlyList<PushBlockSynchronizerRecord> GetRoomRecords(int group,int room) =>
        _records.ValuesOrEmpty((group << 8) | room);
}

internal readonly record struct PushBlockSynchronizerRecord(int Order,string Source);
