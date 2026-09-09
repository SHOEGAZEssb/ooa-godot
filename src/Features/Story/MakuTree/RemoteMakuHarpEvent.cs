namespace oracleofages;

/// <summary>
/// Room-entry owner for present-day INTERAC_REMOTE_MAKU_CUTSCENE
/// $8a:$00/v$02 in room 0:3a after obtaining the Harp of Ages.
/// </summary>
internal sealed class RemoteMakuHarpEvent(RoomEventContext context) :
    RemoteMakuEntryEvent<RemoteMakuHarpDatabase>(context, new RemoteMakuHarpDatabase())
{
    public override bool Matches(int group, OracleRoomData room)
    {
        RemoteMakuEventRecord record = Database.Record;
        OracleSaveData save = Context.Rooms.SaveData;
        return group == record.Group &&
            room.Id == record.Room &&
            save.HasTreasure(record.RequiredTreasure) &&
            !save.HasRoomFlag(
                record.Group,
                record.Room,
                (byte)record.RoomFlag);
    }
}
