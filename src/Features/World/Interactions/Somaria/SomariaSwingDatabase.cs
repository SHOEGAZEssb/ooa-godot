using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class SomariaSwingDatabase
{
    private readonly Dictionary<int,List<SomariaParentFrame>> _frames = new();
    private readonly int[] _selector = new int[24];
    internal SomariaSwingDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/metadata/somaria_parent_animations.tsv",
            new GeneratedTableSchema("Somaria parent animations", GeneratedTableKeySemantics.Unique,
                ["mode","frame","duration","graphic","parameter","source"],["mode","frame"],headerRequired:true));
        if (table.Rows.Count != 14) throw new InvalidOperationException("Somaria parent animations require14 frames.");
        foreach (var row in table.Rows)
        {
            int mode=row.HexByte(0);
            if (mode is not (0x22 or 0x26 or 0x2d)) throw row.Invalid(0,"Somaria modes$22/$26/$2d");
            if (!_frames.TryGetValue(mode,out var frames)) _frames.Add(mode,frames=new());
            if (row.Decimal(1)!=frames.Count) throw row.Invalid(1,"ordered frames");
            frames.Add(new(row.Decimal(2,1,255),row.HexByte(3),row.HexByte(4))); _=row.RequiredString(5);
        }
        foreach (int mode in new[]{0x22,0x26,0x2d})
            if (!_frames.TryGetValue(mode,out var frames) || frames.Count!=(mode==0x26?4:5) ||
                (frames[^1].Parameter&0x80)==0)
                throw new InvalidOperationException($"Somaria mode${mode:x2} has incomplete parent animation.");
        table=GeneratedTable.Load("res://assets/oracle/metadata/somaria_swing_selector.tsv",
            new GeneratedTableSchema("Somaria swing selector",GeneratedTableKeySemantics.Unique,
                ["index","value","source"],["index"],headerRequired:true));
        if (table.Rows.Count!=24) throw new InvalidOperationException("Somaria swing selector requires24 entries.");
        for(int i=0;i<24;i++)
        {
            var row=table.Rows[i]; if(row.Decimal(0)!=i) throw row.Invalid(0,"ordered selector indices");
            _selector[i]=row.HexByte(1); _=row.RequiredString(2);
        }
    }
    internal IReadOnlyList<SomariaParentFrame> Frames(int mode) => _frames.TryGetValue(mode,out var frames)?frames:
        throw new NotSupportedException($"Somaria parent animation mode${mode:x2} is not imported.");
    internal (int Animation,int Arc) Select(int parameter,int direction)
    {
        if(direction is <0 or >3) throw new ArgumentOutOfRangeException(nameof(direction));
        int index=parameter&0x1f;
        int arcBase=index<0x10?0:0x10;
        if(index<0x10) index=direction*4+(index>>1);
        if(index>=24) throw new NotSupportedException($"Somaria swing parameter${parameter:x2} exceeds native selector.");
        int value=_selector[index]; return(value&7,(value>>4)+arcBase);
    }
}

internal readonly record struct SomariaParentFrame(int Duration,int Graphic,int Parameter);
