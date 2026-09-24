using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Fixed-update owner for INTERAC_CLINK effects allocated by sword beams and
/// enemy collision effects.
/// </summary>
internal sealed class SwordBeamClinkRoomEntity(ClinkEffect clink, Action? initializeOnUpdate = null)
    : RoomEntityAdapter<ClinkEffect>(clink, static _ => { }),
        IFixedRoomEntity, IRoomEntityLifetime,
        IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
        IAlwaysUpdateDuringScreenTransitionRoomEntity, IScreenTransitionPreloadRoomEntity
{
    // INTERAC$07 shares breakTileDebris.s; counter2 is unused by its handler.
    internal byte Counter2Alias { get; private set; }
    internal void WriteCounter2Alias(int value) => Counter2Alias = unchecked((byte)value);
    private Action? _initializeOnUpdate = initializeOnUpdate;
    public bool Finished => Entity.Finished;
    public void UpdateDuringScreenTransition(RoomEntityFrame frame) => Advance();
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.ElapsedFrames == 0) Advance();
        return Entity.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }
    public void UpdateFrame(
        RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
        => Advance();

    private void Advance()
    {
        if (_initializeOnUpdate is { } initialize)
        {
            _initializeOnUpdate = null;
            Entity.Visible = true;
            initialize();
        }
        Entity.AdvanceFrameForEntityManager();
    }
}
