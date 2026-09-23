using Godot;
using System;

namespace oracleofages;

internal sealed record SomariaBlockEnvironment(
    Action<int> PlaySound,
    Action<SomariaBlock> PushLinkAway,
    Action<SomariaBlock> PublishGrabbable,
    Action<Vector2, int> CreatePuff,
    Action<SomariaBlock>? DropHeld = null,
    Action<int, Vector2, int>? CreateHazardEffect = null);
