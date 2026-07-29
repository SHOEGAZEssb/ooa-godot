using System;

namespace oracleofages;

/// <summary>
/// Converts host presentation time into complete original-engine updates.
/// Every consumer is advanced from the callback so subsystems cannot catch up
/// in separate batches.
/// </summary>
internal sealed class ApplicationFixedUpdateScheduler
{
    internal const int UpdatesPerSecond = 60;
    internal const double UpdateDelta = 1.0 / UpdatesPerSecond;

    private double _remainder;

    internal long UpdateCount { get; private set; }
    internal double Remainder => _remainder;

    internal int Advance(double delta, Action update)
    {
        ArgumentNullException.ThrowIfNull(update);
        if (!double.IsFinite(delta) || delta < 0.0)
            throw new ArgumentOutOfRangeException(nameof(delta));

        _remainder += delta * UpdatesPerSecond;
        int updates = (int)Math.Floor(_remainder + 1e-9);
        _remainder -= updates;
        for (int index = 0; index < updates; index++)
        {
            UpdateCount++;
            update();
        }
        return updates;
    }

    internal void Reset()
    {
        _remainder = 0.0;
        UpdateCount = 0;
    }
}
