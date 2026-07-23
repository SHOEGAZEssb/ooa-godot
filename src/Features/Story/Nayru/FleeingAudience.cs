using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed class FleeingAudience(NpcCharacter actor, FleeRecord record, Vector2 velocity)
{
    public NpcCharacter Actor { get; } = actor;
    public FleeRecord Record { get; } = record;
    public Vector2 Velocity { get; } = velocity;
    public int Delay { get; set; } = record.Delay;

    public int ZFixed;
    public int SpeedZ = record.WaitJumpSpeedZ;
    public bool Escaping { get; set; }
}
