namespace Oracles;

internal sealed class RemoteMakuEighthEssenceEvent(RoomEventContext context)
    : RemoteMakuEntryEvent<RemoteMakuEighthEssenceDatabase>(context, RoomEventSourceData.Shared.MakuEighth);
