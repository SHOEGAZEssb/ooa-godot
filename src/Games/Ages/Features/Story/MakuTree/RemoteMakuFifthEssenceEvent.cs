namespace Oracles;
internal sealed class RemoteMakuFifthEssenceEvent(RoomEventContext context)
    :RemoteMakuEntryEvent<RemoteMakuFifthEssenceDatabase>(context,RoomEventSourceData.Shared.MakuFifth);
