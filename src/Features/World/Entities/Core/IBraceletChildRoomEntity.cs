namespace oracleofages;

// The reserved bracelet item runs before enemies; the carried native object's
// own handler still runs in its ordinary object slot later in the update.
internal interface IBraceletChildRoomEntity
{
    void UpdateBraceletChild(Player player);
}
