using System;

namespace Oracles;

internal sealed record ValidationScenario(string Name, Delegate Run, bool RequiresRom,
    string? TimingFamily = null, double TimingFraction = 1);
