using Godot;

namespace oracleofages;

/// <summary>
/// Centralizes the presentation-only screen-scroll offset used by drawable
/// room entities. Logical room/world positions remain unchanged.
/// </summary>
public abstract partial class TransitionOffsetNode2D : Node2D
{
    public Vector2 TransitionDrawOffset { get; private set; }

    internal void SetTransitionDrawOffset(Vector2 offset)
    {
        if (TransitionDrawOffset.IsEqualApprox(offset))
            return;
        TransitionDrawOffset = offset;
        QueueRedraw();
    }
}
