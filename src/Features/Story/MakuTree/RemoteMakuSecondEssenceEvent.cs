namespace oracleofages;

/// <summary>
/// Room-entry owner for past INTERAC_REMOTE_MAKU_CUTSCENE $8a:$01/v$03 in
/// room 1:83 after obtaining the second Essence.
/// </summary>
internal sealed class RemoteMakuSecondEssenceEvent(RoomEventContext context) :
    RemoteMakuEntryEvent<RemoteMakuSecondEssenceDatabase>(context, new RemoteMakuSecondEssenceDatabase())
{
}
