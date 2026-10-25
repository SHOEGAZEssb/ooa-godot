using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

internal sealed class GanonDatabase
{
    private readonly Dictionary<int,ImportedEnemyDefinition> _actors=[];
    private readonly Dictionary<string,byte[]> _tables=[];
    private readonly Dictionary<int,string> _texts=[];
    internal Color[,] SpritePalette {get;}=new Color[1,4];
    internal Color[,] BackgroundPalette {get;}=new Color[1,4];
    private readonly Color[] _reversePalettes;
    internal Color[,] ReversePalette(int index)
    {
        var colors=new Color[1,4];for(int i=0;i<4;i++)colors[0,i]=_reversePalettes[index*4+i];return colors;
    }
    internal EnemyCharacterConfiguration Visual {get;}
    internal IReadOnlyList<int[]> SourceOffsets {get;}
    internal GanonDatabase()
    {
        const string root=AgesGameContent.AssetRoot + "objects/";
        foreach(var row in GeneratedTable.Load(root+"ganon_actors.tsv",new("Ganon actors",GeneratedTableKeySemantics.Unique,
            ["id","subid","sprites","tile-base","palette","source-grayscale-inverted","radius-y","radius-x","damage-quarters","health","animations-base64"],
            ["id","subid"],headerRequired:true)).Rows)
            _actors.Add(row.HexByte(0),new(row.HexByte(0),row.HexByte(1),row.SplitRequired(2,','),row.Decimal(3),row.Decimal(4),row.Boolean01(5),
                row.Decimal(6),row.Decimal(7),row.Decimal(8),row.Decimal(9),row.EncodedAnimations(10)));
        foreach(var row in GeneratedTable.Load(root+"ganon_tables.tsv",new("Ganon native tables",GeneratedTableKeySemantics.Unique,
            ["profile","values","source"],["profile"],headerRequired:true)).Rows)
            _tables.Add(row.RequiredString(0),Array.ConvertAll(row.SplitRequired(1,','),s=>Convert.ToByte(s,16)));
        foreach(var row in GeneratedTable.Load(AgesGameContent.AssetRoot + "cutscenes/ganon_texts.tsv",new("Ganon text",GeneratedTableKeySemantics.Unique,
            ["text-id","message-base64","source"],["text-id"],headerRequired:true)).Rows)
            _texts.Add(row.HexWord(0),row.Base64Utf8(1));
        var sprite=OracleGraphicsData.LoadPaletteColors(root+"ganon_sprite_palette_6.bin",transparentZero:true);
        var background=OracleGraphicsData.LoadPaletteColors(root+"ganon_background_palette_7.bin");
        var reverseBytes=OracleGraphicsData.ReadBytes(root+"ganon_reverse_palettes.bin",96);
        _reversePalettes=new Color[32];
        for(int i=0;i<32;i++)_reversePalettes[i]=new(reverseBytes[i*3]/31f,reverseBytes[i*3+1]/31f,reverseBytes[i*3+2]/31f);
        for(int i=0;i<4;i++){SpritePalette[0,i]=sprite[i];BackgroundPalette[0,i]=background[i];}
        var sources=new List<string>();var offsets=new List<int[]>();var banks=new Dictionary<string,int>();int next=0;
        foreach(var row in GeneratedTable.Load(root+"ganon_visuals.tsv",new("Ganon animation VRAM layouts",GeneratedTableKeySemantics.Unique,
            ["animation","sprites","source"],["animation"],headerRequired:true)).Rows)
        {
            int animation=row.UnsignedDecimal(0);if(animation!=offsets.Count)throw row.Invalid(0,"ordered native animation index");
            string key=row.RequiredString(1);
            if(!banks.TryGetValue(key,out int offset))
            {
                offset=next;banks.Add(key,offset);
                foreach(string name in row.SplitRequired(1,','))
                {
                    sources.Add(name);var image=OracleGraphicsCache.LoadImage(AgesGameContent.AssetRoot + $"gfx/{name}.png");
                    if(image.GetHeight()!=16||image.GetWidth()>128)throw row.Invalid(1,"native 32-tile OBJ graphics sheet");
                    // AppendGraphics aligns each sheet to a $200-byte VRAM
                    // block even when a PNG omits its unused trailing cells.
                    next+=512;
                }
            }
            int frames=OracleGraphicsCache.GetAnimationDefinition(Actor(4).Animations[animation]).Frames.Length;
            offsets.Add(Enumerable.Repeat(offset,frames).ToArray());
        }
        if(offsets.Count!=15)throw new InvalidOperationException("ganon.s: expected fifteen native animations.");
        Visual=EnemyCharacterConfiguration.FromImported(Actor(4)) with {Source=EnemyVisualSource.LoadComposite(sources.ToArray())};
        SourceOffsets=offsets;
    }
    internal ImportedEnemyDefinition Actor(int id)=>_actors[id];
    internal byte[] Bytes(string name)=>_tables[name];
    internal string Text(int id)=>_texts[id];
    internal int Effect(int mode,int item)=>Bytes($"collision-{mode:x2}")[item];
    internal bool Active(bool part,int id,int item)=>Bytes(part?$"active-part-{id:x2}":$"active-{id:x2}")[item]!=0;
}
