using System.Collections.Generic;

namespace Oracles;

// statueEyeball.s:$e2:$04: one counter/RNG consumer per physical slot.
internal sealed class EyePuzzleStatueRoomEntity(StatueEyeball eye, RoomEntityManager owner)
    : RoomEntityAdapter<StatueEyeball>(eye, eye.SetTransitionDrawOffset),
        IFixedRoomEntity, IScreenTransitionPreloadRoomEntity,
        IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private int _counter;
    private bool _spinning, _stopped;
    public bool UpdatesDuringDialogue => !Entity.Initialized;
    public bool UpdatesDuringRoomEntityFreeze => !Entity.Initialized;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.Initialized) { Entity.PrepareForScreenTransition(); return; }
        if (_stopped) { Entity.Visible = true; return; }
        if (!_spinning)
        {
            _spinning = true;
            _counter = EyePuzzleDatabase.Shared.SpinUpdates;
            return;
        }
        if (--_counter == 0)
        {
            _stopped = true;
            int direction;
            do { direction = owner.IncrementNativeFrameCounter() & 3; }
            while (direction == EyePuzzleDatabase.Shared.CorrectDirection(owner.RuntimeState));
            Entity.SetLookingDirection(direction * 2);
        }
        else if ((owner.FrameCounter & 3) == 0)
            Entity.SetLookingDirection(owner.NextRandomValue() & 7);
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Entity.PrepareForScreenTransition();
        return ScreenTransitionPresentation.Visible;
    }
}
