using System;
using System.Linq;

namespace Oracles;

internal sealed class DimitriDepartureDatabase
{
    internal DimitriDeparturePlacement[] Placements {get;}=GeneratedTable.Load(AgesGameContent.AssetRoot+"objects/dimitri_departure_placements.tsv",
        new("Dimitri departure placements",GeneratedTableKeySemantics.Unique,["group","room","order","source"],["group","room"],headerRequired:true))
        .Rows.Select(row=>new DimitriDeparturePlacement(row.Decimal(0,0,7),row.HexByte(1),row.UnsignedDecimal(2),row.RequiredString(3))).ToArray();
    private readonly int[][] _facing=GeneratedTable.Load(AgesGameContent.AssetRoot+"objects/dimitri_departure_facing.tsv",
        new("hidden $71 facing animation",GeneratedTableKeySemantics.Ordered,["direction","counter","parameter","pointer","oam-address","source"],headerRequired:true))
        .Rows.Select((row,index)=>
        {
            if(row.UnsignedDecimal(0)!=index)throw row.Invalid(0,"native facing order");
            _=row.RequiredString(5);
            return new[]{row.HexByte(1),row.HexByte(2),row.HexWord(3),row.HexWord(4)};
        }).ToArray();
    internal bool Matches(int group,int room)=>Placements.Any(p=>p.Group==group && p.Room==room);
    internal int[] Facing(int direction)=>_facing[direction];
    internal DimitriDepartureDatabase()
    {
        if(Placements.Length!=3 || _facing.Length!=4)throw new InvalidOperationException("$71:$06 requires three mainland placements and four native facing entries.");
    }
}

internal sealed record DimitriDeparturePlacement(int Group,int Room,int Order,string Source);
