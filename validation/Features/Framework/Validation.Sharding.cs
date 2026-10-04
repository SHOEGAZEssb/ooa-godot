using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private static void ValidateShardPlanning()
    {
        string[] names = ["ValidateA", "ValidateB", "ValidateC", "ValidateD", "ValidateNew"];
        FailIf(!ValidationShardPlanner.Assign(names, 3, null).SequenceEqual(new[] { 0, 1, 2, 0, 1 }),
            "Absent timing history must preserve round-robin coverage.");
        var weights = ValidationShardPlanner.ParseWeights(
            ["name\ttotal_ms", "ValidateA\t100", "ValidateB\t90", "ValidateC\t10", "ValidateD\t20", "ValidateRemoved\t99999"], "fixture");
        // New scenarios use the median of known, currently registered costs;
        // removed entries cannot inflate the fallback or consume a worker.
        int[] plan = ValidationShardPlanner.Assign(names, 2, weights);
        FailIf(!plan.SequenceEqual(new[] { 0, 1, 0, 0, 1 }) ||
            !plan.SequenceEqual(ValidationShardPlanner.Assign(names, 2, weights)),
            "Timing sharding must balance long scenarios with deterministic ties and retain every registration.");
        FailIf(!ValidationShardPlanner.Assign(names, 8, weights).Order().SequenceEqual(new[] { 0, 1, 2, 3, 4 }) ||
            ValidationShardPlanner.Assign(names, 1, weights).Any(worker => worker != 0) ||
            !ValidationShardPlanner.Assign(names, 3, new Dictionary<string, double> { ["ValidateRemoved"] = 1 })
                .SequenceEqual(new[] { 0, 1, 2, 0, 1 }),
            "Empty workers, focused execution, and stale timing profiles must preserve full coverage.");
        foreach (string[] invalid in new[]
        {
            new[] { "wrong header" }, new[] { "name\ttotal_ms", "ValidateA\tNaN" },
            new[] { "name\ttotal_ms", "ValidateA\tInfinity" }, new[] { "name\ttotal_ms", "ValidateA\t-1" },
            new[] { "name\ttotal_ms", "ValidateA\t1,5" },
            new[] { "name\ttotal_ms", "ValidateA\t1", "ValidateA\t2" }
        })
        {
            bool rejected = false;
            try { ValidationShardPlanner.ParseWeights(invalid, "fixture"); }
            catch (InvalidDataException exception) { rejected = exception.Message.StartsWith("fixture:", StringComparison.Ordinal); }
            FailIf(!rejected, "Malformed timing history must fail with its source context.");
        }
    }
}
