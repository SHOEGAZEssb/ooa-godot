using Godot;
using System.Collections.Generic;

namespace Oracles;

// Ages companionScripts.s:$71:$03 installs its script independently of $67.
internal sealed class RickyGlovesControllerRoomEntity(RickyGlovesEvent owner)
    : INativeInteractionRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
      IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    private bool _initialized;
    internal int State => _initialized ? 1 : 0;
    public Node2D? PresentationNode => null;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;
    public void SetTransitionDrawOffset(Vector2 offset) { }
    private void Dispatch()
    {
        if (Finished) return;
        if (!_initialized)
        {
            _initialized = true;
            Finished = !owner.InitializeController(this);
            return;
        }
        Finished = !owner.AdvanceController(this);
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Dispatch();
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { Dispatch(); return ScreenTransitionPresentation.Hidden; }
}
