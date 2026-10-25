using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

internal sealed class CreditsVerticalDatabase
{
    private readonly List<int> _waits=[];
    private readonly Dictionary<int,IntroOamPart[]> _lines=[];
    internal CreditsVerticalDatabase()
    {
        foreach(var row in GeneratedTable.Load(EndingScreenDatabase.Root+"credits_vertical.tsv",new("INTERAC $af vertical credits",GeneratedTableKeySemantics.Unique,
            ["kind","index","data","source"],["kind","index"],headerRequired:true)).Rows)
        {
            int index=row.Decimal(1);
            switch(row.RequiredString(0))
            {
                case "wait":
                    if(index!=_waits.Count)throw row.Invalid(1,"contiguous source waits");
                    _waits.Add(row.Decimal(2,0,65535));break;
                case "oam":
                    _lines.Add(index,row.SplitRequired(2,';').Select(part=>
                    {
                        var values=Array.ConvertAll(part.Split(','),value=>Convert.ToInt32(value,16));
                        if(values.Length!=4)throw row.Invalid(2,"four-byte source OAM records");
                        return new IntroOamPart(values[0],values[1],values[2],values[3]);
                    }).ToArray());break;
                default:throw row.Invalid(0,"wait or oam");
            }
        }
        if(_waits.Count!=18||(_waits[^1]&255)!=255||_lines.Count!=17)
            throw new InvalidOperationException("creditsTextVertical.s / data_4556.s: incomplete clean-US waits, sentinel or text rows.");
    }
    internal int Wait(int index)=>_waits[index];
    internal IntroOamPart[] Line(int index)=>_lines.TryGetValue(index,out var line)?line:throw new NotSupportedException($"data_4556.s: unsupported credits row ${index:x2}.");
}
