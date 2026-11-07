using System.Collections.Concurrent;

namespace Oracles;

internal sealed class StalfosDatabase
{
    private static readonly ConcurrentDictionary<string,Lazy<StalfosDatabase>> Packs=new(StringComparer.Ordinal);
    internal static StalfosDatabase For(string root)=>Packs.GetOrAdd(root,
        static key=>new Lazy<StalfosDatabase>(()=>new(key))).Value;
    private readonly Dictionary<int,StalfosRecord> _records=[];
    internal StalfosBehaviorProfile Behavior {get;}
    internal StalfosRecord Get(int subid)=>_records.TryGetValue(subid,out var record)?record:
        throw new NotSupportedException($"stalfos.s: subid${subid:x2} has no selected-pack definition.");

    private StalfosDatabase(string root)
    {
        var table=GeneratedTable.Load(root+"metadata/stalfos_behavior.tsv",
            new("Stalfos operands",GeneratedTableKeySemantics.Unique,["index","value","source"],["index"],headerRequired:true));
        if(table.Rows.Count!=7)throw new InvalidOperationException($"{table.Path}: expected seven native Stalfos operands.");
        var values=table.Rows.Select((row,index)=>row.UnsignedDecimal(0)==index
            ?new EnemyBehaviorValue(row.Decimal(1),row.RequiredString(2)):throw row.Invalid(0,$"Stalfos operand {index}")).ToArray();
        Behavior=new(values[0].Value,values[1].Value,values[2].Value,values[3].Value,values[4].Value,values[5].Value,values[6].Value,values);
        table=GeneratedTable.Load(root+"objects/stalfos.tsv",new("Stalfos definitions",GeneratedTableKeySemantics.Unique,
            ["id","subid","sprite","tile-base","palette","radius-y","radius-x","damage-quarters","health","speed-raw","walk-animation","jump-animation"],
            ["id","subid"],headerRequired:true));
        foreach(var row in table.Rows)
        {
            var record=new StalfosRecord(root,row.HexByte(0),row.HexByte(1),row.RequiredString(2),row.UnsignedDecimal(3),
                row.UnsignedDecimal(4),row.UnsignedDecimal(5),row.UnsignedDecimal(6),row.UnsignedDecimal(7),
                row.UnsignedDecimal(8),row.UnsignedDecimal(9),row.RequiredString(10),row.RequiredString(11));
            if(record.Id!=EnemyId.Stalfos || record.SubId is not (0 or 2))throw row.Invalid(0,"ENEMY$31:$00/$02");
            _records.Add(record.SubId,record);
        }
        if(_records.Count==0)throw new InvalidOperationException($"{table.Path}: missing native Stalfos definitions.");
    }
}

public readonly record struct StalfosRecord(string AssetRoot, int Id, int SubId, string SpriteName, int TileBase, int Palette, int CollisionRadiusY, int CollisionRadiusX, int DamageQuarters, int Health, int SpeedRaw, string WalkAnimation, string JumpAnimation);
