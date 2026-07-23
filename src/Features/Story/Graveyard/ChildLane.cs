using Godot;
using System;

namespace oracleofages;
internal sealed class ChildLane(ChildRole role, NpcCharacter actor)
{
    public ChildRole Role { get; } = role;
    public NpcCharacter Actor { get; } = actor;
    public ChildStage Stage { get; set; }
    public int Counter { get; set; }
    public int DialogueIndex { get; set; } = -1;

    public int ZFixed;
    public int SpeedZ;
    public int BaseX { get; } = Mathf.FloorToInt(actor.Position.X);
    public Vector2 PrecisePosition { get; set; } = actor.Position;
}
