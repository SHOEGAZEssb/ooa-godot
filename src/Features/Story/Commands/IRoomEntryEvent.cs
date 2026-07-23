using Godot;
using System;

namespace oracleofages;

/// <summary>
/// A room event whose entry selection needs no cross-event coordination.
/// Events are matched and started in the controller's explicit priority order.
/// </summary>
internal interface IRoomEntryEvent : IRoomEvent
{
    bool Matches(int group, OracleRoomData room);
    void Start(OracleRoomData room);
}
