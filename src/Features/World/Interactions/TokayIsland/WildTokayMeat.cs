using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// INTERAC_TOKAY_MEAT $8c; the reserved ITEM C owns thrown physics.
internal partial class WildTokayMeat : TransitionOffsetNode2D
{
    private readonly List<WildTokayMeatFrame> _frames=new();
    private WildTokayMeatDatabase _data=null!;
    private OracleRoomData _room=null!;
    private BombRecord _throwing=null!;
    private Action<int> _sound=null!;
    private RoomEntityManager _world=null!;
    private Player? _holder;
    private NativeBraceletThrow? _throw;
    private readonly BraceletGrabGeometry _grab=new();
    private int _z,_speedZ;
    internal int State {get;private set;}
    internal int Substate {get;private set;}
    internal int Counter {get;private set;}
    internal bool Lifted=>State==2&&Substate<2;
    internal bool Thrown=>_throw?.Active==true;
    internal bool Finished {get;private set;}
    internal int ZFixed=>_z;
    internal int SpeedZ=>_speedZ;
    internal NativeBraceletThrow? ReservedThrow=>_throw;
    internal void BindWorld(RoomEntityManager world)=>_world=world;
    internal void Initialize(WildTokayMeatDatabase database,OracleRoomData room,BombRecord throwing,Action<int> soundRequested)
    {
        _data=database;_room=room;_throwing=throwing;_sound=soundRequested;Position=new(database.StartX,database.StartY);Visible=false;
        Image image=OracleGraphicsCache.LoadImage(AgesGameContent.AssetRoot + $"gfx/{database.Sprite}.png");
        foreach(var frame in OracleGraphicsCache.GetAnimationDefinition(database.Animation).Frames)
        {
            var (texture,offset)=OracleOamCompositor.BuildPositionedOamTexture(image,frame.EncodedOam,database.TileBase,database.Palette,null,true);
            _frames.Add(new(texture,offset));
        }
        TreeExiting+=Finish;
    }
    internal bool TryUseBracelet(Player player,Vector2I direction)
    {
        if(Finished)return false;
        if(_holder==player)
        {
            if(_throw is null)throw new InvalidOperationException("INTERAC$8c release precedes native state2 setup.");
            _throw.Begin(CarriedObjectMotion.DirectionIndex(player.FacingVector),direction==Vector2I.Zero?0xff:CarriedObjectMotion.DirectionIndex(direction)*8,RingEffects.UsesStrongThrow(player.Inventory));
            player.EndCarriedObjectPose();_holder=null;return true;
        }
        if(State!=1||player.IsCarryingObject||!player.IsOnGroundForBracelet)return false;
        if(!_grab.Overlaps(new(player.EnemyContactPosition-new Vector2(6,6),new(12,12)),player.EnemyContactZ,CarriedObjectMotion.DirectionIndex(player.FacingVector),new(Position-new Vector2(8,8),new(16,16)),_z>>8,false))return false;
        State=2;Substate=0;_holder=player;player.BeginCarriedObjectPose();
        _throw=new(_room,0,()=>!Finished&&Lifted,()=>!Finished&&State==2&&Substate<3,()=>Position,()=>_z,
            CopyPosition,_=>Substate=2,()=>Substate=3,
            (position,speed,angle)=>{var velocity=NativeObjectMovement.Velocity(_world.RuntimeState,speed,angle);return position.Add(velocity.YFixed,velocity.XFixed);},
            BraceletWeightDatabase.Shared,_throwing);
        return true;
    }
    internal void UpdateFrame(Player player,int frameCounter=0)
    {
        if(Finished)return;
        switch(State)
        {
            case 0:State=1;Counter=_data.FallDelay;_z=_data.StartZ<<8;_speedZ=0;return;
            case 1:
                switch(Substate)
                {
                    case 0:if(Counter!=0){Counter--;return;}Substate=1;Visible=true;ZIndex=ObjectDrawPriority.InFrontOfLinkZIndex;_sound(_data.SoundFall);return;
                    case 1:
                        if(OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,_data.FallGravity)){Substate=2;_sound(_data.SoundLand);}
                        QueueRedraw();return;
                    case 2:ZIndex=ObjectDrawPriority.RelativeToLink(Position.Y,player.Position.Y);return;
                    default:throw new InvalidOperationException($"INTERAC$8c state1/substate${Substate:x2}.");
                }
            case 2:
                if(Substate==0)
                {
                    // justGrabbed republishes the pointer and retries its
                    // checked replacement allocation until it succeeds.
                    _world.RuntimeState.SetWramByte(0xcfda,(byte)(0xd0+_world.InteractionSlot(this)));
                    _world.RuntimeState.SetWramByte(0xcfdb,0x45);
                    if(_world.TryCreateInteraction(new WildTokayMeatSpawn()) is not null)Substate=1;
                    return;
                }
                if(Substate==1)return;
                if(Substate!=2)throw new InvalidOperationException($"INTERAC$8c unsupported grabbed substate${Substate:x2}.");
                if((_z>>8)<0)return;
                // dropLinkHeldItem clears Link's grab bytes, but ITEM C
                // remains enabled until its following item-phase check.
                State=3;Counter=_data.DropLife;ZIndex=ObjectDrawPriority.FixedLowPriorityZIndex;return;
            case 3:
                if(--Counter==0){Finish();return;}Visible=(frameCounter&1)!=0;ZIndex=ObjectDrawPriority.RelativeToLink(Position.Y,player.Position.Y);return;
            default:throw new InvalidOperationException($"INTERAC$8c unsupported state${State:x2}.");
        }
    }
    internal void UpdateBraceletChild(Player player)
    {
        if(_holder is not null&&!_holder.IsCarryingObject){_holder=null;Substate=2;}
        _throw?.Update(_sound);
    }
    internal void UpdateHeldPosition(Player player)
    {
        if(_holder!=player||!Lifted)return;
        if(player.BraceletEntityOffset is Vector2I offset)CopyPosition(new((byte)((int)player.Position.X+offset.X),(byte)(int)player.Position.Y),unchecked((sbyte)(player.EnemyContactZ+offset.Y)));
        else _throw?.Hold(player.Position,player.EnemyContactZ,player.CarriedObjectAnimationFrame==0?2:3,CarriedObjectMotion.DirectionIndex(player.FacingVector));
    }
    private void CopyPosition(Vector2 position,int zHigh){Position=position.Floor();_z=zHigh<<8;QueueRedraw();}
    internal void CancelReservedThrow()=>_throw?.Cancel();
    internal void Catch()=>Finish();
    internal void Finish()
    {
        if(Finished)return;_holder?.EndCarriedObjectPose();_holder=null;CancelReservedThrow();Finished=true;Visible=false;
    }
    public override void _Draw()
    {if(!Finished&&Visible&&_frames.Count!=0)DrawTexture(_frames[0].Texture,_frames[0].Offset+new Vector2(0,_z>>8)+SourceOamDrawOffset);}
}

internal sealed record WildTokayMeatFrame(Texture2D Texture,Vector2 Offset);
