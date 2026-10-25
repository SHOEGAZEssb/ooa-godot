using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// companionCutscene.s:$11 -> $10 -> $0f. One owner retains the physical
// w1Companion slot while native replacements clear id..$3f, but not enabled.
internal sealed partial class CompanionCreditsRoomEntity(RoomEventContext context,BlackTowerEndingDatabase data,Vector2 position) :
    TransitionOffsetNode2D,IRoomEntity,IFixedRoomEntity,IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    private EnemyAnimationPlayer _animation=null!;
    private Vector2 _position=position;
    internal int Id=>CompanionRuntimeState.ReadCutsceneId(context.Entities.RuntimeState);
    internal int Subid=>CompanionRuntimeState.ReadCutsceneSubid(context.Entities.RuntimeState);
    internal int State {get;private set;}
    internal int Substate {get;private set;}
    internal int Counter {get;private set;}
    internal int Counter2 {get;private set;}
    internal int Direction {get;private set;}
    internal int Angle {get;private set;}
    internal int Speed {get;private set;}
    internal int ZFixed {get;private set;}
    internal int SpeedZ {get;private set;}
    internal int AnimationIndex=>_animation.AnimationIndex;
    internal int AnimationCounter=>State==0?0:_animation.RemainingUpdates;
    internal int AnimationParameter=>State==0?0:_animation.CurrentParameter;
    internal Texture2D Texture=>_animation.CurrentTexture;
    internal Vector2 FrameOffset=>_animation.CurrentOffset;
    internal Vector2 PrecisePosition=>_position;
    internal void ApplyNativePalette(IReadOnlyDictionary<int,Color[]>? palette)=>_animation?.SetPaletteOverride(palette);
    public Node2D Node=>this;
    public bool UpdatesDuringDialogue=>true;
    public bool UpdatesDuringRoomEntityFreeze=>true;
    private bool Decrement(){Counter=(Counter-1)&255;return Counter==0;}
    private void Move()=>Position=NativeObjectMovement.ApplySpeed(context.Entities.RuntimeState,ref _position,Speed,Angle);
    private bool Gravity(int value)
    {int z=ZFixed,speed=SpeedZ;bool landed=OracleObjectMath.UpdateSpeedZ(ref z,ref speed,value);ZFixed=z;SpeedZ=speed;return landed;}
    private void Animation(int index)=>_animation.SetAnimation(index);
    private void InitializeVisual()
    {
        string sprite;string[] animations;int tile=0,palette;int[][]? offsets=null;
        switch(Id)
        {
            case 0x11:var moosh=new MooshRescueEventDatabase().Visual;sprite=moosh.Sprite;animations=moosh.Animations;tile=moosh.TileBase;palette=moosh.Palette;break;
            case 0x10:var dimitri=new DimitriDatabase();sprite="spr_dimitri";animations=dimitri.Animations;offsets=dimitri.SourceOffsets;palette=2;break;
            case 0x0f:var ricky=new RickyGlovesEventDatabase().Visual;sprite=ricky.Sprite;animations=ricky.Animations;offsets=ricky.AnimationSourceOffsets;tile=ricky.TileBase;palette=ricky.Palette;break;
            default:throw new NotSupportedException($"companionCutscene.s: SPECIALOBJECT${Id:x2}.");
        }
        _animation=new(this,animations.Length);
        _animation.Load(OracleGraphicsCache.LoadImage(AgesGameContent.AssetRoot + $"gfx/{sprite}.png"),animations,tile,palette,
            positionedOam:true,animationSourceOffsets:offsets,paletteOverrides:context.Entities.NativeObjectPalettes);
        ZIndex=ObjectDrawPriority.FixedHighPriorityZIndex;Visible=true;
    }
    private void Replace(int id,int subid,Vector2 position)
    {
        CompanionRuntimeState.ReplaceCutscene(context.Entities.RuntimeState,id,subid,position);
        State=Substate=Counter=Counter2=Direction=Angle=Speed=ZFixed=SpeedZ=0;
        _position=position;Position=position;Visible=false;
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(State==0)
        {
            State=1;InitializeVisual();Position=_position.Floor();
            switch(Id)
            {
                case 0x11:
                    Counter=90;Speed=0x37;Angle=0x10;ZFixed=unchecked((short)0xe0ff);
                    if(context.Entities.InteractionSlotAvailable)context.Entities.AddEntity(new CreditsBananaRoomEntity(data,this));
                    Animation(7);break;
                case 0x10:Speed=0x28;ZFixed=-0x20;Animation(0x19);break;
                case 0x0f:Speed=0x50;Angle=8;SpeedZ=-0x200;Animation(2);break;
            }
        }
        else switch(Id){case 0x11:AdvanceMoosh();break;case 0x10:AdvanceDimitri();break;case 0x0f:AdvanceRicky();break;}
        QueueRedraw();
    }
    private void MooshAnimation()
    {if(((Angle-4)&7)==0){Direction=((((Angle+4)&0x18)>>3)-1)&3;Animation(5+Direction);}}
    private void AdvanceMoosh()
    {
        if(Substate!=0){_animation.Advance();Move();}
        switch(Substate)
        {
            case 0:if(Decrement()){Counter=72;Substate=1;}return;
            case 1:if(Decrement()){Counter=6;Substate=2;}return;
            case 2:if(Angle!=0x10){Substate=3;return;}goto case 3;
            case 3:
                if(Substate==3&&Angle==0x10){Substate=4;Animation(7);return;}
                if(Decrement()){Angle=(Angle-1)&31;Counter=6;MooshAnimation();}return;
            case 4:if((int)Position.Y>=0xb0)Replace(0x10,0,new(0x28,0xe8));return;
            default:throw new NotSupportedException($"Moosh credits substate${Substate:x2}.");
        }
    }
    private void AdvanceDimitri()
    {
        switch(Substate)
        {
            case 0:
                Substate=1;if(Counter2==2)Animation(0x1a);
                var jumps=data.Table("companion-jumps");int index=Counter2*3;Angle=jumps[index];SpeedZ=unchecked((short)(jumps[index+1]|jumps[index+2]<<8));return;
            case 1:
                _animation.Advance();Move();if(!Gravity(0x18))return;
                Counter2++;if(Counter2==3){Substate=2;Counter=6;}else{Substate=0;Counter=8;}return;
            case 2:if(Decrement()){Substate=3;Counter=20;Animation(0x27);}return;
            case 3:if(Decrement()){Substate=4;Counter=120;}return;
            case 4:_animation.Advance();if(Decrement()){Substate=5;Counter=60;Angle=0x0b;Speed=0x14;}return;
            case 5:if(Decrement()){Substate=6;Animation(0x26);}return;
            case 6:
                _animation.Advance();Move();if((int)Position.X==0x78){Animation(5);return;}
                if((int)Position.X>=0xb0)Replace(0x0f,1,new(0xd8,0x48));return;
            default:throw new NotSupportedException($"Dimitri credits substate${Substate:x2}.");
        }
    }
    public new void SetTransitionDrawOffset(Vector2 offset)=>base.SetTransitionDrawOffset(offset);
    public override void _Draw()
    {if(_animation is not null)DrawTexture(_animation.CurrentTexture,_animation.CurrentOffset+new Vector2(0,ZFixed>>8)+SourceOamDrawOffset);}
}
