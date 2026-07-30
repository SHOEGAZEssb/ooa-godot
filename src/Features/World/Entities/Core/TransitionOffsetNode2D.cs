using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Centralizes the presentation-only screen-scroll offset used by drawable
/// room entities. Logical room/world positions remain unchanged.
/// </summary>
public abstract partial class TransitionOffsetNode2D : Node2D
{
    private Func<Vector2, Vector2> _worldToScreen = static position => position;

    public Vector2 TransitionDrawOffset { get; private set; }
    internal Vector2 SourceOamWrapOffset =>
        OracleObjectMath.SourceOamWrapOffset(_worldToScreen(Position));
    protected Vector2 SourceOamDrawOffset =>
        TransitionDrawOffset + SourceOamWrapOffset;

    internal void SetWorldToScreen(Func<Vector2, Vector2> worldToScreen)
    {
        _worldToScreen = worldToScreen ??
            throw new ArgumentNullException(nameof(worldToScreen));
        QueueRedraw();
    }

    internal void SetTransitionDrawOffset(Vector2 offset)
    {
        if (TransitionDrawOffset.IsEqualApprox(offset))
            return;
        TransitionDrawOffset = offset;
        QueueRedraw();
    }
}
