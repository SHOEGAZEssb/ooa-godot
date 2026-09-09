using Godot;
using System;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private static void ValidateRoomEventScheduling()
    {
        var child = new ValidationRoomEvent();
        var parent = new ValidationRoomEvent(child);
        FailIf(
            RoomEventController.SelectUpdateOwner([parent, child], 0, 0x39) != parent ||
            RoomEventController.SelectUpdateOwner([child, parent], 0, 0x39) != parent,
            "Composite room events must own child dispatch regardless of entry precedence.");

        var competing = new ValidationRoomEvent();
        foreach (IRoomEvent[] events in new IRoomEvent[][]
                 { [parent, child, competing], [competing, parent, child] })
        {
            bool rejected = false;
            try
            {
                RoomEventController.SelectUpdateOwner(events, 0, 0x39);
            }
            catch (InvalidOperationException error)
            {
                rejected = error.Message.Contains("0:39") &&
                    error.Message.Contains("competing event update owners");
            }
            FailIf(!rejected,
                "A competing input owner was silently starved by room-event priority.");
        }

        parent.Cancel();
        FailIf(RoomEventController.SelectUpdateOwner([parent, child], 0, 0x39) != child,
            "A completed composite event retained its child's update ownership.");
        child.Cancel();
        FailIf(RoomEventController.SelectUpdateOwner([parent, child], 0, 0x39) is not null,
            "Cancelled room events retained update ownership.");
        GD.Print("Validated explicit composite room-event ownership, handoff, " +
            "and diagnostics for competing input owners.");
    }
}
