namespace oracleofages;

internal sealed class RalphBeforeCurrentsEvent(RoomEventContext context)
    : RalphAfterChevalEvent(context, beforeCurrents: true) { }
