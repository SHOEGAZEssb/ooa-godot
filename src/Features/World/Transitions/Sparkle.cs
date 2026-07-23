using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;
internal sealed class Sparkle
{
    public readonly Vector2 Position;
    public readonly Animator Animator;
    public Sparkle(Vector2 position, Animator animator)
    {
        Position = position;
        Animator = animator;
    }
}
