using Godot;
using System.Collections.Generic;

namespace oracleofages;

// PART $58. Its contact response suppresses swords and alternate movement
// updates via the existing one-pass restriction handoff used by attached Gels.
internal sealed partial class VeranBeeProjectileRoomEntity : EnemyCharacter,IRoomEntity,IFixedRoomEntity,
    IRoomEntityLifetime,IPostObjectLinkContactRoomEntity,INativePartHealthRoomEntity,IObjectCollisionHeightRoomEntity,
    IPlayerRestriction,IAfterPlayerUpdateRoomEntity
{
    private readonly RoomEntityManager _owner;
    private readonly VeranFinalDatabase _data;
    private readonly int _parentSlot;
    private bool _collision=true,_hit,_swordRestriction,_movementRestriction;
    private int _z;
    internal int State {get;private set;}
    internal int Counter {get;private set;}
    internal int Angle {get;private set;}
    internal int Speed {get;private set;}
    internal int ZFixed=>_z;
    internal override bool InitializationPending=>State==0;
    internal override bool CollisionEnabled=>!IsDead&&_collision;
    protected override Vector2 AnimationDrawOffset=>base.AnimationDrawOffset+Vector2.Down*(_z>>8);
    public Node2D Node=>this;
    public bool Finished=>IsDead;
    public int CollisionZ=>_z>>8;
    public bool DisablesSword=>_swordRestriction;
    public bool DisablesMovement=>_movementRestriction;
    public bool AlternatesMovementWithSwordRestriction=>false;
    internal VeranBeeProjectileRoomEntity(RoomEntityManager owner,VeranFinalDatabase data,int parentSlot,Vector2 position,int z)
    {
        _owner=owner;_data=data;_parentSlot=parentSlot;_z=(z>>8)<<8;
        InitializeEnemy(position.Floor(),EnemyCharacterConfiguration.FromImported(data.Actor(0x58)),positionedOam:true);Visible=false;
    }
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset)=>SetTransitionDrawOffset(offset);
    public void ClearHealthAndCollision(){Health=0;_collision=false;}
    public void AfterPlayerUpdate(){_swordRestriction=_movementRestriction=false;}
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        try{UpdateCore(frame);}finally{AdvanceInvincibilityCounter();}
    }
    private void UpdateCore(RoomEntityFrame frame)
    {
        if(_hit){_hit=false;_collision=false;State=3;Counter=240;Visible=false;}
        if(State==0){State=1;Angle=16;Speed=0x78;Counter=9;_owner.OnSoundRequested(SoundId.SndBeam);Visible=true;ZIndex=ObjectDrawPriority.InFrontOfLinkZIndex;}
        switch(State)
        {
            case 1:
                if(Dec()!=0)
                {
                    if(_owner.ResolveEnemySlot(_parentSlot)?.Node is not VeranFinalCharacter parent)
                        throw new System.NotSupportedException("veranBeeProjectile.s:$58 has no live enemy relatedObj1.");
                    Position=new((int)parent.Position.X+Position.X%1,(((int)parent.Position.Y+20)&255)+Position.Y%1);_z=(parent.ZFixed&0xff00)|(_z&255);return;
                }
                State=2;goto case 2;
            case 2:
                Position=ApplyMovementSpeed(OracleObjectPosition.FromPixels(Position),Speed,Angle).PrecisePosition;
                if(Position.Y>=176)Finish();return;
            case 3:
                if(Dec()==0){Finish();return;}
                if(frame.AnyButtonJustPressed)Counter=Counter>=10?Counter-10:1;
                _swordRestriction=true;_movementRestriction=(frame.Counter&1)!=0;return;
        }
    }
    private int Dec()=>Counter=Counter==0?0:Counter-1;
    public void HandleLinkContact(Player player)
    {
        if(!CollisionEnabled||_hit||InvincibilityCounter!=0||!player.EnemyContactHeightOverlaps(CollisionZ))return;
        if(player.CanAcceptShieldCollision&&_data.Active(true,0x58,player.Inventory.ShieldLevel)&&player.TryBlockWithShield(CollisionBounds))
        {
            // Mode $06 uses effect $1f: LINKDMG_20 has sound but no recoil;
            // ENEMYDMG_34 suppresses further PART collisions for $1c updates.
            InvincibilityCounter=-28;return;
        }
        if(player.OverlapsEnemyCollision(CollisionBounds,CollisionZ)&&player.ApplyEnemyContactDamage(Position,_data.Actor(0x58).DamageQuarters,
            RingDamageSource.Generic,34,15))_hit=true;
    }
}
