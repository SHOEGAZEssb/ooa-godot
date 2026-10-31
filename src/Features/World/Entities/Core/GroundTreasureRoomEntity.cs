using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class GroundTreasureRoomEntity(
    GroundTreasurePickup treasure,
    OracleRuntimeState memory,int treasureId,byte? zHigh,
    Func<bool> collectionAllowed,
    Action<GroundTreasurePickup, Player> collected)
    : RoomEntityAdapter<GroundTreasurePickup>(
        treasure, treasure.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime, INativeInteractionRoomEntity,
        IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IDugTileRoomEntity,
        IScreenTransitionPreloadRoomEntity, IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private int _address;
    private bool _retired;
    public bool Finished => _retired || Entity.Finished || _address!=0 && memory.ReadWramByte(_address)==0;
    public bool NativeInitializationPending => Entity.State==PickupState.Initializing;
    public void BindNativeInteractionSlot(int slot)
    {
        _address=((0xd0+slot)<<8)|0x40;
        memory.SetWramByte(_address,1); memory.SetWramByte(_address+1,0x60);
        memory.SetWramByte(_address+2,checked((byte)treasureId));
        memory.SetWramByte(_address+0x0b,(byte)Entity.Record.Y); memory.SetWramByte(_address+0x0d,(byte)Entity.Record.X);
        // Placed obj_DoubleValue writes XY only; objectCreateInteraction copies XYZ highs.
        if(zHigh is byte value) memory.SetWramByte(_address+0x0f,value);
        Entity.SetNativeBirthZHigh(memory.ReadWramByte(_address+0x0f));
    }
    public void ReleaseNativeInteractionSlot()
    {
        if(!_retired && _address!=0 && memory.ReadWramByte(_address+1)==0x60 && memory.ReadWramByte(_address+2)==treasureId)
            for(int offset=0;offset<64;offset++) memory.SetWramByte(_address+offset,0);
        _retired=true;
    }
    bool IUpdatesDuringDialogueRoomEntity.UpdatesDuringDialogue =>
        Entity.UpdatesDuringDialogue;
    bool IUpdatesDuringRoomEntityFreeze.UpdatesDuringRoomEntityFreeze => Entity.UpdatesDuringDialogue;

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Entity.InitializeGraphicsState();
        return Entity.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }

    public void UpdateDuringScreenTransition(RoomEntityFrame frame)
    {
        if (Entity.UpdatesDuringDialogue) Entity.UpdateFrame(frame.Player);
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        // State2 alone calls returnIfScrollMode01Unset; spawn checks can fall through.
        bool touchAllowed = Entity.State != PickupState.Waiting || (memory.ReadField("wScrollMode") & 1) != 0;
        Entity.UpdateFrame(frame.Player);
        if (touchAllowed && Entity.TryCollectScriptGrant()) collected(Entity, frame.Player);
        else if (touchAllowed && collectionAllowed() && Entity.TryCollect(frame.Player))
            collected(Entity, frame.Player);
    }

    public void NotifyTileDug(int packedPosition) =>
        Entity.NotifyTileDug(packedPosition);

}
