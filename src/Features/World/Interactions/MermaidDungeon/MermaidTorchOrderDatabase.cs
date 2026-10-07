using System;
using System.Linq;

namespace oracleofages;

internal sealed class MermaidTorchOrderDatabase
{
    internal MermaidTorchStep[] Steps { get; }
    internal MermaidTorchOrderDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/objects/mermaid_torch_order.tsv",
            new GeneratedTableSchema("Mermaid torch order",GeneratedTableKeySemantics.Unique,
                ["order","position","expected-mask","reset-position","source"],["order"],headerRequired:true));
        Steps = table.Rows.Select((row,index) =>
        {
            if (row.UnsignedDecimal(0) != index) throw row.Invalid(0,"consecutive torch steps");
            return new MermaidTorchStep((byte)row.HexByte(1),(byte)row.HexByte(2),(byte)row.HexByte(3),row.RequiredString(4));
        }).ToArray();
        if (Steps.Length != 4) throw new InvalidOperationException("INTERAC$90:$07 requires four torch steps.");
    }
}

internal readonly record struct MermaidTorchStep(byte Position,byte ExpectedMask,byte ResetPosition,string Source);
