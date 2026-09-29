using System.Runtime.CompilerServices;

namespace oracleofages;

// Bulk elapsed-time stepping belongs to the validation harness. Production
// room events receive one update from the application scheduler.
internal static class ValidationRoomEventStepping
{
    private static readonly ConditionalWeakTable<RoomEventController, ApplicationFixedUpdateScheduler>
        Clocks = new();

    internal static void Update(this RoomEventController events, double delta) =>
        Clocks.GetOrCreateValue(events).Advance(delta, () =>
        {
            if (events.ObjectUpdateSuspended) events.ResumeObjectUpdate();
            else
            {
                events.UpdateSpecialObjects();
                events.UpdateFrame();
            }
        });

    internal static void UpdateDuringTimeWarp(this RoomEventController events, double delta) =>
        Clocks.GetOrCreateValue(events).Advance(delta, events.UpdateDuringTimeWarpFrame);
}
