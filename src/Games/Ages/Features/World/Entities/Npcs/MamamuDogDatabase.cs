using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Oracles;

internal sealed class MamamuDogDatabase
{
    internal int[] Counters { get; }
    internal string[] Animations { get; }
    internal int[] RoamingMap0 { get; }
    internal string RoamingText { get; }

    internal MamamuDogDatabase()
    {
        var table = GeneratedTable.Load(AgesGameContent.AssetRoot + "cutscenes/mamamu_dog.tsv",
            new GeneratedTableSchema("Mamamu indoor dog", GeneratedTableKeySemantics.Unique,
                ["key", "value"], ["key"], headerRequired: true));
        var rows = table.Rows.ToDictionary(row => row.RequiredString(0), row => row.RequiredString(1));
        if (rows.Count != 7 || !rows.ContainsKey("counters") || !rows.ContainsKey("roaming-map0") || !rows.ContainsKey("roaming-text") || Enumerable.Range(0, 4).Any(i => !rows.ContainsKey($"animation-{i}")))
            throw new InvalidOperationException("mamamuDog.s:$54 metadata requires counters, roaming waypoints and four animations.");
        Counters = rows["counters"].Split(',').Select(value => int.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToArray();
        if (Counters.Length != 8 || Counters.Any(value => value is < 1 or > 255))
            throw new InvalidOperationException("mamamuDog_randomCounterValues requires eight nonzero bytes.");
        Animations = Enumerable.Range(0, 4).Select(i => rows[$"animation-{i}"]).ToArray();
        RoamingMap0 = rows["roaming-map0"].Split(',').Select(value => int.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToArray();
        if (RoamingMap0.Length != 14)
            throw new InvalidOperationException("mamamuDog.s:$54:$01 map0 requires seven YX points.");
        RoamingText=Encoding.UTF8.GetString(Convert.FromBase64String(rows["roaming-text"]));
    }
}
