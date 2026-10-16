using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class ExplosionWithDebrisDatabase
{
    private readonly Dictionary<int,NpcRecord> _visuals=[];
    internal ExplosionWithDebrisDatabase()
    {
        foreach(var row in GeneratedTable.Load("res://assets/oracle/objects/explosion_debris_visuals.tsv",new("INTERAC_EXPLOSION_WITH_DEBRIS $99",
            GeneratedTableKeySemantics.Unique,["subid","sprite","tile-base","palette","animation","source"],["subid"],headerRequired:true)).Rows)
        {
            int subid=row.Decimal(0);string anim=row.RequiredString(4);
            _visuals.Add(subid,new(0,0,0x99,subid,0,0,0,0,row.RequiredString(1),row.Decimal(2),row.Decimal(3),0,false,
                anim,anim,anim,anim,"",NpcImplementationClassification.EventOwned));
        }
    }
    internal NpcCharacter Actor(int subid,Vector2 position)
    {
        var actor=NpcCharacter.CreateFromRecord(_visuals[subid] with {X=(int)position.X,Y=(int)position.Y});
        actor.SetScriptAnimation(_visuals[subid].DownAnimation);actor.SetAnimationRate(0);actor.SetBlocksLink(false);
        actor.SetFixedDrawPriority(ObjectDrawPriority.InFrontOfLinkZIndex);return actor;
    }
}
