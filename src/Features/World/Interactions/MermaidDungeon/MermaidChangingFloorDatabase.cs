using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class MermaidChangingFloorDatabase
{
    private readonly byte[,,] _patterns = new byte[2,2,65];
    private readonly Dictionary<int,MermaidFloorWorkerProfile> _workers = new();
    internal MermaidChangingFloorDatabase()
    {
        var patterns = GeneratedTable.Load("res://assets/oracle/objects/mermaid_changing_floor_patterns.tsv",
            new GeneratedTableSchema("Mermaid changing floor streams",GeneratedTableKeySemantics.Unique,
                ["state","half","offset","value","source"],["state","half","offset"],headerRequired:true));
        int order = 0;
        foreach (var row in patterns.Rows)
        {
            int state = row.Decimal(0,0,1), half = row.Decimal(1,0,1), offset = row.Decimal(2,0,64);
            if (state*130+half*65+offset != order++) throw row.Invalid(2,"source-ordered 65-byte halves");
            _patterns[state,half,offset] = (byte)row.HexByte(3);
            _ = row.RequiredString(4);
        }
        if (order != 260) throw new InvalidOperationException("Mermaid changing floor requires four 65-byte source streams.");
        var workers = GeneratedTable.Load("res://assets/oracle/objects/mermaid_changing_floor_workers.tsv",
            new GeneratedTableSchema("Mermaid serpentine floor workers",GeneratedTableKeySemantics.Unique,
                ["subid","position","y-step","x-step","buffer-offset","source"],["subid"],headerRequired:true));
        foreach (var row in workers.Rows)
        {
            int subid = row.HexByte(0);
            if (subid is not (5 or 6)) throw row.Invalid(0,"INTERAC$90:$05/$06");
            _workers.Add(subid,new((byte)row.HexByte(1),(byte)row.HexByte(2),(byte)row.HexByte(3),(byte)row.HexByte(4),row.RequiredString(5)));
        }
        if (_workers.Count != 2) throw new InvalidOperationException("Mermaid changing floor requires both source worker profiles.");
    }
    internal MermaidFloorWorkerProfile Worker(int subid) => _workers.TryGetValue(subid,out var profile) ? profile :
        throw new InvalidOperationException($"Missing INTERAC$90:${subid:x2} floor worker profile.");
    internal void LoadPattern(OracleRuntimeState runtime,int state)
    {
        for (int half = 0; half < 2; half++)
        for (int offset = 0; offset < 65; offset++)
            runtime.SetWramByte(WramAddress.wBigBuffer+half*0x80+offset,_patterns[state,half,offset]);
    }
}

internal readonly record struct MermaidFloorWorkerProfile(byte Position,byte YStep,byte XStep,byte BufferOffset,string Source);
