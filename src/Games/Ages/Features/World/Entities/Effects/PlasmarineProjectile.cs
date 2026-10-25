using Godot;
using System;

namespace Oracles;

internal sealed partial class PlasmarineProjectile : TransitionOffsetNode2D
{
    private readonly PlasmarineProfile _data=PlasmarineProfile.Shared;
    private readonly PlasmarineProjectileDatabase _parts=PlasmarineProjectileDatabase.Shared;
    private readonly OracleRoomData _room;
    private readonly OracleRuntimeState _memory;
    private readonly Func<int,PlasmarineCharacter?> _resolve;
    internal int ParentSlot {get;}
    internal int SubId {get;}
    internal int State {get;private set;}
    internal int Counter1 {get;private set;}
    internal int Counter2 {get;private set;}
    internal int Angle {get;private set;}
    internal int Speed {get;private set;}
    internal int InvincibilityCounter {get;set;}
    internal bool PendingCollision {get;private set;}
    internal bool Finished {get;private set;}
    internal bool CollisionEnabled=>State!=0 && !Finished && !PendingCollision;
    internal EnemyAnimationPlayer Animation {get;}
    internal Rect2 CollisionBounds=>new(Position.Floor()-new Vector2(_parts.Properties[2]&15,_parts.Properties[2]>>4),
        new Vector2((_parts.Properties[2]&15)*2,(_parts.Properties[2]>>4)*2));
    internal PlasmarineProjectile(PlasmarineProjectileSpawn spawn,OracleRoomData room,OracleRuntimeState memory,
        Func<int,PlasmarineCharacter?> resolve)
    {
        if(spawn.ColorIndex is not (0 or 1 or 4))throw new NotSupportedException($"plasmarineProjectile.s: unsupported color${spawn.ColorIndex:x2}.");
        Position=spawn.Position.Floor();ParentSlot=spawn.ParentSlot;SubId=spawn.ColorIndex;
        _room=room;_memory=memory;_resolve=resolve;
        var visual=_parts.Visual;Animation=new(this,visual.Animations.Length);
        Animation.Load(EnemyVisualSource.LoadComposite(visual.Sprites),visual.Animations,visual.TileBase,SubId+1,
            sourceGrayscaleInverted:visual.SourceGrayscaleInverted,positionedOam:true);
        Visible=false;ZIndex=ObjectDrawPriority.InFrontOfLinkZIndex;
    }
    internal void PublishCollision()=>PendingCollision=true;
    internal void UpdateFrame(Vector2 target,int frame)
    {
        if(Finished)return;
        if(State!=0 && InvincibilityCounter!=0)InvincibilityCounter-=Math.Sign(InvincibilityCounter);
        if(State!=0 && PendingCollision) {Delete();return;}
        var boss=_resolve(ParentSlot);
        if(boss is null || boss.IsDead) {Delete();return;}
        if(State==0)
        {
            PendingCollision=false;State=1;Speed=_data.PartState[boss.Health<_data.PartState[0]?2:1];
            Counter1=_data.PartState[3];Counter2=_data.PartState[4];Animation.SetAnimation(SubId);
            Angle=OracleObjectMovement.Shared.RelativeAngle(Position,target);Visible=true;return;
        }
        if(SubId!=boss.ColorIndex && RoomEntityManager.ObjectCollisionXYOverlaps(CollisionBounds,boss.CollisionBounds) && boss.InvincibilityCounter==0)
        {boss.TakeOppositeProjectile();Delete();return;}
        if((frame&1)==0)
        {
            if(Counter1!=0)Counter1--;
            if(Counter1==0) {Delete();return;}
            if(Counter1<_data.PartState[5])Visible=!Visible;
            Counter2=(Counter2-1)&255;
            if(Counter2==0)
            {
                Counter2=_data.PartState[6];int toward=OracleObjectMovement.Shared.RelativeAngle(Position,target),delta=(Angle-toward)&31;
                if(delta!=0)Angle=(Angle+(delta<16?-1:1))&31;
            }
        }
        var velocity=NativeObjectMovement.Velocity(_memory,Speed,Angle);
        Position=OracleObjectPosition.FromPixels(Position).Add(velocity.YFixed,velocity.XFixed).PrecisePosition;
        int rounded=(Angle&7)==0?Angle:(Angle&0x18)+4;
        int y=((int)Position.Y+unchecked((sbyte)_data.PartBoundaryOffsets[rounded/2]))&255;
        int x=((int)Position.X+unchecked((sbyte)_data.PartBoundaryOffsets[rounded/2+1]))&255;
        if(x>=_room.Width || y>=_room.Height || _room.GetTerrainInfo(new(x,y)).Collision==255) {Delete();return;}
        Animation.Advance();QueueRedraw();
    }
    private void Delete() {Finished=true;Visible=false;}
    public override void _Draw() {if(!Finished && Visible)DrawTexture(Animation.CurrentTexture,Animation.CurrentOffset+TransitionDrawOffset);}
}
