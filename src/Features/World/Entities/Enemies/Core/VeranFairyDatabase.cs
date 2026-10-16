using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class VeranFairyDatabase
{
    private readonly Dictionary<string,byte[]> _tables=[];
    private readonly Dictionary<int,ImportedEnemyDefinition> _actors=[];
    private readonly Dictionary<int,string> _texts=[];
    internal IReadOnlyDictionary<int,Color[]> MimicPalettes {get;}
    internal VeranFairyDatabase()
    {
        foreach(var row in GeneratedTable.Load("res://assets/oracle/objects/veran_fairy_tables.tsv",
            new("Veran fairy native tables",GeneratedTableKeySemantics.Unique,["profile","values","source"],["profile"],headerRequired:true)).Rows)
            _tables.Add(row.RequiredString(0),Array.ConvertAll(row.SplitRequired(1,','),v=>Convert.ToByte(v,16)));
        foreach(var (name,count) in new[]{("pillars",12),("mimics",4),("explosions",8),("pattern-0",9),("pattern-1",11),
            ("pattern-2",11),("pattern-3",11),("attacks",24),("speeds",3),("wrap-keys",21),("wrap-positions",16),
            ("collision-06",32),("collision-0d",32),("collision-39",32),("collision-77",32),
            ("active-enemy-06",32),("active-enemy-64",32),("active-part-2d",32),("active-part-2f",32),("part-2d",8),("part-2f",8),("part-boundaries",16)})
            if(!_tables.TryGetValue(name,out var bytes)||bytes.Length!=count)
                throw new InvalidOperationException($"veranFairy.s: missing or malformed {name}.");
        foreach(var row in GeneratedTable.Load("res://assets/oracle/objects/veran_fairy_actors.tsv",
            new("Veran fairy actors",GeneratedTableKeySemantics.Unique,
                ["id","subid","sprites","tile-base","palette","source-grayscale-inverted","radius-y","radius-x","damage-quarters","health","animations-base64"],
                ["id","subid"],headerRequired:true)).Rows)
            _actors.Add(row.HexByte(0),new(row.HexByte(0),row.HexByte(1),row.SplitRequired(2,','),row.Decimal(3),row.Decimal(4),row.Boolean01(5),
                row.Decimal(6),row.Decimal(7),row.Decimal(8),row.Decimal(9),row.EncodedAnimations(10)));
        foreach(var row in GeneratedTable.Load("res://assets/oracle/cutscenes/veran_fairy_texts.tsv",
            new("Veran fairy dialogue",GeneratedTableKeySemantics.Unique,["text-id","message-base64","source"],["text-id"],headerRequired:true)).Rows)
            _texts.Add(row.HexWord(0),row.Base64Utf8(1));
        var palette=OracleGraphicsData.LoadPalette("res://assets/oracle/objects/link_mimic_palette.bin",1,6);
        MimicPalettes=new Dictionary<int,Color[]> {[6]=[palette[6,0],palette[6,1],palette[6,2],palette[6,3]]};
    }
    internal ImportedEnemyDefinition Actor(int id)=>_actors[id];
    internal byte[] Bytes(string name)=>_tables[name];
    internal string Text(int id)=>_texts[id];
    internal bool Active(bool part,int id,int item)=>_tables[$"active-{(part?"part":"enemy")}-{id:x2}"][item]!=0;
    internal int Effect(int mode,int item)=>_tables[$"collision-{mode:x2}"][item];
}
