namespace Oracles;

// Reserved ITEM C follows Link and item parents. Its related object retains
// its own native category and update gates.
internal interface IBraceletChildRoomEntity
{
    bool ReservedBraceletChildActive { get; }
    void WriteReservedBraceletAngle(int angle);
    void UpdateBraceletChild(Player player, bool frozen = false);
}
