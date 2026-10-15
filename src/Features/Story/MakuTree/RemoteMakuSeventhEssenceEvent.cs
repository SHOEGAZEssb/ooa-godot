using System;

namespace oracleofages;

/// <summary>Dynamic $8a:$00/v$09 after CUTSCENE_TURN_TO_STONE returns to $2:$90.</summary>
internal sealed class RemoteMakuSeventhEssenceEvent(RoomEventContext context)
    : RemoteMakuEvent(context, new RemoteMakuSeventhEssenceDatabase())
{
    public override bool OwnsUpdatesOf(IRoomEvent other) => other is JabuExteriorEvent;
    internal void StartAfterStoneScene()
    {
        if (!MatchesEssenceRoom(Context.Rooms.ActiveGroup, Context.Rooms.CurrentRoom))
            throw new InvalidOperationException("Post-D7 dynamic remote Maku requires incomplete room $2:$90 with seventh essence bit $40.");
        Begin();
    }
}
