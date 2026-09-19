using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

internal sealed class DefeatedMoblinDatabase
{
    private const string Root="res://assets/oracle/cutscenes/";
    private readonly Dictionary<string,byte[]> _native=[];
    private readonly Dictionary<int,(string Sprite,int Tile,int Palette,int Animation,string[] Animations)> _visuals=[];
    internal IReadOnlyList<CutsceneCommand> Commands {get;}=CutsceneCommandCatalog.Load(Root+"defeated_moblin_commands.tsv");
    internal DefeatedMoblinDatabase()
    {
        var table=GeneratedTable.Load(Root+"defeated_moblin_native.tsv",new GeneratedTableSchema("defeated Moblin native",GeneratedTableKeySemantics.Unique,
            ["key","values","source"],["key"],headerRequired:true));
        foreach(var row in table.Rows) _native.Add(row.RequiredString(0),Array.ConvertAll(row.SplitRequired(1,','),s=>Convert.ToByte(s,16)));
        foreach(var (key,count) in new[]{("placement",2),("gorons",16),("directions",8),("speeds",2)})
            if(!_native.TryGetValue(key,out var bytes)||bytes.Length!=count) throw new InvalidOperationException($"INTERAC $72: missing {key}.");
        table=GeneratedTable.Load(Root+"defeated_moblin_visuals.tsv",new GeneratedTableSchema("defeated Moblin visuals",GeneratedTableKeySemantics.Unique,
            ["subid","sprite","tile-base","palette","default-animation","animations-base64"],["subid"],headerRequired:true));
        foreach(var row in table.Rows) _visuals.Add(row.UnsignedDecimal(0),(row.RequiredString(1),row.UnsignedDecimal(2),row.UnsignedDecimal(3),row.UnsignedDecimal(4),row.EncodedAnimations(5)));
        if(_visuals.Count!=3 || _visuals.Values.Any(v=>v.Animations.Length!=8)) throw new InvalidOperationException("INTERAC $72: incomplete graphics.");
    }
    internal byte[] Bytes(string key)=>_native[key];
    internal string Animation(int index)=>_visuals[0].Animations[index];
    internal int Entry(int subid,int index)=>Commands.First(c=>c.Source.Label== (subid switch {
        0=>"kingMoblinDefeated_kingScript",1=>"kingMoblinDefeated_helperMoblinScript",_=>$"kingMoblinDefeated_goron{index}"
    })).Source.CommandIndex;
    internal NpcRecord Actor(int subid,int index,int y,int x)
    {
        var v=_visuals[subid]; string animation=v.Animations[v.Animation];
        return new(0,9,0x72,subid,y,x,index,0,v.Sprite,v.Tile,v.Palette,v.Animation,false,
            animation,animation,animation,animation,string.Empty,NpcImplementationClassification.EventOwned);
    }
}
