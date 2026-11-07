using System.Collections.Concurrent;

namespace Oracles;

internal sealed class AquamentusDatabase
{
    private static readonly ConcurrentDictionary<string,Lazy<AquamentusDatabase>> Packs=new(StringComparer.Ordinal);
    internal static AquamentusDatabase For(string root)=>Packs.GetOrAdd(root,static key=>new Lazy<AquamentusDatabase>(()=>new(key))).Value;
    internal int[] FallingSpeeds {get;}
    internal int[] FireDelays {get;}
    internal int[] ChargeProbabilities {get;}
    internal int[] CollisionBoxes {get;}
    internal int[] BodyAnimations {get;}
    internal int[] HornAnimations {get;}
    internal int[] HornOffsets {get;}
    internal int[] AttackerRecoil {get;}
    internal IReadOnlyDictionary<int,Color[]> Palette {get;}
    internal EnemyProjectileVisualRecord Projectile {get;}
    private AquamentusDatabase(string root)
    {
        var table=GeneratedTable.Load(root+"metadata/aquamentus.tsv",
            new("ENEMY$78 native tables",GeneratedTableKeySemantics.Unique,["profile","index","value","source"],["profile","index"],headerRequired:true));
        var groups=table.Rows.GroupBy(row=>row.RequiredString(0)).ToDictionary(group=>group.Key,group=>group.ToArray());
        int[] Values(string name,int count)
        {
            if(!groups.Remove(name,out var rows)||rows.Length!=count)throw new InvalidOperationException($"{table.Path}: expected {count} Aquamentus {name} rows.");
            return rows.Select((row,index)=>row.UnsignedDecimal(1)==index?row.UnsignedDecimal(2):throw row.Invalid(1,$"ordered {name} index{index}")).ToArray();
        }
        FallingSpeeds=Values("falling-speeds",16);FireDelays=Values("fire-delays",5);ChargeProbabilities=Values("charge-probabilities",4);
        CollisionBoxes=Values("collision-boxes",12);BodyAnimations=Values("body-animations",6);HornAnimations=Values("horn-animations",10);
        HornOffsets=Values("horn-offsets",18);var colors=Values("palette",4);AttackerRecoil=Values("attacker-recoil",3);
        if(groups.Count!=0)throw new InvalidOperationException($"{table.Path}: unclaimed Aquamentus profiles.");
        Palette=new Dictionary<int,Color[]>{{6,colors.Select(value=>new Color((value&31)/31f,((value>>5)&31)/31f,((value>>10)&31)/31f)).ToArray()}};
        var visual=GeneratedTable.Load(root+"effects/aquamentus_projectile.tsv",
            new("PART_AQUAMENTUS_PROJECTILE $40",GeneratedTableKeySemantics.Ordered,
                ["sprites","tile-base","palette","source-grayscale-inverted","animations-base64"],headerRequired:true)).SingleRow();
        Projectile=new(root,visual.SplitRequired(0,','),visual.UnsignedDecimal(1),visual.UnsignedDecimal(2),visual.Boolean01(3),visual.EncodedAnimations(4));
    }
}
