using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// INTERAC $71:$0d remains state0, including while text and object masks are active.
internal sealed partial class SymmetryCompanionBarrierRoomEntity(DungeonObjectRecord record,
    OracleSaveData save, OracleRuntimeState runtime, Func<ICompanionBarrierTarget?> companion,
    Action<int, Vector2> showText) : Node2D, IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => true;
    public bool UpdatesDuringRoomEntityFreeze => true;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Dispatch(frame.Player);
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns) =>
        throw new InvalidOperationException($"{record.Source}: companion barrier preload requires Link's death state.");
    public ScreenTransitionPresentation PrepareForScreenTransition(Player? player, ICollection<RoomEntitySpawn> spawns)
    {
        if (player is null) throw new InvalidOperationException($"{record.Source}: companion barrier preload requires Link's death state.");
        Dispatch(player); return ScreenTransitionPresentation.Hidden;
    }
    public void SetTransitionDrawOffset(Vector2 offset) { }
    private void Dispatch(Player player)
    {
        if (Finished) return;
        if (player.IsDying) { runtime.SetWramByte(WramAddress.wDisabledObjects, 0); Finished = true; return; }
        if (save.HasGlobalFlag(GlobalFlag.TuniNutPlaced)) { Finished = true; return; }
        if ((runtime.ReadWramByte(WramAddress.wScrollMode) & 0x0e) != 0 ||
            !CompanionRuntimeState.AnyActive(runtime) || companion() is not { } target || target.CompanionId is < 0x0b or > 0x0d) return;
        if (unchecked((byte)(record.X - (int)target.BarrierPosition.X + 5)) >= 0x0b ||
            record.Y < unchecked((byte)(int)target.BarrierPosition.Y)) return;
        target.SetBarrierCoordinate(false, unchecked((byte)(record.Y + 1)));
        target.DropForSymmetryBarrier(player);
        showText(save.ReadWramByte(WramAddress.wAnimalCompanion), target.BarrierPosition);
    }
}
