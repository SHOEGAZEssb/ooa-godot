using Godot;

namespace Oracles;

internal sealed class ExclamationMarkDatabase
{
    private readonly string _sprite;
    private readonly int _tileBase;
    private readonly int _palette;
    private readonly string _animation;

    internal ExclamationMarkDatabase(string assetRoot)
    {
        var row=GeneratedTable.Load(assetRoot+"effects/exclamation_mark.tsv",
            new("INTERAC_EXCLAMATION_MARK $9f:$00",GeneratedTableKeySemantics.Ordered,
                ["sprite","tile-base","palette","animation","source"],headerRequired:true)).SingleRow();
        _sprite=row.RequiredString(0);_tileBase=row.UnsignedDecimal(1);_palette=row.UnsignedDecimal(2);
        _animation=row.RequiredString(3);_=row.RequiredString(4);
    }

    internal NpcRecord CreateRecord(OracleRoomData room,Vector2 position)=>new(room.Group,room.Id,InteractionId.ExclamationMark,0,
        (int)position.Y,(int)position.X,0,0,_sprite,_tileBase,_palette,0,false,
        _animation,_animation,_animation,_animation,string.Empty,NpcImplementationClassification.EventOwned);
}
