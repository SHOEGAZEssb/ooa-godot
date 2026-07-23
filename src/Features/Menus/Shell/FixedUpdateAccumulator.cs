using Godot;
using System;

namespace oracleofages;

internal sealed class FixedUpdateAccumulator
{
    private double _remainder;

    internal int Consume(double delta)
    {
        if (delta < 0.0)
            throw new ArgumentOutOfRangeException(nameof(delta));
        _remainder += delta * 60.0;
        int updates = (int)Math.Floor(_remainder + 1e-9);
        _remainder -= updates;
        return updates;
    }

    internal void Reset() => _remainder = 0.0;
}
