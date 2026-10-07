using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

// ITEM$0c owns an eight-sector arc rather than the ordinary sword selector.
internal sealed class BiggoronSwordDatabase
{
    private readonly BiggoronSwordGraphic[] _weapon = new BiggoronSwordGraphic[8];
    private readonly SwordArc[] _arcs = new SwordArc[8];
    private readonly Dictionary<int,List<BiggoronSwordParentFrame>> _parents = new();
    private readonly Dictionary<(int Mode,int Frame,int Direction),LinkGraphicRecord> _link = new();

    internal BiggoronSwordDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/metadata/biggoron_sword_animations.tsv",
            new GeneratedTableSchema("ITEM$0c animations",GeneratedTableKeySemantics.Unique,
                ["animation","sprite","tile-base","oam-flags","source-offset","collision",
                 "radius-y","radius-x","damage","health","sound","frames","source"],
                ["animation"],headerRequired:true));
        if(table.Rows.Count!=8) throw new InvalidOperationException("ITEM$0c requires eight ordered weapon poses.");
        for(int i=0;i<8;i++)
        {
            var row=table.Rows[i];
            if(row.Decimal(0)!=i) throw row.Invalid(0,"ordered pose $00-$07");
            var graphic = new BiggoronSwordGraphic(row.RequiredString(1),row.HexByte(2),row.HexByte(3),
                row.Decimal(4,0,65535),row.HexByte(5),new(row.Decimal(7,0,15),row.Decimal(6,0,15)),
                row.HexByte(8),row.HexByte(9),row.HexByte(10),row.RequiredString(11),row.RequiredString(12));
            _=OracleGraphicsCache.GetAnimationDefinition(graphic.Animation);
            _weapon[i]=graphic;
        }
        table=GeneratedTable.Load("res://assets/oracle/metadata/biggoron_sword_arcs.tsv",
            new GeneratedTableSchema("biggoronSwordArcData",GeneratedTableKeySemantics.Unique,
                ["index","radius-y","radius-x","offset-y","offset-x","source"],["index"],headerRequired:true));
        if(table.Rows.Count!=8) throw new InvalidOperationException("biggoronSwordArcData requires eight ordered sectors.");
        for(int i=0;i<8;i++)
        {
            var row=table.Rows[i];
            if(row.Decimal(0)!=i) throw row.Invalid(0,"ordered sector $00-$07");
            _arcs[i]=new(row.Decimal(1,0,255),row.Decimal(2,0,255),
                row.Decimal(3,-128,127),row.Decimal(4,-128,127));
            _=row.RequiredString(5);
        }
        table=GeneratedTable.Load("res://assets/oracle/metadata/biggoron_sword_parent_animations.tsv",
            new GeneratedTableSchema("ITEM$0c parent animations",GeneratedTableKeySemantics.Unique,
                ["mode","frame","duration","graphic","parameter","source"],["mode","frame"],headerRequired:true));
        if(table.Rows.Count!=12) throw new InvalidOperationException("ITEM$0c requires twelve parent frames in modes $23/$27.");
        foreach(var row in table.Rows)
        {
            int mode=row.HexByte(0);
            if(mode is not (0x23 or 0x27)) throw row.Invalid(0,"ITEM$0c mode $23/$27");
            if(!_parents.TryGetValue(mode,out var frames)) _parents.Add(mode,frames=new());
            if(row.Decimal(1)!=frames.Count) throw row.Invalid(1,"ordered parent frames");
            frames.Add(new(row.Decimal(2,1,255),row.HexByte(3),row.HexByte(4),row.RequiredString(5)));
        }
        foreach(int mode in new[]{0x23,0x27})
            if(!_parents.TryGetValue(mode,out var frames) || frames.Count!=6 ||
                (frames[^1].Parameter&0x80)==0 || frames.GetRange(0,5).Exists(frame=>(frame.Parameter&0x80)!=0))
                throw new InvalidOperationException($"ITEM$0c mode ${mode:x2} lacks its six-frame terminal stream.");
        table=GeneratedTable.Load("res://assets/oracle/metadata/biggoron_sword_link_graphics.tsv",
            new GeneratedTableSchema("ITEM$0c Link graphics",GeneratedTableKeySemantics.Unique,
                ["kind","variant","phase","direction","graphics-index","oam-index","byte-offset","oam","source"],
                ["kind","variant","phase","direction"],headerRequired:true));
        if(table.Rows.Count!=48) throw new InvalidOperationException("ITEM$0c requires 48 ordered Link graphics.");
        for(int i=0;i<48;i++)
        {
            var row=table.Rows[i]; int mode=i<24?0x23:0x27,frame=i%24/4,direction=i%4;
            if(row.RequiredString(0)!="biggoron" || row.Decimal(1)!=mode || row.Decimal(2)!=frame ||
                row.Decimal(3)!=direction || row.HexByte(4)!=_parents[mode][frame].Graphic+direction)
                throw row.Invalid(0,$"ordered Biggoron mode ${mode:x2}/frame {frame}/direction {direction}");
            string oam=row.RequiredString(7);
            _=OracleGraphicsCache.GetAnimationDefinition($"127,0@{oam}");
            _link.Add((mode,frame,direction),new("biggoron",mode,frame,direction,row.HexByte(4),row.HexByte(5),
                row.HexWord(6),oam,oam.Split(';').All(part=>(int.Parse(part.Split(',')[3])&0x20)!=0),row.RequiredString(8)));
        }
    }

    internal BiggoronSwordGraphic Weapon(int pose) => pose is >=0 and <8 ? _weapon[pose] :
        throw new ArgumentOutOfRangeException(nameof(pose));
    internal SwordArc Arc(int sector) => sector is >=0 and <8 ? _arcs[sector] :
        throw new ArgumentOutOfRangeException(nameof(sector));
    internal IReadOnlyList<BiggoronSwordParentFrame> Frames(int mode) => _parents.TryGetValue(mode,out var frames)?frames:
        throw new NotSupportedException($"ITEM$0c parent mode ${mode:x2} is not imported.");
    internal LinkGraphicRecord LinkGraphic(int mode,int frame,int direction) => _link.TryGetValue((mode,frame,direction),out var graphic)?graphic:
        throw new NotSupportedException($"ITEM$0c Link mode ${mode:x2}/frame {frame}/direction {direction} is not imported.");
    internal int SelectArc(int parameter,int direction)
    {
        if(direction is <0 or >3) throw new ArgumentOutOfRangeException(nameof(direction));
        int phase=(parameter&0x0e)>>1;
        return (direction==1?phase:(direction+1)*2-phase)&7;
    }
}

internal readonly record struct BiggoronSwordGraphic(string Sprite,int TileBase,int OamFlags,int SourceOffset,
    int Collision,Vector2I Radius,int Damage,int Health,int Sound,string Animation,string Source);
internal readonly record struct BiggoronSwordParentFrame(int Duration,int Graphic,int Parameter,string Source);
