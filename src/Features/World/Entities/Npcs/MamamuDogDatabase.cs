using System;
using System.Globalization;
using System.Linq;

namespace oracleofages;

internal sealed class MamamuDogDatabase
{
    internal int[] Counters { get; }
    internal string[] Animations { get; }

    internal MamamuDogDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/cutscenes/mamamu_dog.tsv",
            new GeneratedTableSchema("Mamamu indoor dog", GeneratedTableKeySemantics.Unique,
                ["key", "value"], ["key"], headerRequired: true));
        var rows = table.Rows.ToDictionary(row => row.RequiredString(0), row => row.RequiredString(1));
        if (rows.Count != 5 || !rows.ContainsKey("counters") || Enumerable.Range(0, 4).Any(i => !rows.ContainsKey($"animation-{i}")))
            throw new InvalidOperationException("mamamuDog.s:$54:$00 metadata requires counters and four animations.");
        Counters = rows["counters"].Split(',').Select(value => int.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToArray();
        if (Counters.Length != 8 || Counters.Any(value => value is < 1 or > 255))
            throw new InvalidOperationException("mamamuDog_randomCounterValues requires eight nonzero bytes.");
        Animations = Enumerable.Range(0, 4).Select(i => rows[$"animation-{i}"]).ToArray();
    }
}
