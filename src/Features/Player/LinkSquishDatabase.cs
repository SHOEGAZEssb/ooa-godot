using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class LinkSquishDatabase
{
    internal static LinkSquishDatabase Shared { get; } = new();
    private readonly Lookup<int,LinkSquishFrame> _frames = new();
    internal int FlickerCount { get; }
    private LinkSquishDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/metadata/link_squish_frames.tsv",
            new GeneratedTableSchema("Link squish animation",GeneratedTableKeySemantics.Unique,
                ["mode","frame","duration","graphic","parameter","next","offset","oam","source"],
                ["mode","frame"],headerRequired:true));
        Image image = OracleGraphicsCache.LoadImage("res://assets/oracle/gfx/spr_link.png");
        foreach (var row in table.Rows)
        {
            int mode = row.HexByte(0);
            if (mode is not (6 or 7)) throw row.Invalid(0,"squish modes $06/$07");
            var frames = _frames.GetOrAdd(mode);
            if (row.UnsignedDecimal(1) != frames.Count) throw row.Invalid(1,"ordered source frames");
            int offset = Convert.ToInt32(row.RequiredString(6),16);
            frames.Add(new(row.Decimal(2,1,254),row.HexByte(3),row.HexByte(4),row.Decimal(5,0,2),
                NpcCharacter.BuildOamTexture(image,row.RequiredString(7),offset/16,0),row.RequiredString(8)));
        }
        if (_frames.ValuesOrEmpty(6).Count != 3 || _frames.ValuesOrEmpty(7).Count != 3)
            throw new InvalidOperationException("Link squish requires both three-frame loops.");
        var control = GeneratedTable.Load("res://assets/oracle/metadata/link_squish_control.tsv",
            new GeneratedTableSchema("Link squish control",GeneratedTableKeySemantics.Unique,
                ["flicker-count","source"],["flicker-count"],headerRequired:true));
        if (control.Rows.Count != 1) throw new InvalidOperationException("Link squish requires one control record.");
        FlickerCount = control.Rows[0].HexByte(0);
    }
    internal IReadOnlyList<LinkSquishFrame> Frames(bool vertical) => _frames.ValuesOrEmpty(vertical ? 7 : 6);
}

internal readonly record struct LinkSquishFrame(int Duration,int Graphic,int Parameter,int Next,
    Texture2D Texture,string Source);
