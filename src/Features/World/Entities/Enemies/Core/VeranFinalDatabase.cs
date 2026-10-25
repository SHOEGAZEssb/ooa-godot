using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class VeranFinalDatabase
{
    private readonly Dictionary<string,byte[]> _tables=[];
    private readonly Dictionary<int,ImportedEnemyDefinition> _actors=[];
    private readonly Dictionary<int,string> _texts=[];
    internal IReadOnlyDictionary<int,Color[]> Palettes {get;}
    internal VeranFinalDatabase()
    {
        foreach(var row in GeneratedTable.Load(AgesGameContent.AssetRoot + "objects/veran_final_tables.tsv",
            new("Veran final native tables",GeneratedTableKeySemantics.Unique,["profile","values","source"],["profile"],headerRequired:true)).Rows)
            _tables.Add(row.RequiredString(0),Array.ConvertAll(row.SplitRequired(1,','),v=>Convert.ToByte(v,16)));
        foreach(var (name,count) in new[]{("transform-probability",5),("transform-sequence",4),("spider-counters",4),("spider-angles",32),
            ("bee-corners",16),("bee-counters",2),("bee-targets",32),("acid-cardinals",4),("acid-diagonals",4),("child-angles",3),("death-explosions",16),
            ("collision-06",32),("collision-09",32),("collision-1f",32),("collision-6a",32),("collision-6b",32),("collision-6c",32),("collision-7c",32),
            ("active-enemy-02",32),("active-enemy-16",32),("active-enemy-1f",32),("active-part-56",32),("active-part-58",32),("part-56",8),("part-57",8),("part-58",8)})
            if(!_tables.TryGetValue(name,out var values)||values.Length!=count)
                throw new InvalidOperationException($"veranFinalForm.s: missing or malformed {name}.");
        foreach(var row in GeneratedTable.Load(AgesGameContent.AssetRoot + "objects/veran_final_actors.tsv",
            new("Veran final actors",GeneratedTableKeySemantics.Unique,
                ["id","subid","sprites","tile-base","palette","source-grayscale-inverted","radius-y","radius-x","damage-quarters","health","animations-base64"],
                ["id","subid"],headerRequired:true)).Rows)
            _actors.Add(row.HexByte(0),new(row.HexByte(0),row.HexByte(1),row.SplitRequired(2,','),row.Decimal(3),row.Decimal(4),row.Boolean01(5),
                row.Decimal(6),row.Decimal(7),row.Decimal(8),row.Decimal(9),row.EncodedAnimations(10)));
        foreach(var row in GeneratedTable.Load(AgesGameContent.AssetRoot + "cutscenes/veran_final_texts.tsv",
            new("Veran final dialogue",GeneratedTableKeySemantics.Unique,["text-id","message-base64","source"],["text-id"],headerRequired:true)).Rows)
            _texts.Add(row.HexWord(0),row.Base64Utf8(1));
        var palette=OracleGraphicsData.LoadPalette(AgesGameContent.AssetRoot + "objects/veran_final_palettes.bin",2,6);
        Palettes=new Dictionary<int,Color[]> {[6]=[palette[6,0],palette[6,1],palette[6,2],palette[6,3]],
            [7]=[palette[7,0],palette[7,1],palette[7,2],palette[7,3]]};
    }
    internal ImportedEnemyDefinition Actor(int id)=>_actors[id];
    internal byte[] Bytes(string name)=>_tables[name];
    internal string Text(int id)=>_texts[id];
    internal int Effect(int mode,int item)=>_tables[$"collision-{mode:x2}"][item];
    internal bool Active(bool part,int id,int item)=>_tables[$"active-{(part?"part":"enemy")}-{id:x2}"][item]!=0;
}
