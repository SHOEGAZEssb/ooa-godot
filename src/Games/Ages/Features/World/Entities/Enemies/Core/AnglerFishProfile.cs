using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class AnglerFishProfile
{
    private static readonly Lazy<AnglerFishProfile> LazyShared=new(()=>new());
    internal static AnglerFishProfile Shared=>LazyShared.Value;
    internal IReadOnlyList<int> State {get;}
    internal string AntennaSprite {get;}
    internal int AntennaTileBase {get;}
    internal int AntennaPalette {get;}
    internal int AntennaAnimation {get;}
    private readonly IReadOnlyList<int> _offsets,_bodyEffects,_antennaEffects,_bubbleEffects,_active,_bubbleActive;
    private AnglerFishProfile()
    {
        var table=GeneratedTable.Load(AgesGameContent.AssetRoot + "enemies/angler_fish.tsv",
            new GeneratedTableSchema("Angler Fish native tables",GeneratedTableKeySemantics.Grouped,
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
                throw new InvalidOperationException($"anglerFish.s: {profile} requires {count} source values.");
            return values;
        }
        State=Take("state",14);_offsets=Take("antenna-offsets",12);
        _bodyEffects=Take("effects-64",32);_antennaEffects=Take("effects-47",32);_bubbleEffects=Take("effects-24",32);
        _active=Take("active-76",32);_bubbleActive=Take("active-26",32);
        if(profiles.Count!=0)throw new InvalidOperationException("anglerFish.s: unconsumed source tables.");
        var visual=GeneratedTable.Load(AgesGameContent.AssetRoot + "enemies/angler_fish_antenna.tsv",
            new GeneratedTableSchema("Angler Fish fixed-bank antenna",GeneratedTableKeySemantics.Unique,
                ["sprite","tile-base","palette","animation","source"],["sprite"],headerRequired:true));
        var visualRow=visual.SingleRow();
        AntennaSprite=visualRow.RequiredString(0);AntennaTileBase=visualRow.Decimal(1,0,255);
        AntennaPalette=visualRow.Decimal(2,0,7);AntennaAnimation=visualRow.Decimal(3,0,6);_=visualRow.RequiredString(4);
    }
    internal Vector2I AntennaOffset(int direction)
    {
        if(direction is <0 or >5)throw new InvalidOperationException($"anglerFish_antenna: unsupported direction${direction:x2}.");
        return new(unchecked((sbyte)_offsets[direction*2+1]),unchecked((sbyte)_offsets[direction*2]));
    }
    internal int Effect(int mode,int item)=>mode switch
    {
        0x64=>_bodyEffects[item],0x47=>_antennaEffects[item],0x24=>_bubbleEffects[item],
        _=>throw new InvalidOperationException($"anglerFish.s: unsupported collision mode${mode:x2}.")
    };
    internal bool Active(bool bubble,int item)=>(bubble?_bubbleActive:_active)[item]!=0;
}
