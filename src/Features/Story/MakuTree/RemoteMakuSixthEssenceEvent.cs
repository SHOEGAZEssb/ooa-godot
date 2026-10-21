namespace oracleofages;

internal sealed class RemoteMakuSixthEssenceEvent(RoomEventContext context)
    : RemoteMakuEntryEvent<RemoteMakuSixthEssenceDatabase>(context,RoomEventSourceData.Shared.MakuSixth);
