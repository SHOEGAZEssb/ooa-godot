using System.Collections.Concurrent;

namespace Oracles;

internal sealed class BladeTrapDatabase
{
    private static readonly ConcurrentDictionary<string,Lazy<BladeTrapDatabase>> Packs=new(StringComparer.Ordinal);
    internal static BladeTrapDatabase For(string root)=>Packs.GetOrAdd(root,
        static key=>new Lazy<BladeTrapDatabase>(()=>new(key))).Value;
    internal IReadOnlyList<EnemyBehaviorValue> Blue {get;}
    internal IReadOnlyList<EnemyBehaviorValue> Red {get;}
    internal IReadOnlyList<EnemyBehaviorValue> Green {get;}
    internal int GoldSpeed {get;}
    internal ArmoredSwordAttackerKnockbackProfile ArmoredKnockback {get;}

    private BladeTrapDatabase(string root)
    {
        var table=GeneratedTable.Load(root+"metadata/blade_trap.tsv",
            new("ENEMY$0e operands",GeneratedTableKeySemantics.Unique,["profile","index","value","source"],["profile","index"],headerRequired:true));
        var profiles=new Dictionary<string,List<EnemyBehaviorValue>>();
        foreach(var row in table.Rows)
        {
            string profile=row.RequiredString(0);
            if(!profiles.TryGetValue(profile,out var values))profiles.Add(profile,values=[]);
            if(row.UnsignedDecimal(1)!=values.Count)throw row.Invalid(1,$"ordered {profile} operand{values.Count}");
            values.Add(new(row.Decimal(2),row.RequiredString(3)));
        }
        IReadOnlyList<EnemyBehaviorValue> Profile(string name,int count)=>profiles.TryGetValue(name,out var values)&&values.Count==count?values:
            throw new InvalidOperationException($"{table.Path}: expected{count} ENEMY$0e {name} operands.");
        if(profiles.Count!=5)throw new InvalidOperationException($"{table.Path}: expected five trap profiles.");
        Blue=Profile("blue",8);Red=Profile("red",3);Green=Profile("green",4);GoldSpeed=Profile("gold",1)[0].Value;
        var armor=Profile("armor",3);ArmoredKnockback=new(armor[0].Value,armor[1].Value,armor[2].Value,armor);
    }
}
