using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class KillPuffRoomEntity(KillEnemyPuffEffect puff)
    : RoomEntityAdapter<KillEnemyPuffEffect>(puff, puff.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime,
        IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
        IAlwaysUpdateDuringScreenTransitionRoomEntity, IScreenTransitionPreloadRoomEntity
{
    // INTERAC$08 shares breakTileDebris.s; counter2 is unused by its handler.
    internal byte Counter2Alias { get; private set; }
    internal void WriteCounter2Alias(int value) => Counter2Alias = unchecked((byte)value);
    public bool Finished => Entity.Finished;
    public void UpdateDuringScreenTransition(RoomEntityFrame frame) => Entity.UpdateFrame();
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.Initialized) Entity.UpdateFrame();
        return Entity.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame();
}
