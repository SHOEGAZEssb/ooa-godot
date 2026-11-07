using System.Collections.Concurrent;

namespace Oracles;

internal sealed class BoomerangMoblinDatabase
{
    private static readonly ConcurrentDictionary<string,Lazy<BoomerangMoblinDatabase>> Packs=new(StringComparer.Ordinal);
    internal static BoomerangMoblinDatabase For(string root)=>Packs.GetOrAdd(root,
        static key=>new Lazy<BoomerangMoblinDatabase>(()=>new(key))).Value;
    internal int SpeedRaw {get;}
    internal IReadOnlyList<EnemyBehaviorValue> RouteCounters {get;}
    internal MoblinBoomerangBehaviorProfile Projectile {get;}
    internal EnemyProjectileVisualRecord Visual {get;}

    private BoomerangMoblinDatabase(string root)
    {
        var table=GeneratedTable.Load(root+"metadata/boomerang_moblin.tsv",
            new("ENEMY$0a/PART$21 operands",GeneratedTableKeySemantics.Unique,["index","value","source"],["index"],headerRequired:true));
        if(table.Rows.Count!=14)throw new InvalidOperationException($"{table.Path}: expected14 Moblin/boomerang operands.");
        var values=table.Rows.Select((row,index)=>row.UnsignedDecimal(0)==index
            ?new EnemyBehaviorValue(row.Decimal(1),row.RequiredString(2)):throw row.Invalid(0,$"ordered Moblin operand{index}")).ToArray();
        SpeedRaw=values[0].Value;RouteCounters=values[1..5];var part=values[5..];
        Projectile=new(part[0].Value,part[1].Value,part[2].Value,part[3].Value,part[4].Value,
            part[5].Value,part[6].Value,part[7].Value,part[8].Value,part);
        var visual=GeneratedTable.Load(root+"effects/moblin_boomerang.tsv",
            new("PART$21 graphics",GeneratedTableKeySemantics.Ordered,
                ["sprites","tile-base","palette","source-grayscale-inverted","animations-base64"],headerRequired:true)).SingleRow();
        Visual=new(root,visual.SplitRequired(0,','),visual.UnsignedDecimal(1),visual.UnsignedDecimal(2),
            visual.Boolean01(3),visual.EncodedAnimations(4));
        if(Visual.Animations.Length!=1)throw new InvalidOperationException($"{root}: PART$21 requires one native animation.");
    }
}
