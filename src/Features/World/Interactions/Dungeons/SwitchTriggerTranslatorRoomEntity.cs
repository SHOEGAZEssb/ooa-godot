using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// INTERAC $24:$x0/$x1 never leaves state0; enabled bit 1 deletes before writing.
internal sealed partial class SwitchTriggerTranslatorRoomEntity(DungeonObjectRecord record,
    OracleRuntimeState runtime, Action<int, bool> setTrigger, Func<IRoomEntity, bool> isOutgoing) : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IScreenTransitionPreloadRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => true;
    public bool UpdatesDuringRoomEntityFreeze => true;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Dispatch();
    public void UpdateDuringScreenTransition(RoomEntityFrame frame) => Dispatch();
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { Dispatch(); return ScreenTransitionPresentation.Hidden; }
    public void SetTransitionDrawOffset(Vector2 offset) { }
    private void Dispatch()
    {
        if (Finished) return;
        if (isOutgoing(this)) { Finished = true; return; }
        int bit = (record.SubId >> 4) & 7;
        int address = (record.SubId & 15) switch {
            0 => OracleRuntimeState.ToggleBlocksStateAddress,
            1 => OracleRuntimeState.SwitchStateAddress,
            _ => throw new InvalidOperationException($"{record.Source}: unsupported translator $24:${record.SubId:x2}.") };
        setTrigger(bit, (runtime.ReadWramByte(address) & (1 << bit)) != 0);
    }
}
