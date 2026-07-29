using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Declares source state-0 presentation work that must finish while a
/// destination room is preloaded for scrolling. Implementations may initialize
/// graphics, resolve visibility/deletion predicates, and create presentation
/// children, but must not advance ordinary movement, counters, animation, RNG,
/// collision, or scripts.
/// </summary>
internal interface IScreenTransitionPreloadRoomEntity
{
    void PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns);
}
