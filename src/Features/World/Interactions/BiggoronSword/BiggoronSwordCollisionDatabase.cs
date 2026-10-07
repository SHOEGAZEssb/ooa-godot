using System;

namespace oracleofages;

internal sealed class BiggoronSwordCollisionDatabase
{
    internal static BiggoronSwordCollisionDatabase Shared { get; }=new();
    private readonly int[] _effects=new int[0x7d];
    private readonly bool[] _enemies=new bool[128],_parts=new bool[0x5a];
    private BiggoronSwordCollisionDatabase()
    {
        var table=GeneratedTable.Load("res://assets/oracle/metadata/biggoron_sword_collision_effects.tsv",
            new GeneratedTableSchema("ITEMCOLLISION_BIGGORON_SWORD $07",GeneratedTableKeySemantics.Unique,
                ["mode","effect","source"],["mode"],headerRequired:true));
        if(table.Rows.Count!=_effects.Length) throw new InvalidOperationException("ITEM$0c requires $7d collision modes.");
        for(int i=0;i<_effects.Length;i++)
        {
            var row=table.Rows[i];
            if(row.HexByte(0)!=i) throw row.Invalid(0,"ordered collision modes");
            _effects[i]=row.HexByte(1); _=row.RequiredString(2);
        }
        LoadMasks("enemy",_enemies); LoadMasks("part",_parts);
    }
    private static void LoadMasks(string kind,bool[] masks)
    {
        var table=GeneratedTable.Load($"res://assets/oracle/metadata/biggoron_sword_{kind}_collisions.tsv",
            new GeneratedTableSchema($"ITEM$0c {kind} masks",GeneratedTableKeySemantics.Unique,
                ["id","enabled","source"],["id"],headerRequired:true));
        if(table.Rows.Count!=masks.Length) throw new InvalidOperationException($"Incomplete ITEM$0c {kind} masks.");
        for(int i=0;i<masks.Length;i++)
        {
            var row=table.Rows[i];
            if(row.HexByte(0)!=i) throw row.Invalid(0,"ordered collision types");
            masks[i]=row.Decimal(1,0,1)!=0; _=row.RequiredString(2);
        }
    }
    internal bool EnemyEnabled(int type) => type>=0 && _enemies[type&0x7f];
    internal bool PartEnabled(int type) => type is >=0 and <0x5a && _parts[type];
    internal int Effect(int mode) => (mode&0x7f)<_effects.Length?_effects[mode&0x7f]:
        throw new NotSupportedException($"objectCollisionTable has no ITEM$0c mode ${(mode&0x7f):x2}.");
}
