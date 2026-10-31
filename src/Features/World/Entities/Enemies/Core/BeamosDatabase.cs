using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Oracles;

internal sealed class BeamosDatabase
{
    private static readonly ConcurrentDictionary<string, Lazy<BeamosDatabase>> Packs = new(StringComparer.Ordinal);
    internal static BeamosDatabase For(string root) => Packs.GetOrAdd(root,
        static key => new Lazy<BeamosDatabase>(() => new(key))).Value;
    internal BeamosBehaviorProfile Behavior { get; }

    private BeamosDatabase(string root)
    {
        var table = GeneratedTable.Load(root + "metadata/beamos_behavior.tsv",
            new("Beamos and beam behavior", GeneratedTableKeySemantics.Grouped,
                ["owner", "table", "index", "value", "source"], ["owner", "table"], headerRequired: true));
        EnemyBehaviorValue[] Read(string owner, string name, int count)
        {
            var rows = table.Rows.Where(row => row.RequiredString(0) == owner && row.RequiredString(1) == name).ToArray();
            if (rows.Length != count) throw new InvalidOperationException($"{table.Path}: {owner}/{name} requires {count} native operands.");
            return rows.Select((row, index) => row.UnsignedDecimal(2) == index
                ? new EnemyBehaviorValue(row.Decimal(3), row.RequiredString(4))
                : throw row.Invalid(2, "native operand order")).ToArray();
        }
        Behavior = new(Read("beamos", "state-profile", 7), Read("beamos", "angle-to-animation", 32),
            Read("beamos-beam", "state-profile", 3), Read("beamos-beam", "angle-to-animation", 16));
        if (table.Rows.Count != 58) throw new InvalidOperationException($"{table.Path}: unexpected Beamos behavior rows.");
    }
}
