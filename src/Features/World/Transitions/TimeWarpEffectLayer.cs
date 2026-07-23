using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

internal sealed partial class TimeWarpEffectLayer : Node2D
{
    internal Action<TimeWarpEffectLayer>? DrawContents { get; set; }

    public override void _Draw() => DrawContents?.Invoke(this);
}
