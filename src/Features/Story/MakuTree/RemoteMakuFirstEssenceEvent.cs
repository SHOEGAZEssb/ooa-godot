namespace oracleofages;

/// <summary>
/// Room-entry owner for present-day INTERAC_REMOTE_MAKU_CUTSCENE
/// $8a:$00/v$00 in room 0:8d after the first Essence.
/// </summary>
internal sealed class RemoteMakuFirstEssenceEvent(RoomEventContext context) :
    RemoteMakuEntryEvent<RemoteMakuFirstEssenceDatabase>(context, new RemoteMakuFirstEssenceDatabase())
{
}
