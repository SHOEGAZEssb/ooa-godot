using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class NayruRescueFlowerRoomEntity : IRoomEntity,IFixedRoomEntity,IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    private readonly MakuTreeAdviceDatabase _data;
    private readonly NayruRescueActorRoomEntity _parent;
    private readonly NpcCharacter _actor;
    public Node2D Node => _actor;
    public bool UpdatesDuringDialogue => true;
    public bool UpdatesDuringRoomEntityFreeze => true;
    internal NayruRescueFlowerRoomEntity(MakuTreeAdviceDatabase data,NayruRescueActorRoomEntity parent)
    {_data=data;_parent=parent;_actor=NpcCharacter.CreateFromRecord(data.Flower);_actor.SetBlocksLink(false);_actor.SetAnimationRate(0);}
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {_actor.ShowInitializedPresentation();_actor.SetScriptAnimation(_parent.FlowerFrowning?_data.FrowningFlower:_data.Flower.UpAnimation);}
    public void SetTransitionDrawOffset(Vector2 offset) => _actor.SetTransitionDrawOffset(offset);
}
