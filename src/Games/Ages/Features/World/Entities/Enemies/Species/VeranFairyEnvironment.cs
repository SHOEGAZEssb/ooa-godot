using Godot;
using System;

namespace Oracles;

internal sealed record VeranFairyEnvironment(VeranFairyDatabase Data,RoomEntityManager Entities,
    RoomTransitionController Transitions,Action<int,string> ShowText,Action<Vector2> Lightning,
    Action<Vector2,int> Explosion,Action EscapeAttempt,Action<IRoomEntity> Own);
