using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace oracleofages;

internal sealed class PlasmarineProjectileDatabase
{
    private static readonly Lazy<PlasmarineProjectileDatabase> LazyShared=new(()=>new());
    internal static PlasmarineProjectileDatabase Shared=>LazyShared.Value;
    internal EnemyProjectileVisualRecord Visual {get;}
    internal IReadOnlyList<int> Properties {get;}
    private readonly Dictionary<int,(bool Enabled,int Effect)> _collisions=[];
    internal (bool Enabled,int Effect) Collision(int item)=>_collisions[item];
    private PlasmarineProjectileDatabase()
    {
        var table=GeneratedTable.Load("res://assets/oracle/effects/plasmarine_parts.tsv",
            new GeneratedTableSchema("Plasmarine projectile",GeneratedTableKeySemantics.Unique,
                ["id","animation-index","sprite","tile-base","palette","source-grayscale-inverted","animation","part-data","source"],
                ["id","animation-index"],headerRequired:true));
        var rows=table.Rows.ToArray();
        if(rows.Length!=5 || rows.Where((row,index)=>row.HexByte(0)!=0x43 || row.UnsignedDecimal(1)!=index).Any())
            throw new InvalidOperationException("plasmarineProjectile.s: PART$43 requires its two base and three fallthrough animations.");
        var first=rows[0];
        Visual=new([first.RequiredString(2)],first.UnsignedDecimal(3),first.UnsignedDecimal(4),first.Boolean01(5),rows.Select(row=>row.RequiredString(6)).ToArray());
        Properties=first.SplitRequired(7,',').Select(value=>int.Parse(value,CultureInfo.InvariantCulture)).ToArray();
        if(Properties.Count!=8)throw first.Invalid(7,"eight native PART$43 property bytes");
        foreach(var row in rows)_=row.RequiredString(8);
        var collisions=GeneratedTable.Load("res://assets/oracle/effects/plasmarine_part_collisions.tsv",
            new GeneratedTableSchema("Plasmarine projectile collisions",GeneratedTableKeySemantics.Unique,
                ["id","collision","enabled","effect","source"],["id","collision"],headerRequired:true));
        foreach(var row in collisions.Rows)
        {
            if(row.HexByte(0)!=0x43)throw row.Invalid(0,"PART$43");
            _collisions.Add(row.HexByte(1),(row.Boolean01(2),row.HexByte(3)));_=row.RequiredString(4);
        }
        if(_collisions.Count!=32)throw new InvalidOperationException("PART$43 requires all 32 collision columns.");
    }
}
