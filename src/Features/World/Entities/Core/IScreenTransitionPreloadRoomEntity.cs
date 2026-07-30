using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Declares source state-0 work that must finish while a destination room is
/// preloaded for scrolling. The original enemy and interaction dispatchers
/// continue updating state 0 while scrolling, then freeze later states.
/// Implementations must preserve any state-0 graphics, visibility/deletion,
/// child creation, counter, and RNG effects without advancing state-8+
/// movement, animation, collision, or scripts.
/// </summary>
internal interface IScreenTransitionPreloadRoomEntity
{
    ScreenTransitionPresentation PrepareForScreenTransition(
        ICollection<RoomEntitySpawn> spawns);
}

/// <summary>
/// The resolved drawing state after an incoming object's source state 0.
/// Hidden is valid only when state 0 intentionally leaves no drawable object.
/// </summary>
internal enum ScreenTransitionPresentation
{
    Visible,
    Hidden
}
