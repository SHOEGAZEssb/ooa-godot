using System;

namespace oracleofages;

/// <summary>
/// Room-entry owner for present-day INTERAC_REMOTE_MAKU_CUTSCENE
/// $8a:$00/v$02 in room 0:3a after obtaining the Harp of Ages.
/// </summary>
internal sealed class RemoteMakuHarpEvent :
    RemoteMakuEvent,
    IRoomEntryEvent
{
    private readonly RemoteMakuHarpDatabase _database;

    internal RemoteMakuHarpEvent(RoomEventContext context)
        : this(context, new RemoteMakuHarpDatabase())
    {
    }

    private RemoteMakuHarpEvent(
        RoomEventContext context,
        RemoteMakuHarpDatabase database)
        : base(context, database)
    {
        _database = database;
    }

    internal RemoteMakuHarpDatabase Database => _database;

    public bool Matches(int group, OracleRoomData room)
    {
        RemoteMakuEventRecord record = _database.Record;
        OracleSaveData save = Context.Rooms.SaveData;
        return group == record.Group &&
            room.Id == record.Room &&
            save.HasTreasure(record.RequiredTreasure) &&
            !save.HasRoomFlag(
                record.Group,
                record.Room,
                (byte)record.RoomFlag);
    }

    public void Start(OracleRoomData room)
    {
        if (!Matches(Context.Rooms.ActiveGroup, room))
        {
            throw new InvalidOperationException(
                $"Room {Context.Rooms.ActiveGroup:x}:{room.Id:x2} cannot " +
                "start the post-Harp remote Maku event.");
        }
        Begin();
    }
}
