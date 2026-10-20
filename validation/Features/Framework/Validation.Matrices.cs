using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private string _executingValidationName = "";

    private void RegisterValidationMatrix(Action<int, int> run, int domain, int rangeSize)
    {
        string family = run.Method.Name;
        // Preserve the original exact focused command as one complete-family
        // execution. Full runs register disjoint ranges instead of a duplicate.
        if (_validationFilter == family)
        {
            _registeredValidations.Add(new(family, (Action)(() => run(0, domain)), true));
            return;
        }
        foreach (var (first, count) in ValidationCaseRanges.Create(domain, rangeSize))
        {
            string name = $"{family}Range{first:D2}To{first + count - 1:D2}";
            _registeredValidations.Add(new(name, (Action)(() => run(first, count)), true, family, (double)count / domain));
        }
    }

    private void VerifyValidationMatrixCases(string family, int first, int count, int domain, int cases, int expected)
    {
        FailIf(first < 0 || count < 1 || first + count > domain || cases != expected,
            $"{family} range {first}/{count} of {domain}: executed {cases} cases, expected {expected}.");
        GD.Print($"VALIDATION_CASES name={_executingValidationName} family={family} first={first} count={count} domain={domain} cases={cases}");
    }

    private static void ValidateMatrixRangePlanning()
    {
        foreach (var (domain, size) in new[] { (32, 4), (4, 1), (7, 2), (1, 8) })
        {
            var ranges = ValidationCaseRanges.Create(domain, size);
            int[] coverage = ranges.SelectMany(range => Enumerable.Range(range.First, range.Count)).ToArray();
            FailIf(!coverage.SequenceEqual(Enumerable.Range(0, domain)) ||
                coverage.Distinct().Count() != domain || ranges.Any(range => range.Count < 1 || range.Count > size),
                "Matrix ranges must cover each original domain value once, including a short final range.");
        }
        foreach (var (domain, size) in new[] { (0, 4), (4, 0), (-1, 1) })
        {
            bool rejected = false;
            try { ValidationCaseRanges.Create(domain, size); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            FailIf(!rejected, "Empty or negative matrix domains/ranges must fail before registration.");
        }
        Action body = static () => { };
        ValidationScenario[] scenarios = [new("ValidateRangeA", body, true, "ValidateFamily", .25),
            new("ValidateRangeB", body, true, "ValidateFamily", .75), new("ValidateOther", body, false)];
        var weights = new Dictionary<string, double> { ["ValidateFamily"] = 400, ["ValidateRangeA"] = 50 };
        ValidationCaseRanges.SeedTimingWeights(scenarios, weights);
        FailIf(weights["ValidateRangeA"] != 50 || weights["ValidateRangeB"] != 300 || weights.ContainsKey("ValidateOther"),
            "Historical family cost must seed only missing range weights without overwriting measurements.");
    }
}
