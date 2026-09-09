using System;

namespace oracleofages;

/// <summary>Room-entry lifecycle for placed INTERAC_REMOTE_MAKU_CUTSCENE $8a variants.</summary>
internal abstract class RemoteMakuEntryEvent<TDatabase>(
    RoomEventContext context, TDatabase database) : RemoteMakuEvent(context, database), IRoomEntryEvent
    where TDatabase : RemoteMakuEventDatabase
{
    internal TDatabase Database { get; } = database;

    public virtual bool Matches(int group, OracleRoomData room) => MatchesEssenceRoom(group, room);

    public void Start(OracleRoomData room)
    {
        if (!Matches(Context.Rooms.ActiveGroup, room))
            throw new InvalidOperationException(
                $"Room {Context.Rooms.ActiveGroup:x}:{room.Id:x2} cannot start " +
                $"INTERAC_REMOTE_MAKU_CUTSCENE ${Record.InteractionId:x2}:${Record.SubId:x2}" +
                $"/v${Record.Var03:x2} ({GetType().Name}).");
        Begin();
    }
}
