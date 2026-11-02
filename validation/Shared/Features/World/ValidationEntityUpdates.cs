using System.Runtime.CompilerServices;

namespace Oracles;

// Component fixtures supply original-update time explicitly; the production
// entity dispatcher consumes the main thread's byte without owning its clock.
internal static class ValidationEntityUpdates
{
    private static readonly ConditionalWeakTable<RoomEntityManager, ComponentClock> Clocks = new();

    private sealed class ComponentClock
    {
        internal readonly ApplicationFixedUpdateScheduler Scheduler = new();
        internal readonly OracleSaveData Save;

        internal ComponentClock(RoomEntityManager entities)
        {
            Save = entities.SaveData ?? OracleSaveData.CreateStandardGame(entities.RuntimeState.Game);
            // Isolated object fixtures arrange their first native frame directly.
            Save.WriteField("wPlaytimeCounter", checked((byte)entities.FrameCounter));
        }
    }

    internal static void AdvanceEntityFrames(this RoomEntityManager entities, double delta, Player player,
        bool timeWarpArrival = false, bool enemiesDisabled = false, bool specialObjectsUpdated = false,
        Action? updateLink = null)
    {
        if (delta == 0)
        {
            entities.UpdateObjects(0, player, timeWarpArrival, enemiesDisabled);
            return;
        }
        ComponentClock clock = Clocks.GetValue(entities, owner => new(owner));
        clock.Scheduler.Advance(delta, () =>
        {
            MainThreadClock.Advance(clock.Save, entities.RuntimeState);
            if (!specialObjectsUpdated) entities.UpdateSpecialObjectsBeforePlayer(player);
            updateLink?.Invoke();
            entities.UpdateObjects(ApplicationFixedUpdateScheduler.UpdateDelta, player, timeWarpArrival, enemiesDisabled);
        });
    }
}
