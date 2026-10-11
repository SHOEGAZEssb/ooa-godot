using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class NayruRescueBowtieRoomEntity : IRoomEntity,IFixedRoomEntity,IRoomEntityLifetime
{
    private readonly NpcCharacter _actor;
    private readonly NayruRescueActorRoomEntity _parent;
    private readonly NayruRescueWarpDatabase _data;
    private int _animation=-1;
    public Node2D Node => _actor;
    public bool Finished => _parent.Finished;
    internal NayruRescueBowtieRoomEntity(NayruRescueWarpDatabase data,NayruRescueActorRoomEntity parent)
    {_data=data;_parent=parent;_actor=data.CreateActor(0x63,0x3d,Vector2.Zero);}
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(Finished)return;
        _actor.ShowInitializedPresentation();_actor.Visible=_parent.Actor.Visible;
        if(!_actor.Visible)return;
        var table=_data.Table("bowtie-offsets");int index=_parent.Actor.CurrentAnimationParameter*4;
        if(index+3>=table.Length)throw new System.InvalidOperationException($"INTERAC$63:$3d parent animParameter ${_parent.Actor.CurrentAnimationParameter:x2}.");
        if(_animation!=table[index+3]) {_animation=table[index+3];_actor.SetScriptAnimation(_data.Animation(0x63,_animation));}
        _actor.SetStatePosition(_parent.PrecisePosition.Floor()+new Vector2(unchecked((sbyte)table[index+1]),unchecked((sbyte)table[index])));
        int palette=_parent.Substate is 2 or 3?6:2;
        _actor.SetBasePalette(palette);_actor.SetScriptPaletteOverride(palette==6?_data.StonePalette:null);
        _actor.SetFixedDrawPriority(ObjectDrawPriority.FromVisible(table[index+2]));
        _actor.SetScriptDrawOffset(Vector2.Down*(_parent.ZFixed>>8));
    }
    public void SetTransitionDrawOffset(Vector2 offset) => _actor.SetTransitionDrawOffset(offset);
}
