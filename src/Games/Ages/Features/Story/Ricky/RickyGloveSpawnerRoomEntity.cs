
namespace Oracles;

// INTERAC_RICKYS_GLOVE_SPAWNER $74 retains its slot while creating $60:$48.
internal sealed class RickyGloveSpawnerRoomEntity(GroundTreasureDatabaseRecord record,
    OracleSaveData save,InventoryState inventory,RoomEntityManager owner) :
    INativeInteractionRoomEntity,IFixedRoomEntity,IRoomEntityLifetime,IScreenTransitionPreloadRoomEntity
{
    private int _address;
    private bool _retired;
    public Node2D Node {get;}=new(){Name="RickyGloveSpawner_74",Visible=false};
    public bool Finished=>_retired || _address!=0 && owner.RuntimeState.ReadWramByte(_address)==0;
    public bool NativeInitializationPending=>!Finished;
    public void BindNativeInteractionSlot(int slot)
    {
        if(record.NativeSpawnerId!=0x74 || save.Game!=OracleGame.Ages)
            throw new InvalidOperationException($"{record.Source}: native $74 requires the Ages glove-spawner record.");
        _address=((0xd0+slot)<<8)|0x40;
        Write(0,1);Write(1,0x74);Write(2,0);Write(0x0b,record.Y);Write(0x0d,record.X);
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(Finished)return;
        if(GroundTreasureDatabase.ShouldSpawn(record,save,inventory))
        {
            if(!owner.InteractionSlotAvailable)return;
            owner.Spawn<GroundTreasurePickup>(new GroundTreasureRecordSpawn(record with
            {Y=Read(0x0b),X=Read(0x0d),NativeSpawnerId=-1},(byte)Read(0x0f)));
        }
        ReleaseNativeInteractionSlot();
    }
    public void ReleaseNativeInteractionSlot()
    {
        if(!_retired && _address!=0 && Read(1)==0x74)
            for(int offset=0;offset<64;offset++)Write(offset,0);
        _retired=true;
    }
    private int Read(int offset)=>owner.RuntimeState.ReadWramByte(_address+offset);
    private void Write(int offset,int value)=>owner.RuntimeState.SetWramByte(_address+offset,unchecked((byte)value));
    public void SetTransitionDrawOffset(Vector2 offset) { }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)=>ScreenTransitionPresentation.Hidden;
}
