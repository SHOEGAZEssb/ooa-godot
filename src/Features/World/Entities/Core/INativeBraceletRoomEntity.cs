using System;

namespace oracleofages;

// Native objects publish pickup candidates during their handler. Link reads
// the preceding update's buffer; the final carried-position copy runs last.
internal interface INativeBraceletRoomEntity : IHeldObjectPositionRoomEntity, IBraceletInteractableRoomEntity
{
    void BindGrabbablePublisher(Action<INativeBraceletRoomEntity> publish);
}
