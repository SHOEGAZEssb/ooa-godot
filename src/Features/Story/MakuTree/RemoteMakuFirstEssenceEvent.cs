using System;

namespace oracleofages;

/// <summary>
/// Room-entry owner for present-day INTERAC_REMOTE_MAKU_CUTSCENE
/// $8a:$00/v$00 in room 0:8d after the first Essence.
/// </summary>
internal sealed class RemoteMakuFirstEssenceEvent :
    RemoteMakuEvent,
    IRoomEntryEvent
{
    private readonly RemoteMakuFirstEssenceDatabase _database;

    internal RemoteMakuFirstEssenceEvent(RoomEventContext context)
        : this(context, new RemoteMakuFirstEssenceDatabase())
    {
    }

    private RemoteMakuFirstEssenceEvent(
        RoomEventContext context,
        RemoteMakuFirstEssenceDatabase database)
        : base(context, database)
    {
        _database = database;
    }

    internal RemoteMakuFirstEssenceDatabase Database => _database;

    public bool Matches(int group, OracleRoomData room) =>
        MatchesEssenceRoom(group, room);

    public void Start(OracleRoomData room)
    {
        if (!Matches(Context.Rooms.ActiveGroup, room))
        {
            throw new InvalidOperationException(
                $"Room {Context.Rooms.ActiveGroup:x}:{room.Id:x2} cannot " +
                "start the first-Essence remote Maku event.");
        }
        Begin();
    }
}
