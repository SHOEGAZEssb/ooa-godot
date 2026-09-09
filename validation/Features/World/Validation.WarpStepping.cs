namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void UpdateRoomWarpTransition(double delta)
    {
        // Validation often advances several nominal frames at once. The live
        // time-warp controller deliberately processes at most one vblank step
        // per rendered call, so preserve that call boundary in bulk checks.
        const double frame = 1.0 / 60.0;
        while (_transitions.TimeWarpActive && delta > frame + 0.000001)
        {
            _transitions.UpdateWarp(frame);
            if (_transitions.TimeWarpActive)
                _roomEvents.UpdateDuringTimeWarp(frame);
            else
                _roomEvents.Update(frame);
            delta -= frame;
        }
        if (delta > 0.000001)
        {
            _transitions.UpdateWarp(delta);
            if (_transitions.TimeWarpActive)
                _roomEvents.UpdateDuringTimeWarp(delta);
            else
                _roomEvents.Update(delta);
        }
    }
}
