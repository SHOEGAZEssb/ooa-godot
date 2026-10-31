using System.Runtime.CompilerServices;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static readonly ConditionalWeakTable<AgesRoomTransitionController, ApplicationFixedUpdateScheduler> WarpComponentUpdates = new();

    private void UpdateRoomWarpTransition(double delta)
    {
        WarpComponentUpdates.GetValue(_transitions, _ => new()).Advance(delta, () =>
        {
            const double frame = ApplicationFixedUpdateScheduler.UpdateDelta;
            MainThreadClock.Advance(_saveData, _runtimeState);
            _transitions.BeginLinkUpdate();
            _transitions.UpdateWarp(frame);
            if (_transitions.TimeWarpActive)
                _roomEvents.UpdateDuringTimeWarp(frame);
            else
                _roomEvents.Update(frame);
        });
    }
}
