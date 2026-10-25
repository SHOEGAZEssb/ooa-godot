namespace Oracles;

// The reserved bracelet item runs before enemies; the carried native object's
// own handler still runs in its ordinary object slot later in the update.
internal interface IBraceletChildRoomEntity
{
    bool ReservedBraceletChildActive { get; }
    void WriteReservedBraceletAngle(int angle);
    void UpdateBraceletChild(Player player);
}
