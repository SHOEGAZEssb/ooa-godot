using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// miscPuzzles.s $dc:$01/$02 both delete in state0 for an unlinked file.
internal sealed class SeasonsHerosCaveLinkedEntranceRoomEntity : IRoomEntity, IFixedRoomEntity,
    INativeInteractionRoomEntity, IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity,
    IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    public Node2D Node {get;}=new(){Name="UnlinkedHerosCaveEntranceGuard",Visible=false};
    public bool Finished {get;private set;}
    public bool NativeInitializationPending=>!Finished;
    public bool UpdatesDuringDialogue=>true;
    public bool UpdatesDuringRoomEntityFreeze=>true;
    internal SeasonsHerosCaveLinkedEntranceRoomEntity(OracleSaveData save,NativeObjectInstruction source)
    {
        if(save.IsLinkedGame)throw new NotSupportedException($"{source.Source}: linked Hero's Cave entrance is not implemented.");
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)=>Finished=true;
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {Finished=true;return ScreenTransitionPresentation.Hidden;}
    public void SetTransitionDrawOffset(Vector2 offset){}
}
