
namespace Oracles;

// Ages companionScripts.s:$71:$00 is an enabled1 interaction. var3a is
// decremented by its body before the native script's separate wait/call loop.
internal sealed class MooshRescueControllerRoomEntity(MooshRescueEvent owner)
    : INativeInteractionRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
      IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    private bool _initialized;
    internal int State => _initialized ? 1 : 0;
    internal byte ShakeCounter { get; set; }
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
