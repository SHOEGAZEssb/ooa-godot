using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class BlueStalfosProfile
{
    private static readonly Lazy<BlueStalfosProfile> LazyShared = new(() => new());
    internal static BlueStalfosProfile Shared => LazyShared.Value;
    internal IReadOnlyList<int> State { get; }
    internal IReadOnlyList<int> Quadrants { get; }
    internal IReadOnlyList<int> Targets { get; }
    internal IReadOnlyList<int> SickleBoxes { get; }
    internal IReadOnlyList<int> BabySpeeds { get; }
    internal IReadOnlyList<int> PartState { get; }
    internal IReadOnlyList<int> PartAngles { get; }
    internal IReadOnlyList<int> PartBoundaryOffsets { get; }
    internal int PartPattern { get; }
    private readonly Dictionary<int, IReadOnlyList<int>> _effects = new();
    private readonly IReadOnlyList<int> _active;
    private BlueStalfosProfile()
    {
        var table = GeneratedTable.Load("res://assets/oracle/enemies/blue_stalfos.tsv",
            new GeneratedTableSchema("Blue Stalfos native tables", GeneratedTableKeySemantics.Grouped,
                ["profile", "index", "value", "source"], ["profile"], headerRequired: true));
        var profiles = new Dictionary<string, List<int>>();
        foreach (var row in table.Rows)
        {
            string profile = row.RequiredString(0);
            if (!profiles.TryGetValue(profile, out var values)) profiles.Add(profile, values = []);
            if (row.UnsignedDecimal(1) != values.Count) throw row.Invalid(1, $"contiguous {profile} indices");
            values.Add(row.Decimal(2)); _ = row.RequiredString(3);
        }
        IReadOnlyList<int> Take(string profile, int count)
        {
            if (!profiles.Remove(profile, out var values) || values.Count != count)
                throw new InvalidOperationException($"blueStalfos.s: {profile} requires {count} source values.");
            return values;
        }
        State = Take("state", 25); Quadrants = Take("quadrants", 16); Targets = Take("targets", 8);
        SickleBoxes = Take("sickle-boxes", 36); BabySpeeds = Take("baby-speeds", 4);
        PartState = Take("part-state", 13); PartPattern = Take("part-pattern", 1)[0];
        PartAngles = Take("part-angles", 7); PartBoundaryOffsets = Take("part-boundary-offsets", 16);
        foreach (int mode in new[] { 0x48, 0x65, 0x66, 0x77, 4 }) _effects.Add(mode, Take($"effects-{mode:x2}", 32));
        _active = Take("active-77", 32);
        if (profiles.Count != 0) throw new InvalidOperationException("blueStalfos.s: unconsumed source tables.");
    }
    internal int Effect(int mode, int item) => _effects.TryGetValue(mode, out var effects)
        ? effects[item] : throw new NotSupportedException($"blueStalfos.s: collision mode${mode:x2}.");
    internal bool Active(int item) => _active[item] != 0;
}
