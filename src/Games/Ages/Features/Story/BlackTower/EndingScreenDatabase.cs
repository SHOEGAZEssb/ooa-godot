using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

internal sealed class EndingScreenDatabase
{
    internal const string Root=AgesGameContent.AssetRoot + "cutscenes/";
    private readonly Dictionary<string,List<EndingScreenUpload>> _uploads=[];
    private readonly Dictionary<int,List<EndingPaletteUpload>> _palettes=[];
    private readonly Dictionary<string,IntroOamPart[]> _oam=[];
    private readonly Dictionary<int,string> _texts=[];
    private readonly List<EndingScreenRamUpload> _secretRam=[];
    private readonly Dictionary<int,List<EndingScreenRamUpload>> _linkedRam=[];
    internal EndingScreenDatabase()
    {
        foreach(var row in GeneratedTable.Load(Root+"ending_graphics.tsv",new("Ending graphics headers",GeneratedTableKeySemantics.Ordered,
            ["profile","index","asset","target","bank","padding","invert","interleave","offset","length","source"],headerRequired:true)).Rows)
        {
            string profile=row.RequiredString(0);if(!_uploads.TryGetValue(profile,out var uploads))_uploads.Add(profile,uploads=[]);
            if(row.Decimal(1)!=uploads.Count)throw row.Invalid(1,"original upload order");
            uploads.Add(new(row.RequiredString(2),row.RequiredString(3),row.Decimal(4,0,1),row.Decimal(5,0,255),row.Boolean01(6),row.Boolean01(7),row.Decimal(8,0,1023),row.Decimal(9,0,2048)));
        }
        if(_uploads.Count!=28||_uploads["credits-linked-end"].Count!=4||_uploads["hero-secret-text"].Count!=1||_uploads["credits-waving"].Count!=9||_uploads["credits-linked6"].Count!=5||_uploads["credits-linked4"].Count!=5||_uploads["credits-linked5"].Count!=5||_uploads["credits-linked7"].Count!=5||_uploads["reunion-makutree"].Count!=8||_uploads["ending-secret"].Count!=11||_uploads["ending-continued"].Count!=3||_uploads["oracle"].Count!=8||_uploads["twinrova"].Count!=10||_uploads["closeup"].Count!=7||_uploads["credits-maku"].Count!=2||_uploads["credits-performance"].Count!=2||_uploads["credits-sword"].Count!=2||_uploads["credits-construction"].Count!=2||_uploads["credits-maku-past"].Count!=2||_uploads["credits-scroll"].Count!=9||_uploads["credits-end"].Count!=4||
            Enumerable.Range(0,8).Any(index=>_uploads[$"credits-image{index}"].Count!=((index&3)==0?6:(index&3)==1?11:10)))
            throw new InvalidOperationException("gfxHeaders.s:$2b..$2d incomplete ending uploads.");
        foreach(var row in GeneratedTable.Load(Root+"ending_ram_uploads.tsv",new("Ending RAM graphics uploads",GeneratedTableKeySemantics.Ordered,
            ["header","index","buffer","target","bank","length","source"],headerRequired:true)).Rows)
        {
            if(row.HexByte(0)!=0x2b||row.Decimal(1)!=_secretRam.Count)throw row.Invalid(1,"UNCMP_GFXH_2b original upload order");
            _secretRam.Add(new(row.RequiredString(2),row.HexWord(3),row.Decimal(4,0,1),row.Decimal(5,16,1024)));
        }
        if(_secretRam.Count!=3)throw new InvalidOperationException("uncmpGfxHeaders.s:uncmpGfxHeader2b requires three RAM uploads.");
        foreach(var row in GeneratedTable.Load(Root+"ending_linked_ram_uploads.tsv",new("Linked ending RAM graphics uploads",GeneratedTableKeySemantics.Ordered,
            ["header","index","buffer","target","bank","length","source"],headerRequired:true)).Rows)
        {
            int header=row.HexByte(0);if(!_linkedRam.TryGetValue(header,out var uploads))_linkedRam.Add(header,uploads=[]);
            if(row.Decimal(1)!=uploads.Count)throw row.Invalid(1,"original RAM upload order");
            uploads.Add(new(row.RequiredString(2),row.HexWord(3),row.Decimal(4,0,1),row.Decimal(5,16,1024)));
        }
        if(_linkedRam.Count!=2||_linkedRam[1].Count!=1||_linkedRam[0x2c].Count!=2)throw new InvalidOperationException("uncmpGfxHeaders.s: incomplete linked ending RAM headers $01/$2c.");
        foreach(var row in GeneratedTable.Load(Root+"ending_palettes.tsv",new("Ending palette slots",GeneratedTableKeySemantics.Ordered,
            ["header","kind","first","count","colors","source"],headerRequired:true)).Rows)
        {
            int header=row.HexByte(0),count=row.Decimal(3,1,8);if(!_palettes.TryGetValue(header,out var palettes))_palettes.Add(header,palettes=[]);
            byte[] bytes=Convert.FromHexString(row.RequiredString(4));if(bytes.Length!=count*12||bytes.Any(value=>value>31))throw row.Invalid(4,"four RGB5 component triples per palette slot");
            var colors=new Color[count,4];for(int index=0;index<count*4;index++)
            {
                colors[index/4,index%4]=new(bytes[index*3]/31f,bytes[index*3+1]/31f,bytes[index*3+2]/31f);
            }
            palettes.Add(new(row.RequiredString(1),row.Decimal(2,0,7),colors));
        }
        foreach(var row in GeneratedTable.Load(Root+"ending_oam.tsv",new("Ending OAM",GeneratedTableKeySemantics.Unique,
            ["name","parts","source"],["name"],headerRequired:true)).Rows)
        {
            var parts=row.SplitRequired(1,';').Select(value=>
            {
                var bytes=Array.ConvertAll(value.Split(','),v=>Convert.ToInt32(v,16));if(bytes.Length!=4)throw row.Invalid(1,"four-byte OAM records");
                return new IntroOamPart(bytes[0],bytes[1],bytes[2],bytes[3]);
            }).ToArray();_oam.Add(row.RequiredString(0),parts);
        }
        if(_oam["oamData_4e37"].Length!=40||_oam["oamData_4d05"].Length!=38||_oam["oamData_4d9e"].Length!=38)
            throw new InvalidOperationException("endgameCutsceneOamData.s: incomplete fullscreen OAM.");
        foreach(var row in GeneratedTable.Load(Root+"ending_texts.tsv",new("Fullscreen ending text",GeneratedTableKeySemantics.Unique,
            ["text-id","message-base64","source"],["text-id"],headerRequired:true)).Rows)_texts.Add(row.HexWord(0),row.Base64Utf8(1));
    }
    internal IReadOnlyList<EndingScreenUpload> Uploads(string profile)=>_uploads[profile];
    internal IReadOnlyList<EndingScreenRamUpload> SecretRamUploads=>_secretRam;
    internal IReadOnlyList<EndingScreenRamUpload> LinkedRamUploads(int header)=>_linkedRam[header];
    internal IntroOamPart[] Oam(string name)=>_oam[name];
    internal string Text(int id)=>_texts[id];
    internal void ApplyPalette(int header,BackgroundPaletteState background,RoomEntityManager entities)
    {
        foreach(var row in _palettes[header])
        {
            if(row.Kind=="Bg")background.LoadSlots(row.First,row.Colors);
            else if(row.Kind=="Spr")entities.LoadNativeObjectPaletteSlots(row.First,row.Colors);
            else throw new InvalidOperationException($"paletteHeaders.s: PALH_${header:x2} unsupported {row.Kind} upload.");
        }
    }
}

internal readonly record struct EndingScreenUpload(string Asset,string Target,int Bank,int Padding,bool Invert,bool Interleave,int Offset,int Length);
internal readonly record struct EndingScreenRamUpload(string Buffer,int Target,int Bank,int Length);
internal readonly record struct EndingPaletteUpload(string Kind,int First,Color[,] Colors);
