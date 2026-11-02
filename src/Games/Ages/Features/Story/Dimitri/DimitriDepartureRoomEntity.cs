
namespace Oracles;

// $71:$06 runs after D1. Its raw state-zero request is consumed on D1's next visit.
internal sealed class DimitriDepartureRoomEntity : INativeInteractionRoomEntity,IFixedRoomEntity,IRoomEntityLifetime,IScreenTransitionPreloadRoomEntity
{
    private readonly RoomSession _rooms;
    private readonly RoomEntityManager _entities;
    private readonly DimitriDepartureScriptHost _script;
    private int _address;
    private bool _retired;
    internal OracleRuntimeState Memory=>_entities.RuntimeState;
    public Node2D Node {get;}=new(){Name="DimitriDeparture_71_06",Visible=false};
    public bool Finished=>_retired || _address!=0 && Read(0)==0;
    public bool NativeInitializationPending=>!Finished && (_address==0 || Read(4)==0);
    internal DimitriDepartureRoomEntity(RoomSession rooms,RoomEntityManager entities,DimitriDepartureDatabase data)
    {_rooms=rooms;_entities=entities;_script=new(this,rooms,entities,data);}
    public void BindNativeInteractionSlot(int slot)
    {_address=((0xd0+slot)<<8)|0x40;Write(0,1);Write(1,0x71);Write(2,6);}
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(Finished)return;
        // Player owns the live death trigger; its raw WRAM alias is not produced yet.
        if(frame.Player.IsDying)
        {Memory.SetField("wDisabledObjects",0);_script.Relinquish();ReleaseNativeInteractionSlot();return;}
        if(Read(4)==0)
        {
            if((_rooms.SaveData.ReadField("wDimitriState")&0x40)!=0 || Memory.ReadWramByte(0xd101)!=0x0c)
            {ReleaseNativeInteractionSlot();return;}
            if(Memory.ReadWramByte(0xd138)!=0)return;
            Write(4,1);_script.Begin();
            _script.SetTransitions(1);
            Memory.SetWramByte(0xd103,2);Memory.SetWramByte(0xd104,0x0a);
            return;
        }
        if(Read(4)!=1)throw new NotSupportedException($"companionScripts.s:$71:$06 state${Read(4):x2}.");
        _script.Advance(frame.Player);
    }
    internal Vector2 NativePosition=>new(Read(0x0d),Read(0x0b));
    internal void SetFacing(int[] frame)
    {
        Write(0x20,frame[0]);Write(0x21,frame[1]);Write(0x22,frame[2]);Write(0x23,frame[2]>>8);
        Write(0x1e,frame[3]);Write(0x1f,frame[3]>>8);
    }
    internal void EndScript()
    {Memory.SetField("wStatusBarNeedsRefresh",(byte)(Memory.ReadField("wStatusBarNeedsRefresh")|2));_script.Relinquish();ReleaseNativeInteractionSlot();}
    public void ReleaseNativeInteractionSlot()
    {
        if(_retired)return;
        _script.Cancel();
        if(_address!=0 && Read(1)==0x71 && Read(2)==6)
            for(int offset=0;offset<64;offset++)Write(offset,0);
        _retired=true;
    }
    private int Read(int offset)=>Memory.ReadWramByte(_address+offset);
    private void Write(int offset,int value)=>Memory.SetWramByte(_address+offset,unchecked((byte)value));
    public void SetTransitionDrawOffset(Vector2 offset) { }
    // No graphics preload. The D1-dependent request belongs to the physical interaction visit.
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)=>ScreenTransitionPresentation.Hidden;
}
