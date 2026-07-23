using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;
internal sealed class Particle
{
    public Vector2 PrecisePosition;
    public readonly int SpeedFixed;
    public readonly int SubId;
    public Particle(Vector2 position, int speedFixed, int subId)
    {
        PrecisePosition = position;
        SpeedFixed = speedFixed;
        SubId = subId;
    }
}
