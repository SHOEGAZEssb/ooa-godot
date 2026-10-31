using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// A persistent native $9f allocation supplies slot occupancy and phase observation.
internal sealed class FluteCallPhaseProbe(OracleRuntimeState memory,Action visit) :
    INativeInteractionRoomEntity,IFixedRoomEntity,IRoomEntityLifetime
{
    private int _address;
    public Node2D Node { get; } = new();
    public bool Finished { get; private set; }
    public bool NativeInitializationPending => true;
    public void BindNativeInteractionSlot(int slot)
    {
        _address=((0xd0+slot)<<8)|0x40;
        memory.SetWramByte(_address,1); memory.SetWramByte(_address+1,0x9f);
        memory.SetWramByte(_address+6,0xff);
    }
    public void ReleaseNativeInteractionSlot()
    {
        if (Finished) return;
        for(int offset=0;offset<0x40;offset++) memory.SetWramByte(_address+offset,0);
        Finished=true;
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)=>visit();
    public void SetTransitionDrawOffset(Vector2 offset) { }
}
