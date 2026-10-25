using System.Collections.Generic;

namespace Oracles;

/// <summary>Linked children occupying later slots in the interaction pass.</summary>
internal interface IRoomInteractionChildSource
{
    void UpdateChildren(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns);
}
