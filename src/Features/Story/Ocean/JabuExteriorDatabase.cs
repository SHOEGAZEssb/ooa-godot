using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

internal sealed class JabuExteriorDatabase
{
    private readonly Dictionary<string, byte[]> _bytes = new(StringComparer.Ordinal);
    internal DungeonInteractionVisual Bubble { get; }
    internal int Group=>Bytes("placements")[0];
    internal int Room=>Bytes("placements")[1];
    internal byte[] ActorSubIds=>[Bytes("placements")[3],Bytes("placements")[5]];
    internal byte[] Bytes(string key) => _bytes.TryGetValue(key, out var bytes) ? bytes :
        throw new InvalidOperationException($"Jabu exterior source table '{key}' is missing.");
    internal JabuExteriorDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/objects/jabu_tables.tsv",
            new GeneratedTableSchema("Jabu exterior", GeneratedTableKeySemantics.Unique,
                ["kind","key","bytes","source"], ["kind","key"], headerRequired:true));
        foreach (var row in table.Rows)
        {
            if (row.RequiredString(0) != "bytes") throw row.Invalid(0,"native byte table");
            _bytes.Add(row.RequiredString(1),row.RequiredString(2).Split(',').Select(byte.Parse).ToArray());
        }
        for(int i=1;i<=2;i++) if(Bytes($"opening-{i}").Length!=160) throw new InvalidOperationException("Jabu opening must contain 20 by 4 tile/attribute pairs.");
        if(Bytes("bubble-positions").Length!=48) throw new InvalidOperationException("INTERAC $97:$01: expected sixteen bubble positions.");
        if(Bytes("placements").Length!=6 || Bytes("placements")[2]!=InteractionId.Miscellaneous2 || Bytes("placements")[4]!=InteractionId.Miscellaneous2)
            throw new InvalidOperationException("Jabu exterior requires two ordered INTERAC $dc placements.");
        var visuals=GeneratedTable.Load("res://assets/oracle/objects/jabu_bubbles.tsv",
            new GeneratedTableSchema("PART $16",GeneratedTableKeySemantics.Unique,
                ["subid","sprite","tile-base","palette","animation"],["subid"],headerRequired:true));
        if(visuals.Rows.Count!=2) throw new InvalidOperationException("PART $16: expected two bubble animations.");
        var first=visuals.Rows[0];
        Bubble=new("16",[first.RequiredString(1)],first.UnsignedDecimal(2),first.UnsignedDecimal(3),true,
            visuals.Rows.Select(r=>r.RequiredString(4)).ToArray());
    }
}
