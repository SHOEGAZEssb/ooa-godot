using Godot;
using System;

namespace Oracles;

internal sealed record SomariaBlockEnvironment(
    Action<int> PlaySound,
    Action<SomariaBlock> PushLinkAway,
    Action<SomariaBlock> PublishGrabbable,
    Action<Vector2, int> CreatePuff,
    Action<SomariaBlock>? DropHeld = null,
    Action<int, Vector2, int>? CreateHazardEffect = null);
