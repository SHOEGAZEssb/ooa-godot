using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal interface IRoomEntity
{
    // Presentation is optional. Native allocation, update eligibility and
    // lifetime belong to the entity even when it has no scene-tree node.
    Node2D? PresentationNode => Node;
    // Visual-only callers require a presenter. Controllers implement the
    // optional boundary directly; a visual operation on one is a contract error.
    Node2D Node => throw new InvalidOperationException($"{GetType().Name} has no presentation node.");
    void SetTransitionDrawOffset(Vector2 offset);
}
