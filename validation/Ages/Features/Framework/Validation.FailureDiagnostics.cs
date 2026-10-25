using System;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void ValidateFailureDiagnostics()
    {
        int evaluations = 0;
        FailIf(false, $"Unused diagnostic {++evaluations}.");
        FailIf(evaluations != 0, "Passing validation interpolated a failure-only diagnostic.");
        string? diagnostic = null;
        try { FailIf(true, $"Room ${0x4a:x2}, column {12,4}, probe {++evaluations}."); }
        catch (InvalidOperationException exception) { diagnostic = exception.Message; }
        FailIf(diagnostic != "Room $4a, column   12, probe 1." || evaluations != 1,
            "Failure diagnostics must still throw and preserve hexadecimal IDs, alignment and evaluation exactly once.");
    }
}
