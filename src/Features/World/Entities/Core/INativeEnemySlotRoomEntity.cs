
namespace Oracles;

/// <summary>Receives the authoritative native page index and a live ENEMY-slot lookup.</summary>
internal interface INativeEnemySlotRoomEntity : INativeEnemyRoomEntity
{
    void BindEnemySlot(int slot, Func<int, IRoomEntity?> resolve);
    void ReleaseEnemySlot() { }
}
