using System;
using System.Collections.Generic;

namespace Oracles;

internal static class ValidationCaseRanges
{
    internal static IReadOnlyList<(int First, int Count)> Create(int domain, int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(domain);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        var ranges = new List<(int, int)>();
        for (int first = 0; first < domain; first += Math.Min(size, domain - first))
            ranges.Add((first, Math.Min(size, domain - first)));
        return ranges;
    }

    internal static void SeedTimingWeights(IEnumerable<ValidationScenario> scenarios, Dictionary<string, double> weights)
    {
        // An existing complete profile names the former unsplit scenario.
        // Distribute that cost across its new ranges until measured individual
        // costs exist. Explicit range measurements always take precedence.
        foreach (ValidationScenario scenario in scenarios)
            if (scenario.TimingFamily is { } family && !weights.ContainsKey(scenario.Name) &&
                weights.TryGetValue(family, out double total))
                weights.Add(scenario.Name, total * scenario.TimingFraction);
    }
}
