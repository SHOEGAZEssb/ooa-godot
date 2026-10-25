namespace Oracles;

// Native enabled bit 1 preserves a held allocation through scrolling.
internal interface IScrollPersistentRoomEntity : IRoomEntity
{
    bool PersistsAcrossScroll { get; }
    void EnterRoom(OracleRoomData room);
}
