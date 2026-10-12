using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class PlasmarineProfile
{
    private static readonly Lazy<PlasmarineProfile> LazyShared=new(()=>new());
    internal static PlasmarineProfile Shared=>LazyShared.Value;
    internal IReadOnlyList<int> State {get;}
    internal IReadOnlyList<int> PartState {get;}
    internal IReadOnlyList<int> PartBoundaryOffsets {get;}
    private readonly IReadOnlyList<int> _projectiles,_effects,_shockEffects,_active;
    private PlasmarineProfile()
    {
        var table=GeneratedTable.Load("res://assets/oracle/enemies/plasmarine.tsv",
            new GeneratedTableSchema("Plasmarine native tables",GeneratedTableKeySemantics.Grouped,
                ["profile","index","value","source"],["profile"],headerRequired:true));
        var profiles=new Dictionary<string,List<int>>();
        foreach(var row in table.Rows)
        {
            string profile=row.RequiredString(0);
            if(!profiles.TryGetValue(profile,out var values))profiles.Add(profile,values=[]);
            if(row.UnsignedDecimal(1)!=values.Count)throw row.Invalid(1,$"contiguous {profile} indices");
            values.Add(row.Decimal(2));_=row.RequiredString(3);
        }
        IReadOnlyList<int> Take(string profile,int count)
        {
            if(!profiles.Remove(profile,out var values) || values.Count!=count)
                throw new InvalidOperationException($"plasmarine.s: {profile} requires {count} source values.");
            return values;
        }
        State=Take("state",11);_projectiles=Take("projectile-counts",7);
        PartState=Take("part-state",8);PartBoundaryOffsets=Take("part-boundary-offsets",16);
        _effects=Take("effects-4f",32);_shockEffects=Take("effects-68",32);_active=Take("active-7e",32);
        if(profiles.Count!=0)throw new InvalidOperationException("plasmarine.s: unconsumed source tables.");
    }
    internal int ProjectileCount(int health)=>health is >=1 and <=7 ? _projectiles[health-1] :
        throw new InvalidOperationException($"plasmarine.s: projectile table requires health$01..$07, got${health:x2}.");
    internal int Effect(int mode,int item)=>mode switch
    {
        0x4f=>_effects[item],0x68=>_shockEffects[item],
        _=>throw new InvalidOperationException($"plasmarine.s: unsupported collision mode${mode:x2}.")
    };
    internal bool Active(int item)=>_active[item]!=0;
}
