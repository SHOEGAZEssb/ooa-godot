using System.Collections.Concurrent;

namespace Oracles;

internal static class FloormasterDatabase
{
    private static readonly ConcurrentDictionary<string,Lazy<FloormasterBehaviorProfile>> Packs=new(StringComparer.Ordinal);
    internal static FloormasterBehaviorProfile For(string root)=>Packs.GetOrAdd(root,
        static key=>new Lazy<FloormasterBehaviorProfile>(()=>Load(key))).Value;
    private static FloormasterBehaviorProfile Load(string root)
    {
        var table=GeneratedTable.Load(root+"metadata/floormaster.tsv",
            new("ENEMY$35 native operands",GeneratedTableKeySemantics.Unique,
                ["profile","index","value","source"],["profile","index"],headerRequired:true));
        var groups=table.Rows.GroupBy(row=>row.RequiredString(0)).ToDictionary(group=>group.Key,group=>group.ToArray());
        EnemyBehaviorValue[] Values(string name,int count)
        {
            if(!groups.Remove(name,out var rows)||rows.Length!=count)
                throw new InvalidOperationException($"{table.Path}: expected {count} Floormaster {name} rows.");
            return rows.Select((row,index)=>row.UnsignedDecimal(1)==index?new EnemyBehaviorValue(row.Decimal(2),row.RequiredString(3)):
                throw row.Invalid(1,$"ordered {name} index {index}")).ToArray();
        }
        var profile=new FloormasterBehaviorProfile(Values("state-profile",16),Values("hover-z",8),Values("chase-z",8),
            Values("collision-effects",32),Values("active-collisions",32),Values("link-return",6));
        if(groups.Count!=0||table.Rows.Count!=102)throw new InvalidOperationException($"{table.Path}: expected102 native Floormaster operands.");
        return profile;
    }
}
