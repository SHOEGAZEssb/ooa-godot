namespace oracleofages;

/// <summary>
/// Owns dynamic INTERAC_REMOTE_MAKU_CUTSCENE $8a:$00/v$04 after the post-D3
/// controller returns Link to room 0:ba.
/// </summary>
internal sealed class RemoteMakuThirdEssenceEvent : RemoteMakuEvent
{
    private readonly RemoteMakuThirdEssenceDatabase _database;
    private bool _prepared;

    internal RemoteMakuThirdEssenceEvent(RoomEventContext context)
        : this(context, new RemoteMakuThirdEssenceDatabase())
    {
    }

    private RemoteMakuThirdEssenceEvent(
        RoomEventContext context,
        RemoteMakuThirdEssenceDatabase database)
        : base(context, database)
    {
        _database = database;
    }

    internal RemoteMakuThirdEssenceDatabase Database => _database;
    internal bool Prepared => _prepared;

    internal bool PrepareAfterPostD3(
        int pastFlagGroup,
        int pastFlagRoom,
        int pastRoomFlag,
        int standardGlobalFlag)
    {
        RemoteMakuEventRecord record = _database.Record;
        OracleSaveData save = Context.Rooms.SaveData;
        if (Context.Rooms.ActiveGroup != record.Group ||
            Context.Rooms.CurrentRoom.Id != record.Room ||
            (save.ReadWramByte(0xc6bf) & record.EssenceMask) == 0 ||
            save.HasRoomFlag(record.Group, record.Room, (byte)record.RoomFlag))
        {
            return false;
        }

        // @val04 performs these writes during the dynamic interaction's
        // state-0 update, while the return room is still hidden by white.
        save.SetRoomFlag(
            pastFlagGroup, pastFlagRoom, checked((byte)pastRoomFlag));
        if (!save.IsLinkedGame)
            save.SetGlobalFlag(standardGlobalFlag);
        _prepared = true;
        return true;
    }

    internal void StartPrepared()
    {
        if (!_prepared)
        {
            throw new System.InvalidOperationException(
                "Room 0:ba remote Maku event was not initialized by the " +
                "post-D3 controller.");
        }
        _prepared = false;
        Begin();
    }

    public override void Cancel()
    {
        _prepared = false;
        base.Cancel();
    }
}
