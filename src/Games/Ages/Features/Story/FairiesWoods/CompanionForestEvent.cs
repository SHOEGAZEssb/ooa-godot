
namespace Oracles;

// Room-level cancellation resources; each $71 controller owns its native turn.
internal sealed class CompanionForestEvent(RoomEventContext context) : IRoomEvent
{
    private readonly List<CompanionForestControllerRoomEntity> _controllers = new();
    private readonly List<ForestFairyFlight> _flights = new();
    private readonly List<IRoomEntity> _markers = new();
    internal RoomEventContext Context => context;
    public bool RequiresRoomEventUpdate => false;
    public bool HasState => _controllers.Any(controller => !controller.Finished) || _flights.Any(IsLive);
    public bool BlocksGameplay => _controllers.Any(controller => !controller.Finished && controller.BlocksGameplay);
    public bool MenusDisabled => HasState && context.Entities.RuntimeState.ReadField("wMenuDisabled") != 0;
    internal int Instruction => _controllers.LastOrDefault(controller => !controller.Finished)?.Instruction ?? 0;
    internal int Signal => context.Entities.RuntimeState.ReadWramByte(0xcfd2);
    internal IReadOnlyList<ForestFairyFlight> Flights
    {
        get { _flights.RemoveAll(flight => !IsLive(flight)); return _flights; }
    }
    private static bool IsLive(ForestFairyFlight flight) => GodotObject.IsInstanceValid(flight.Actor) && flight.Active;
    public void ReleaseOutgoingActors(int group, OracleRoomData room)
    {
        if (!context.Entities.ScreenTransitionActive) return;
        _controllers.RemoveAll(context.Entities.IsOutgoingEntity);
        _flights.RemoveAll(context.Entities.IsOutgoingEntity);
        _markers.RemoveAll(context.Entities.IsOutgoingEntity);
    }
    internal void RegisterController(CompanionForestControllerRoomEntity controller)
    { if (!context.Entities.IsOutgoingEntity(controller) && !_controllers.Contains(controller)) _controllers.Add(controller); }
    internal void RegisterFlight(ForestFairyFlight flight) => _flights.Add(flight);
    internal void RegisterMarker(Node2D mark) => _markers.Add(context.Entities.EntityAdapters<IRoomEntity>()
        .Single(entity => ReferenceEquals(entity.PresentationNode, mark)));
    public void UpdateFrame() => throw new InvalidOperationException("Companion scripts must run from their native $71 slots.");
    public void Cancel()
    {
        foreach (var controller in _controllers) controller.Cancel();
        _controllers.Clear();
        foreach (var flight in _flights) if (IsLive(flight)) flight.Deactivate();
        _flights.Clear();
        foreach (var marker in _markers) context.Entities.RetireEntity(marker);
        _markers.Clear();
    }
}
