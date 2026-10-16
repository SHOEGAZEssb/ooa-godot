using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class BlackTowerPossessionDatabase
{
    private const string Root="res://assets/oracle/cutscenes/black_tower_possession_";
    private readonly Dictionary<(int,int),NpcRecord> _actors=[];
    private readonly Dictionary<(int,int),string> _animations=[];
    internal IReadOnlyList<CutsceneCommand> Intro { get; }=CutsceneCommandCatalog.Load(Root+"intro.tsv");
    internal IReadOnlyList<CutsceneCommand> Rescue { get; }=CutsceneCommandCatalog.Load(Root+"rescue.tsv");
    internal IReadOnlyList<CutsceneCommand> Ghost { get; }=CutsceneCommandCatalog.Load(Root+"ghost.tsv");
    private readonly Color[] _evil=OracleGraphicsData.LoadPaletteColors("res://assets/oracle/metadata/nayru_possessed_palette.bin",transparentZero:true);
    internal BlackTowerPossessionDatabase()
    {
        foreach(var row in GeneratedTable.Load(Root+"animations.tsv",new("Black Tower possession animations",GeneratedTableKeySemantics.Unique,
            ["id","index","animation","source"],["id","index"],headerRequired:true)).Rows)
            _animations.Add((row.HexByte(0),row.HexByte(1)),row.RequiredString(2));
        foreach(var row in GeneratedTable.Load(Root+"visuals.tsv",new("Black Tower possession actors",GeneratedTableKeySemantics.Unique,
            ["id","subid","sprite","tile-base","palette","default-animation","source"],["id","subid"],headerRequired:true)).Rows)
        {
            int id=row.HexByte(0),subid=row.HexByte(1),animation=row.Decimal(5);string encoded=_animations[(id,animation)];
            _actors.Add((id,subid),new(4,0xfc,id,subid,0,0,0,0,row.RequiredString(2),row.Decimal(3),row.Decimal(4),animation,false,
                encoded,encoded,encoded,encoded,"",NpcImplementationClassification.EventOwned));
        }
    }
    internal NpcCharacter Actor(int id,int subid,Vector2 position)
    {
        var actor=NpcCharacter.CreateFromRecord(_actors[(id,subid)] with {X=(int)position.X,Y=(int)position.Y});
        actor.SetAnimationRate(0);actor.SetBlocksLink(false);actor.SetScriptAnimation(actor.Record.DownAnimation);
        if(id==0x4d && subid==6) {actor.SetBasePalette(6);actor.SetScriptPaletteOverrides(new Dictionary<int,Color[]>{[6]=_evil});}
        return actor;
    }
}
