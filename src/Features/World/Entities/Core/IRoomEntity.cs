using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal interface IRoomEntity
{
    Node2D Node { get; }
    void SetTransitionDrawOffset(Vector2 offset);
}
