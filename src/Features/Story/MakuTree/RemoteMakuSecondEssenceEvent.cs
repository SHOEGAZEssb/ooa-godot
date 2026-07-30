using System;

namespace oracleofages;

/// <summary>
/// Room-entry owner for past INTERAC_REMOTE_MAKU_CUTSCENE $8a:$01/v$03 in
/// room 1:83 after obtaining the second Essence.
/// </summary>
internal sealed class RemoteMakuSecondEssenceEvent :
    RemoteMakuEvent,
    IRoomEntryEvent
{
    private readonly RemoteMakuSecondEssenceDatabase _database;

    internal RemoteMakuSecondEssenceEvent(RoomEventContext context)
        : this(context, new RemoteMakuSecondEssenceDatabase())
    {
    }

    private RemoteMakuSecondEssenceEvent(
        RoomEventContext context,
        RemoteMakuSecondEssenceDatabase database)
        : base(context, database)
    {
        _database = database;
    }

    internal RemoteMakuSecondEssenceDatabase Database => _database;

    public bool Matches(int group, OracleRoomData room)
    {
        RemoteMakuEventRecord record = _database.Record;
        OracleSaveData save = Context.Rooms.SaveData;
        return group == record.Group &&
            room.Id == record.Room &&
            (save.ReadWramByte(0xc6bf) & record.EssenceMask) != 0 &&
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
                "start the second-Essence remote Maku event.");
        }
        Begin();
    }
}
