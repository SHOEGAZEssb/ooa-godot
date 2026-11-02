using System.Collections.Concurrent;

namespace Oracles;

// Common ENEMY$17 mechanics use the selected pack's authoritative collision tables.
internal sealed class GhiniDatabase
{
    private static readonly ConcurrentDictionary<string,Lazy<GhiniDatabase>> Packs = new(StringComparer.Ordinal);
    internal static GhiniDatabase For(string root) => Packs.GetOrAdd(root,
        static key => new Lazy<GhiniDatabase>(() => new(key))).Value;
    internal GhiniBehaviorProfile Behavior { get; }
    internal IReadOnlyList<EnemyBehaviorValue> SpawnState { get; }
    internal IReadOnlyList<EnemyBehaviorValue> SpawnSpeeds { get; }
    internal TargetGhiniBehaviorProfile Target { get; }

    private GhiniDatabase(string root)
    {
        var table=GeneratedTable.Load(OracleAssetPack.CommonRoot+"metadata/ghini_behavior.tsv",
            new("Ghini behavior",GeneratedTableKeySemantics.Grouped,
                ["owner","table","index","value-a","value-b","source"],["owner","table"],headerRequired:true));
        var groups=table.Rows.GroupBy(row=>(row.RequiredString(0),row.RequiredString(1)))
            .ToDictionary(group=>group.Key,group=>group.ToArray());
        EnemyBehaviorValue[] Values(string owner,string name,int count)
        {
            if(!groups.Remove((owner,name),out var rows) || rows.Length!=count)
                throw new InvalidOperationException($"{table.Path}: {owner}/{name} requires {count} ordered native rows.");
            return rows.Select((row,index)=>row.UnsignedDecimal(2)==index && row.Decimal(4)==0
                ? new EnemyBehaviorValue(row.Decimal(3),row.RequiredString(5))
                : throw row.Invalid(2,$"ordered {owner}/{name} index {index} without a secondary operand")).ToArray();
        }
        var values=Values("ghini","state-profile",3);
        Behavior=new(values[0].Value,values[1].Value,values[2].Value,values);
        SpawnState=Values("ghini","spawn-state-profile",8);
        SpawnSpeeds=Values("ghini","spawn-speeds",3);
        var collisions=ObjectCollisionDatabase.For(root);
        string game=OracleAssetPack.GameFor(root)==OracleGame.Ages?"ages":"seasons";
        Target=new(Values("target-ghini","state-profile",12),
            Enumerable.Range(0,32).Select(item=>new EnemyBehaviorValue(collisions.Effect(collisions.EnemyMode(EnemyId.Ghini),item),
                $"data/{game}/objectCollisionTable.s:ENEMY_GHINI/column${item:x2}")).ToArray(),
            Enumerable.Range(0,32).Select(item=>new EnemyBehaviorValue(collisions.EnemyEnabled(EnemyId.Ghini,item)?1:0,
                $"data/{game}/enemyActiveCollisions.s:ENEMY_GHINI/column${item:x2}")).ToArray(),
            Values("target-ghini","seed-status-2c",4),Values("target-ghini","seed-status-38",4));
        if(table.Rows.Count!=34 || groups.Count!=0)
            throw new InvalidOperationException($"{table.Path}: expected34 fully consumed native Ghini rows.");
    }
}

internal readonly record struct GhiniBehaviorProfile(
    int SpeedRaw,
    int MoveCounterBase,
    int MoveCounterMask,
    IReadOnlyList<EnemyBehaviorValue> Sources);
