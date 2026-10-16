using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class MapleEventDatabase
{
    internal IReadOnlyList<(int Speed,int Frames,int Y,int Animation)> CreditsFlight {get;private set;}=[];
    internal int[] CreditsZOffsets {get;private set;}=[];
    private void LoadCredits()
    {
        var flight=new List<(int,int,int,int)>();
        foreach(var row in GeneratedTable.Load("res://assets/oracle/objects/maple_credits_flight.tsv",new("Maple credits flight",GeneratedTableKeySemantics.Ordered,
            ["index","speed","frames","y","animation","source"],headerRequired:true)).Rows)
        {
            if(row.Decimal(0)!=flight.Count)throw row.Invalid(0,"original flight order");
            flight.Add((row.Decimal(1,0,255),row.Decimal(2,1,255),row.HexByte(3),row.HexByte(4)));
        }
        if(flight.Count!=4)throw new InvalidOperationException("companionCutscene.s: Maple requires four credits flight rows.");
        CreditsFlight=flight;
        var offsets=GeneratedTable.Load("res://assets/oracle/objects/maple_credits_z.tsv",new("objectOscillateZ",GeneratedTableKeySemantics.Ordered,
            ["offsets","source"],headerRequired:true)).SingleRow();
        CreditsZOffsets=Array.ConvertAll(offsets.SplitRequired(0,','),value=>(int)unchecked((sbyte)Convert.ToByte(value,16)));
        if(CreditsZOffsets.Length!=8)throw offsets.Invalid(0,"eight native vertical oscillation offsets");
    }
}
