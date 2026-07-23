using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace oracleofages;
internal sealed class ValidationTimelineStep(int durationFrames) : IRoomEventTimelineStep
{
    public int DurationFrames { get; } = durationFrames;
    public int Counter { get; set; }
}
