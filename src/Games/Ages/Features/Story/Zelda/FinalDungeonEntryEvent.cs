using System;
using System.Linq;

namespace Oracles;

// Lifecycle/fade resources for the native entrance interaction. Its timer
// and Link handoff execute exclusively in the physical $b5 interaction slot.
internal sealed class FinalDungeonEntryEvent(RoomEventContext context, Func<bool> flash) : IRoomEntryEvent
{
    private readonly RoomEventResources _resources = new(context, new object());
    private FinalDungeonEnergyRoomEntity? _controller;
    private bool _ownsFade;
    public bool HasState => _controller is { Finished: false };
    public bool BlocksGameplay => false;
    public bool Matches(int group, OracleRoomData room) => group == 5 && room.Id == 0xf4;
    internal FinalDungeonEnergyRoomEntity Create(FinalDungeonEntryDatabase data) => new(context, data, this);
    public void Start(OracleRoomData room) => _controller = context.Entities.EntityAdapters<FinalDungeonEnergyRoomEntity>().SingleOrDefault();
    public void UpdateFrame() { }
    internal bool Flash()
    {
        _resources.CaptureFullScreenFade(); _ownsFade = true;
        return flash();
    }
    internal void FadeIn(int delay) => context.Transitions.BeginNativeWhiteFade(false, delay);
    internal void AdvanceBeforeObjects()
    {
        if (_ownsFade && HasState && _controller!.Substate == 2) context.Transitions.AdvanceNativeWhiteFade();
    }
    internal void Complete()
    {
        _resources.ReleaseFullScreenFade(restoreColor: false);
        _ownsFade = false;
    }
    public void Cancel()
    {
        _controller?.Cancel(); _controller = null;
        if (_ownsFade) context.Transitions.CancelNativeWhiteFade();
        _resources.ReleaseFullScreenFade(); _ownsFade = false;
    }
}
