namespace oracleofages;

/// <summary>
/// Owner for INTERAC_REMOTE_MAKU_CUTSCENE $8a:$00/v$01 allocated by
/// objectData7e69 after room 0:83's Wing Dungeon collapse.
/// </summary>
internal sealed class RemoteMakuWingDungeonEvent : RemoteMakuEvent
{
    private readonly RemoteMakuWingDungeonDatabase _database;

    internal RemoteMakuWingDungeonEvent(RoomEventContext context)
        : this(context, new RemoteMakuWingDungeonDatabase())
    {
    }

    private RemoteMakuWingDungeonEvent(
        RoomEventContext context,
        RemoteMakuWingDungeonDatabase database)
        : base(context, database)
    {
        _database = database;
    }

    internal RemoteMakuWingDungeonDatabase Database => _database;

    internal bool StartWarning()
    {
        RemoteMakuEventRecord record = _database.Record;
        if (Context.Rooms.ActiveGroup != record.Group ||
            Context.Rooms.CurrentRoom.Id != record.Room ||
            Context.Rooms.SaveData.HasRoomFlag(
                record.Group,
                record.Room,
                (byte)record.RoomFlag))
        {
            return false;
        }

        Begin();
        return true;
    }
}
