using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace oracleofages;

internal static class ValidationShardPlanner
{
    internal static Dictionary<string, double> ParseWeights(IEnumerable<string> lines, string context)
    {
        var weights = new Dictionary<string, double>(StringComparer.Ordinal);
        int lineNumber = 0;
        foreach (string line in lines)
        {
            lineNumber++;
            if (lineNumber == 1 && line == "name\ttotal_ms") continue;
            string[] cells = line.Split('\t');
            if (lineNumber == 1 || cells.Length != 2 || !cells[0].StartsWith("Validate", StringComparison.Ordinal) ||
                !double.TryParse(cells[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double milliseconds) ||
                !double.IsFinite(milliseconds) || milliseconds < 0 || !weights.TryAdd(cells[0], milliseconds))
                throw new InvalidDataException($"{context}:{lineNumber}: expected a unique validation name and finite nonnegative total_ms.");
        }
        if (lineNumber == 0) throw new InvalidDataException($"{context}: missing name/total_ms header.");
        return weights;
    }

    internal static int[] Assign(IReadOnlyList<string> names, int workers, IReadOnlyDictionary<string, double>? weights)
    {
        if (workers < 1) throw new ArgumentOutOfRangeException(nameof(workers));
        var assignments = new int[names.Count];
        if (weights is null || weights.Count == 0)
        {
            for (int index = 0; index < names.Count; index++) assignments[index] = index % workers;
            return assignments;
        }
        double[] known = names.Where(weights.ContainsKey).Select(name => weights[name]).Order().ToArray();
        if (known.Length == 0) return Assign(names, workers, null);
        double fallback = Math.Max(0.001, known[known.Length / 2]);
        double Cost(int index) => weights.TryGetValue(names[index], out double value) ? Math.Max(0.001, value) : fallback;
        var loads = new double[workers];
        // Assign long scenarios first, then execute each worker's selections in
        // registration order. Equal costs and worker loads have stable ties.
        foreach (int index in Enumerable.Range(0, names.Count).OrderByDescending(Cost).ThenBy(index => index))
        {
            int worker = 0;
            for (int candidate = 1; candidate < workers; candidate++)
                if (loads[candidate] < loads[worker]) worker = candidate;
            assignments[index] = worker;
            loads[worker] += Cost(index);
        }
        return assignments;
    }
}
