
namespace Oracles;

// bank0.updateAllObjects: native cutscene callers supply their transition and
// script phases; the special-object, entity and cleanup order has one owner.
internal sealed class GameplayObjectDispatcher(
    Player player, RoomEntityManager entities, RoomSession rooms, IGameplayObjectPhases phases, Action<double> complete)
{
    internal bool Suspended { get; private set; }
    private bool _nativeItemPostPending;

    internal void Cancel() { Suspended = false; _nativeItemPostPending = false; }

    internal void Advance(double delta)
    {
        if (Suspended)
            throw new InvalidOperationException("A yielded object pass must resume before starting another update.");
        player.AdvanceDeathPrelude();
        if (!phases.PrepareObjectUpdate(delta)) return;
        OracleRuntimeState memory = rooms.RuntimeState;
        // updateSpecialObjects clears this before the companion and Link
        // publish their own bits, including when instruments freeze the companion.
        memory.SetField("wcc92",0);
        int mode = memory.ReadField("wUseSimulatedInput");
        int? simulatedKeys = mode is not (0 or 2) && (mode & 0x80) == 0 && memory.ReadField("wPaletteThread_mode") == 0 ?
            phases.SampleSimulatedInput() : null;
        Input.PublishGameKeys(memory, simulatedKeys, player.IsDying);
        if (phases.CompanionUpdateAllowed && (player.World as PlayerWorld)?.Instruments.IsPlaying != true)
            entities.UpdateSpecialObjectsBeforePlayer(player);
        phases.UpdateLink();
        // specialObjects.s decrements this after Link, including frozen Link visits.
        byte instrumentLock = memory.ReadField("wInstrumentsDisabledCounter");
        if (instrumentLock != 0) memory.SetField("wInstrumentsDisabledCounter", (byte)(instrumentLock - 1));
        phases.AfterLinkUpdate();
        entities.ClearSignalsAfterPlayer();
        _nativeItemPostPending = phases.PrepareEntityUpdate(delta);
        if (_nativeItemPostPending) entities.UpdateObjects(delta, player);
        if (!phases.UpdateRoomInteractions(delta, resuming: false))
        {
            Suspended = true;
            return;
        }
        Finish(delta);
    }

    internal void Resume(double delta)
    {
        if (!Suspended)
            throw new InvalidOperationException("A room interaction yielded outside the shared object pass.");
        Input.UseGameKeys(rooms.RuntimeState);
        if (!phases.UpdateRoomInteractions(delta, resuming: true)) return;
        Suspended = false;
        Finish(delta);
    }

    private void Finish(double delta)
    {
        phases.BeforeObjectTail(delta);
        entities.SynchronizeCompanionRiderAfterObjects(player);
        entities.UpdateHeldObjectPosition(player);
        player.PublishNativeWalkAnimation();
        entities.PublishCompanionGraphics();
        if (_nativeItemPostPending)
        {
            _nativeItemPostPending = false;
            entities.UpdateNativeItemsPost();
        }
        if (phases.TileGraphicsUpdateAllowed)
            rooms.UpdateChangedTileGraphics(rooms.RuntimeState.ReadField("wScrollMode"));
        phases.AfterObjectTail(delta);
        complete(delta);
    }
}
