using System;

namespace oracleofages;

// collisionEffect24 writes the same relatedObj1 carrier for ordinary drops
// and PART_SMALL_FAIRY. The manager resolves the live physical ITEM slot.
internal interface IAttachableCollectibleRoomEntity
{
    void AttachToItem(Func<ItemDropCarrier?> carrier);
}
