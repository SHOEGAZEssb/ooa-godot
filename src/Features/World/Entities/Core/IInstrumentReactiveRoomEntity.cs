using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Narrow enemy-phase hook for handlers that explicitly inspect
/// wLinkPlayingInstrument while ordinary objects are disabled by ITEM_HARP.
/// </summary>
internal interface IInstrumentReactiveRoomEntity
{
    void UpdateDuringInstrument(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns);
}
