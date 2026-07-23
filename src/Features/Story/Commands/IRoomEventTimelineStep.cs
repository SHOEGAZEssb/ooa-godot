using System;
using System.Collections.Generic;

namespace oracleofages;

internal interface IRoomEventTimelineStep
{
    int DurationFrames { get; }
    int Counter { get; set; }
}
