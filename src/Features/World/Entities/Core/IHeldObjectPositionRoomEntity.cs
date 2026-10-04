namespace oracleofages;

// updateHeldGrabbableObjectPosition follows all object handlers, so carried
// children follow Link's final position even when their parent was frozen.
internal interface IHeldObjectPositionRoomEntity : IRoomEntity
{
    void UpdateHeldPosition(Player player);
}
