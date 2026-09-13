using System;

namespace oracleofages;

/// <summary>Receives the authoritative native page index and a live ENEMY-slot lookup.</summary>
internal interface INativeEnemySlotRoomEntity
{
    void BindEnemySlot(int slot, Func<int, IRoomEntity?> resolve);
}
