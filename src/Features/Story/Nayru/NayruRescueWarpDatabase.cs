using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class NayruRescueWarpDatabase
{
    private const string Root="res://assets/oracle/cutscenes/";
    private readonly Dictionary<(int,int),NpcRecord> _actors=[];
    private readonly Dictionary<(int,int),string> _animations=[];
    private readonly Dictionary<string,IReadOnlyList<CutsceneCommand>> _commands=[];
    private readonly Dictionary<int,string> _texts=[];
    private readonly Dictionary<string,int[]> _tables=[];
    private readonly Dictionary<(int,int),string[]> _sprites=[];
    internal MakuTreeAdviceDatabase Maku { get; } = new();
    internal Color[] StonePalette { get; }
    internal NayruRescueWarpDatabase()
    {
        foreach(var row in GeneratedTable.Load(Root+"nayru_rescue_visuals.tsv",
            new("Nayru rescue actor graphics",GeneratedTableKeySemantics.Unique,
                ["id","subid","sprites","tile-base","palette","animation","source"],["id","subid"],headerRequired:true)).Rows)
        {
            int id=row.HexByte(0),subid=row.HexByte(1);var sprites=row.SplitRequired(2,',');string animation=row.RequiredString(5);
            _sprites.Add((id,subid),sprites);
            _actors.Add((id,subid),new(0,0x38,id,subid,0,0,0,0,sprites[0],row.Decimal(3),row.Decimal(4),0,false,
                animation,animation,animation,animation,"",NpcImplementationClassification.EventOwned));
        }
        foreach(var row in GeneratedTable.Load(Root+"nayru_rescue_animations.tsv",
            new("Nayru rescue animation",GeneratedTableKeySemantics.Unique,["id","index","animation","source"],["id","index"],headerRequired:true)).Rows)
            _animations.Add((row.HexByte(0),row.HexByte(1)),row.RequiredString(2));
        foreach(string script in new[] {"nayru-arrival","nayru-song","nayru-farewell","ralph-arrival","ralph-greeting","ralph-farewell",
            "maku","cloaked","twinrova","boy","old-lady","nayru-escape","ralph-escape","nayru-ending","ralph-ending",
            "nayru-palace","nayru-palace-farewell","ralph-palace","ralph-palace-farewell",
            "nayru-credits-maku","ralph-credits-maku","ralph-credits-sword","maku-credits","nayru-reunion","maku-reunion"})
            _commands.Add(script,CutsceneCommandCatalog.Load(Root+$"nayru_rescue_{script}_commands.tsv"));
        foreach(var row in GeneratedTable.Load(Root+"nayru_rescue_texts.tsv",
            new("Nayru rescue dialogue",GeneratedTableKeySemantics.Unique,["text-id","message-base64","source"],["text-id"],headerRequired:true)).Rows)
            _texts.Add(row.HexWord(0),row.Base64Utf8(1));
        foreach(var row in GeneratedTable.Load(Root+"nayru_rescue_tables.tsv",
            new("Nayru rescue native tables",GeneratedTableKeySemantics.Unique,["profile","values","source"],["profile"],headerRequired:true)).Rows)
            _tables.Add(row.RequiredString(0),Array.ConvertAll(row.SplitRequired(1,','),value=>Convert.ToInt32(value,16)));
        var palette=OracleGraphicsData.LoadPalette(Root+"nayru_stone_sprite_palette.bin",1,6);
        StonePalette=[palette[6,0],palette[6,1],palette[6,2],palette[6,3]];
    }
    internal NpcCharacter CreateActor(int id,int subid,Vector2 position)
    {
        var actor=NpcCharacter.CreateFromRecord(_actors[(id,subid)] with {X=(int)position.X,Y=(int)position.Y});
        foreach(string sprite in _sprites[(id,subid)][1..]) actor.AppendScriptGraphics(sprite);
        actor.SetAnimationRate(0);actor.SetBlocksLink(false);actor.SetDialogue(0,"",false);return actor;
    }
    internal string Animation(int id,int index) => _animations[(id,index)];
    internal IReadOnlyList<CutsceneCommand> Commands(string script) => _commands[script];
    internal string Text(int id) => _texts[id];
    internal int[] Table(string name) => _tables[name];
}
