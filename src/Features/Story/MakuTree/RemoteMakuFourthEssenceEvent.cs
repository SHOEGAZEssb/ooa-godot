namespace oracleofages;

internal sealed class RemoteMakuFourthEssenceEvent(RoomEventContext context) :
    RemoteMakuEntryEvent<RemoteMakuFourthEssenceDatabase>(context, new RemoteMakuFourthEssenceDatabase());
