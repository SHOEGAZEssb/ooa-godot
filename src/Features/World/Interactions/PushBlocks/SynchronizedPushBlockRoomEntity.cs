using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class SynchronizedPushBlockRoomEntity(PushBlockController actor,byte sourcePosition,
    int angle,Func<int> braceletLevel)
    : RoomEntityAdapter<PushBlockController>(actor,offset => actor.Position = offset),
        IFixedRoomEntity, IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity,
        IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity,
        IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private bool _initialized;
    public bool Finished => _initialized && !Entity.Active;
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;
    internal byte SourcePosition => sourcePosition;

    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns) => Advance(frame.Player);
    public void UpdateDuringScreenTransition(RoomEntityFrame frame)
    {
        if (!_initialized) Advance(frame.Player);
    }
    private void Advance(Player player)
    {
        if (!_initialized)
        {
            _initialized = true;
            Entity.StartNativeMovement(sourcePosition,angle,braceletLevel());
        }
        // Native state0 falls through: initialize, move and decrement once.
        Entity.Advance(1.0 / 60.0,player);
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns) =>
        throw new InvalidOperationException("INTERAC $14 state0 requires live Link contact.");
    public ScreenTransitionPresentation PrepareForScreenTransition(Player? player,ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized) Advance(player ?? throw new InvalidOperationException("INTERAC $14 preload requires Link."));
        return Entity.Active ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }
}
