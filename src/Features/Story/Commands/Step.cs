using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed class Step(int durationFrames, Func<Step, bool> update) : IRoomEventTimelineStep
{
    public int DurationFrames { get; } = durationFrames;
    public int Counter { get; set; }
    public Func<Step, bool> Update { get; } = update;
}
