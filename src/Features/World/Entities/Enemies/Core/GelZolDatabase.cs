using System.Collections.Concurrent;

namespace Oracles;

internal sealed class GelZolDatabase
{
    private static readonly ConcurrentDictionary<string,Lazy<GelZolDatabase>> Packs = new(StringComparer.Ordinal);
    internal static GelZolDatabase For(string root) => Packs.GetOrAdd(root,
        static key => new Lazy<GelZolDatabase>(() => new(key))).Value;
    internal GelBehaviorProfile Gel { get; }
    internal ZolBehaviorProfile Zol { get; }

    private GelZolDatabase(string root)
    {
        var table=GeneratedTable.Load(root+"objects/gel_zol.tsv",
            new("Gel/Zol profiles",GeneratedTableKeySemantics.Grouped,
                ["owner","table","index","value-a","value-b","source"],["owner","table"],headerRequired:true));
        EnemyBehaviorValue[] Values(string owner,int count)
        {
            var rows=table.Rows.Where(row=>row.RequiredString(0)==owner).ToArray();
            if(rows.Length!=count)throw new InvalidOperationException($"{table.Path}: {owner} requires {count} ordered profile rows.");
            return rows.Select((row,index)=>row.RequiredString(1)=="state-profile" && row.UnsignedDecimal(2)==index && row.Decimal(4)==0
                ?new EnemyBehaviorValue(row.Decimal(3),row.RequiredString(5))
                :throw row.Invalid(2,$"ordered {owner} profile index {index}")).ToArray();
        }
        if(table.Rows.Count!=23)throw new InvalidOperationException($"{table.Path}: expected 23 native Gel/Zol rows.");
        var gel=Values("gel",8);
        Gel=new(gel[0].Value,gel[1].Value,gel[2].Value,gel[3].Value,gel[4].Value,gel[5].Value,gel[6].Value,gel[7].Value,gel);
        var zol=Values("zol",15);
        Zol=new(zol[0].Value,zol[1].Value,zol[2].Value,zol[3].Value,zol[4].Value,zol[5].Value,zol[6].Value,
            zol[7].Value,zol[8].Value,zol[9].Value,zol[10].Value,zol[11].Value,zol[12].Value,zol[13].Value,zol[14].Value,zol);
    }
}
