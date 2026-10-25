using System;
using System.Linq;

namespace Oracles;

internal sealed class TokayDimitriDatabase
{
    internal TokayDimitriPlacement[] Placements { get; }
    internal bool Matches(int group,int room)=>Placements[0].Group==group&&Placements[0].Room==room;
    internal TokayDimitriDatabase()
    {
        Placements=GeneratedTable.Load(AgesGameContent.AssetRoot + "objects/tokay_dimitri_room.tsv",
            new GeneratedTableSchema("Crescent Island Dimitri interaction prefix",GeneratedTableKeySemantics.Ordered,
                ["group","room","order","id","subid","y","x","source"],["order"],headerRequired:true)).Rows
            .Select(row=>new TokayDimitriPlacement(row.HexByte(0),row.HexByte(1),row.UnsignedDecimal(2),
                row.HexByte(3),row.HexByte(4),row.HexByte(5),row.HexByte(6),row.RequiredString(7))).ToArray();
        if(Placements.Length!=4||!Placements.Select(p=>(p.Order,p.Id,p.SubId)).SequenceEqual(new[]{(0,0x48,0x0f),(1,0x48,0x10),(2,0x67,3),(3,0x71,7)})||
            Placements.Any(p=>p.Group!=Placements[0].Group||p.Room!=Placements[0].Room))
            throw new InvalidOperationException("Crescent Island Dimitri requires the complete $48/$48/$67/$71 source prefix.");
    }
}

internal sealed record TokayDimitriPlacement(int Group,int Room,int Order,int Id,int SubId,int Y,int X,string Source);
