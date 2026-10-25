using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class FallingRockDatabase
{
    private readonly Dictionary<int,NpcRecord> _actors=[];
    private readonly Dictionary<string,byte[]> _tables=[];
    internal FallingRockDatabase()
    {
        foreach(var row in GeneratedTable.Load(AgesGameContent.AssetRoot + "objects/falling_rock_visuals.tsv",
            new("INTERAC_FALLING_ROCK $92 visuals",GeneratedTableKeySemantics.Unique,
                ["id","subid","sprite","tile-base","palette","animation","source"],["id","subid"],headerRequired:true)).Rows)
        {
            string animation=row.RequiredString(5);int subid=row.HexByte(1);
            _actors.Add(subid,new(0,0,0x92,subid,0,0,0,0,row.RequiredString(2),row.Decimal(3),row.Decimal(4),0,false,
                animation,animation,animation,animation,"",NpcImplementationClassification.EventOwned));
        }
        foreach(var row in GeneratedTable.Load(AgesGameContent.AssetRoot + "objects/falling_rock_tables.tsv",
            new("INTERAC_FALLING_ROCK $92 native tables",GeneratedTableKeySemantics.Unique,["profile","values","source"],["profile"],headerRequired:true)).Rows)
            _tables.Add(row.RequiredString(0),Array.ConvertAll(row.SplitRequired(1,','),v=>Convert.ToByte(v,16)));
        if(Bytes("rock-angles").Length!=24||Bytes("rock-positions-0").Length!=32||Bytes("rock-positions-1").Length!=32)
            throw new InvalidOperationException("fallingRock.s: malformed debris-angle/random-position table boundary.");
    }
    internal byte[] Bytes(string name)=>_tables[name];
    internal NpcCharacter Actor(int subid,Vector2 position)
    {
        var actor=NpcCharacter.CreateFromRecord(_actors[subid] with {X=(int)position.X,Y=(int)position.Y});
        actor.SetAnimationRate(0);actor.SetBlocksLink(false);actor.SetScriptVisible(false);
        actor.SetFixedDrawPriority(ObjectDrawPriority.InFrontOfLinkZIndex);return actor;
    }
}
