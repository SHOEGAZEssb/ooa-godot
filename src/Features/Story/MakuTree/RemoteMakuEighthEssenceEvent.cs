namespace oracleofages;

internal sealed class RemoteMakuEighthEssenceEvent(RoomEventContext context)
    : RemoteMakuEntryEvent<RemoteMakuEighthEssenceDatabase>(context, RoomEventSourceData.Shared.MakuEighth);
