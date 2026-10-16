using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class BlackTowerEscapeDatabase
{
    private NpcRecord _wallmaster;
    private readonly Dictionary<int,string> _animations=[];
    internal byte[] Warp {get;}
    internal IReadOnlyList<(int Id,int SubId,Vector2 Position)> Placements {get;}
    internal BlackTowerEscapeDatabase()
    {
        var row=GeneratedTable.Load("res://assets/oracle/cutscenes/black_tower_escape_visuals.tsv",
            new("Black Tower escape Wallmaster",GeneratedTableKeySemantics.Unique,
                ["id","subid","sprite","tile-base","palette","animation","source"],["id","subid"],headerRequired:true)).SingleRow();
        string animation=row.RequiredString(5);
        _wallmaster=new(4,0xf3,row.HexByte(0),row.HexByte(1),0,0,0,0,row.RequiredString(2),row.Decimal(3),row.Decimal(4),0,false,
            animation,animation,animation,animation,"",NpcImplementationClassification.EventOwned);
        foreach(var record in GeneratedTable.Load("res://assets/oracle/cutscenes/black_tower_escape_animations.tsv",
            new("Black Tower escape Wallmaster animations",GeneratedTableKeySemantics.Unique,["id","index","animation","source"],["id","index"],headerRequired:true)).Rows)
            _animations.Add(record.Decimal(1),record.RequiredString(2));
        var table=GeneratedTable.Load("res://assets/oracle/cutscenes/black_tower_escape_tables.tsv",
            new("Black Tower escape warp",GeneratedTableKeySemantics.Unique,["profile","values","source"],["profile"],headerRequired:true)).SingleRow();
        Warp=Array.ConvertAll(table.SplitRequired(1,','),v=>Convert.ToByte(v,16));
        if(table.RequiredString(0)!="warp"||Warp.Length!=5||Warp[4]!=0x03)
            throw new InvalidOperationException("miscCutscenes.s:func_03_7cb7@warpDest: expected five bytes and native CUTSCENE_03 request.");
        var placements=new List<(int,int,Vector2)>();
        foreach(var record in GeneratedTable.Load("res://assets/oracle/cutscenes/black_tower_escape_placements.tsv",
            new("Black Tower escape ordered actors",GeneratedTableKeySemantics.Ordered,["id","subid","y","x","source"],headerRequired:true)).Rows)
            placements.Add((record.HexByte(0),record.HexByte(1),new(record.HexByte(3),record.HexByte(2))));
        Placements=placements;
    }
    internal NpcCharacter Wallmaster(Vector2 position)
    {
        var actor=NpcCharacter.CreateFromRecord(_wallmaster with {X=(int)position.X,Y=(int)position.Y});
        actor.SetAnimationRate(0);actor.SetBlocksLink(false);actor.SetScriptAnimation(Animation(0));
        return actor;
    }
    internal string Animation(int index)=>_animations[index];
}
