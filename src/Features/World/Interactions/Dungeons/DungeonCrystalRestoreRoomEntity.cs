using Godot;
using System.Collections.Generic;

namespace Oracles;

// INTERAC $21:$18 has no enabled-bit guard and deletes in its first dispatch.
internal sealed partial class DungeonCrystalRestoreRoomEntity(DungeonObjectRecord record,
    SharedInteractionDatabase database, OracleSaveData save, OracleRuntimeState runtime) : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IScreenTransitionPreloadRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => true;
    public bool UpdatesDuringRoomEntityFreeze => true;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Dispatch();
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { Dispatch(); return ScreenTransitionPresentation.Hidden; }
    public void SetTransitionDrawOffset(Vector2 offset) { }
    private void Dispatch()
    {
        if (Finished) return;
        int value = runtime.ReadWramByte(AgesGameContent.SwitchStateAddress);
        foreach (var flag in database.CrystalFlags)
            if (save.HasRoomFlag(record.Group, flag.Room, flag.Flag)) value |= 1 << flag.Bit;
        runtime.SetWramByte(AgesGameContent.SwitchStateAddress, (byte)value);
        Finished = true;
    }
}
