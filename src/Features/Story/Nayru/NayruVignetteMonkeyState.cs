using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed class NayruVignetteMonkeyState(NpcCharacter actor, VignetteMonkeyRecord record, int startupDelay, int jumpSpeedZ)
{
    public NpcCharacter Actor { get; } = actor;
    public VignetteMonkeyRecord Record { get; } = record;
    public int StartupDelay { get; } = startupDelay;
    public int JumpSpeedZ { get; } = jumpSpeedZ;

    public int ZFixed;
    public int SpeedZ = jumpSpeedZ;
    public int MovementPhase { get; set; }
    public int MovementCounter { get; set; }
    public int HopCount { get; set; }
    public int Direction { get; set; } = 1;
    public int Animation { get; set; } = record.Animation;
    public bool Stone { get; set; }
}
