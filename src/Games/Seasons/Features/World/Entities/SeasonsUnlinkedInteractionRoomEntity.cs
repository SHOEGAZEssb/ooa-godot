
namespace Oracles;

// miscPuzzles $dc:$01/$02, Impa $9d:$03 and Zelda $44:$08 delete in unlinked state0.
internal sealed class SeasonsUnlinkedInteractionRoomEntity(OracleRuntimeState memory,NativeObjectInstruction source) : IRoomEntity, IFixedRoomEntity,
    INativeInteractionRoomEntity, IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity,
    IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    public Node2D Node {get;}=new(){Name=$"UnlinkedInteraction_{source.Id:x2}_{source.SubId:x2}",Visible=false};
    public bool Finished {get;private set;}
    public bool NativeInitializationPending=>!Finished;
    public bool UpdatesDuringDialogue=>true;
    public bool UpdatesDuringRoomEntityFreeze=>true;
    private void Initialize()
    {
        if(memory.ReadField("wIsLinkedGame")!=0)
            throw new NotSupportedException($"{source.Source}: INTERAC${source.Id:x2}:${source.SubId:x2} linked initialization is not implemented.");
        Finished=true;
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)=>Initialize();
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {Initialize();return ScreenTransitionPresentation.Hidden;}
    public void SetTransitionDrawOffset(Vector2 offset){}
}
