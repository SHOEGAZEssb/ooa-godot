using Godot;
using System.Collections.Generic;

namespace oracleofages;

// ages/parts/roomOfRitesFallingBoulder.s, PART $54. The invisible spawner
// and every falling child retain independent native PART slots.
internal sealed partial class RoomOfRitesBoulderRoomEntity : TransitionOffsetNode2D,
    IRoomEntity,IFixedRoomEntity,IRoomEntityLifetime,IUpdatesDuringDialogueRoomEntity,
    IUpdatesDuringRoomEntityFreeze,INativePartHealthRoomEntity
{
    private readonly RoomEntityManager _owner;
    private readonly FallingBoulderDatabase _data;
    private readonly EnemyAnimationPlayer _animation;
    private readonly TerrainShadowDefinition _shadow=TerrainShadow.Load();
    internal int Subid {get;}
    internal int State {get;private set;}
    internal int Counter {get;private set;}
    internal int ZFixed {get;private set;}
    internal int SpeedZ {get;private set;}
    internal int Slot {get;set;}
    internal int AnimationCounter=>_animation.RemainingUpdates;
    internal int AnimationParameter=>_animation.CurrentParameter;
    public Node2D Node=>this;
    public bool Finished {get;private set;}
    public bool UpdatesDuringDialogue=>State==0;
    public bool UpdatesDuringRoomEntityFreeze=>State==0;
    public void ClearHealthAndCollision() { } // partCode54 ignores PART status.
    internal RoomOfRitesBoulderRoomEntity(RoomEntityManager owner,FallingBoulderDatabase data,int subid=0)
    {
        _owner=owner;_data=data;Subid=subid;Visible=false;
        _animation=new(this,1);
        _animation.Load(OracleGraphicsCache.LoadImage($"res://assets/oracle/gfx/{data.Sprite}.png"),
            [data.Animation],data.TileBase,data.Palette,sourceGrayscaleInverted:data.SourceGrayscaleInverted);
        _animation.SetAnimation(0);ZIndex=ObjectDrawPriority.FromVisible(0xc1);
    }
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset)=>SetTransitionDrawOffset(offset);
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(Finished)return;
        if(Subid==0)
        {
            if(State==0){State=1;Counter=150;return;}
            if(Counter!=0)Counter--;
            if(Counter==0){Finish();return;}
            if((Counter&15)==0&&_owner.PartSlotAvailable)
                _owner.AddEntity(new RoomOfRitesBoulderRoomEntity(_owner,_data,1));
            return;
        }
        if(State==0)
        {
            State=1;Vector2 camera=-_owner.ToScreen(Vector2.Zero);int value=_owner.Random.Next().Value;
            int localY=(value&0x70)+8;
            Position=new(((value&7)*16+40+(int)camera.X)&255,(localY+(int)camera.Y)&255);
            ZFixed=unchecked((sbyte)(-localY-7))<<8;Visible=true;return;
        }
        int z=ZFixed,speed=SpeedZ;bool landed=OracleObjectMath.UpdateSpeedZ(ref z,ref speed,0x20);
        ZFixed=z;SpeedZ=speed;
        if(!landed){_animation.Advance();QueueRedraw();return;}
        var hazard=_owner.ActiveRoom.GetTerrainInfo(Position+new Vector2(0,5)).Hazard;
        if(hazard==HazardType.Hole)
        {
            if(_owner.InteractionSlotAvailable)_owner.Spawn(new FallingDownHoleSpawn(Position));
        }
        else if(hazard is HazardType.Water or HazardType.Lava)_owner.TryCreateSplash(Position,hazard);
        else _owner.TryCreateRockDebris(Position);
        Finish();
    }
    private void Finish(){Finished=true;Visible=false;}
    public override void _Draw()
    {
        if(!Visible)return;
        int z=ZFixed>>8;
        if(z<0&&((_owner.FrameCounter^Slot)&1)!=0&&
            (_owner.ActiveRoom.TilesetFlags&(int)TilesetFlags.Sidescroll)==0&&16-_owner.ToScreen(Vector2.Zero).Y<0x97)
            DrawTexture(_shadow.Texture,_shadow.Offset+SourceOamDrawOffset);
        DrawTexture(_animation.CurrentTexture,new Vector2(-16,-16+z)+SourceOamDrawOffset);
    }
}
