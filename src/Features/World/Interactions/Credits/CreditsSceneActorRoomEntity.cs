using Godot;
using System.Collections.Generic;

namespace Oracles;

// introSprites1.s:$4a:$09/$0a and cf.s. Coordinates remain world bytes;
// the fullscreen OAM owner projects through the authoritative camera.
internal sealed class CreditsSceneActorRoomEntity(RoomEventContext context,BlackTowerEndingDatabase data,
    EndingScreen screen,int id,int subid,int variant=0,CreditsSceneActorRoomEntity? parent=null) :
    IRoomEntity,IFixedRoomEntity,IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    private EnemyAnimationPlayer _animation=null!;
    internal int Id=>id;
    internal int Subid=>subid;
    internal int Variant=>variant;
    internal int State {get;private set;}
    internal int AnimationCounter=>State==0?0:_animation.RemainingUpdates;
    internal int AnimationParameter=>State==0?0:_animation.CurrentParameter;
    internal Vector2 Position {get;private set;}
    public Node2D Node {get;}=new(){Visible=false};
    public bool UpdatesDuringDialogue=>State==0||subid==10&&id==0x4a;
    public bool UpdatesDuringRoomEntityFreeze=>UpdatesDuringDialogue;
    public void SetTransitionDrawOffset(Vector2 offset) {}
    internal void Detach()=>screen.RemoveObjectOam(this);
    private void Relocate()
    {
        var offsets=data.Table("triforce-glow-offsets");
        int y=((int)parent!.Position.Y+(sbyte)offsets[variant*2]+(context.Entities.NextRandomValue()&3)-2)&255;
        int x=((int)parent.Position.X+(sbyte)offsets[variant*2+1]+(context.Entities.NextRandomValue()&3)-2)&255;
        Position=new(x,y);
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(State==0)
        {
            State=1;var record=data.ActorRecord(id,subid);
            _animation=new(Node,1);
            _animation.Load(OracleGraphicsCache.LoadImage(AgesGameContent.AssetRoot + $"gfx/{record.SpriteName}.png"),
                [record.UpAnimation],record.TileBase-(id==0x4a?0x60:0),record.Palette,positionedOam:true);
            _animation.SetAnimation(0);
            if(id==0x4a&&subid==10)
            {_animation.SetFrameCounter((_animation.RemainingUpdates+4*variant)&255);Relocate();}
            else
            {
                var positions=data.Table(id==0xcf?"waving-positions":"triforce-positions");int index=id==0xcf?subid:variant;
                Position=new(positions[index*2+1],positions[index*2]);
                if(id==0x4a)for(int child=2;child>=0&&context.Entities.InteractionSlotAvailable;child--)
                    context.Entities.AddEntity(new CreditsSceneActorRoomEntity(context,data,screen,0x4a,10,child,this));
            }
        }
        else
        {
            if(id==0x4a&&subid==10&&_animation.CurrentParameter==255)Relocate();
            _animation.Advance();
        }
        screen.SetObjectOam(this,data.NativeFrames(id,subid)[_animation.FrameIndex],(int)Position.Y,(int)Position.X,
            context.Entities.NativeInteractionSlot(this),id==0x4a&&subid==10?0:2,context.Transitions.WorldToScreen);
    }
}
