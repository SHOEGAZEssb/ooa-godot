using Godot;

namespace oracleofages;

// ENEMY $1f uses common status/death/recoil while its own AI is a short
// launch, pause and straight pursuit. It has no parent-lifetime deletion gate.
internal sealed partial class VeranChildBeeCharacter : EnemyCharacter
{
    private readonly RoomEntityManager _owner;
    private readonly VeranFinalDatabase _data;
    private int _stun,_z,_speedZ;
    private bool _burnCollisionDisabled;
    internal int StunCounter=>_stun;
    internal int BurnZFixed {get=>_z;set=>_z=value;}
    internal bool NativeDeath {get;private set;}
    internal override bool CollisionEnabled=>base.CollisionEnabled&&!_burnCollisionDisabled;
    protected override Vector2 AnimationDrawOffset=>base.AnimationDrawOffset+Vector2.Down*(_z>>8);
    internal int SubId {get;}
    internal int State {get;private set;}
    internal int Counter {get;private set;}
    internal int Angle {get;private set;}
    internal int Speed {get;private set;}
    internal bool FlewOut {get;private set;}
    internal override bool InitializationPending=>State==0;
    internal VeranChildBeeCharacter(RoomEntityManager owner,VeranFinalDatabase data,int subid,Vector2 position,OracleRoomData room)
    {
        _owner=owner;_data=data;SubId=subid;
        InitializeEnemy(position.Floor(),EnemyCharacterConfiguration.FromImported(data.Actor(0x1f)),paletteOverrides:data.Palettes,positionedOam:true);
        ConfigureSwordKnockback(room,EnemyKnockbackMotion.ScreenBoundary,nativeSpeed:()=>Speed);Visible=false;
    }
    internal override bool TakeSwordHit(Vector2 origin,int damage)
    {bool accepted=base.TakeSwordHit(origin,damage);if(accepted)DeferNativeHitStatus();return accepted;}
    internal void UpdateFrame(RoomEntityFrame frame)
    {
        if(IsDead)return;
        // enemyStandardUpdate handles stun before dispatching enemyCode1f;
        // JUST_HIT and recoil take precedence over that shared motion.
        bool stunned=State!=0&&!NativeHitPending&&!HasActiveKnockback&&Health>0&&_stun!=0;
        if(stunned)Position=EnemyStunMotion.Update(Position,State,frame.Counter,ref _stun,ref _z,ref _speedZ);
        if(BeginFrame()||stunned)return;
        if(Health==0){NativeDeath=true;Finish();return;}
        switch(State)
        {
            case 0:
                _owner.Random.Next();State=8;Speed=0x50;Counter=16;Angle=_data.Bytes("child-angles")[SubId];
                Visible=true;ZIndex=ObjectDrawPriority.InFrontOfLinkZIndex;return;
            case 8:
                Counter=(Counter-1)&255;
                if(Counter==0){Counter=12;State=9;}else Move();AdvanceAnimation();return;
            case 9:
                Counter=(Counter-1)&255;if(Counter!=0){AdvanceAnimation();return;}
                State=10;Angle=OracleObjectMovement.Shared.RelativeAngle(Position.Floor(),(frame.EnemyTarget??frame.Player.Position).Floor());goto case 10;
            case 10:
                Move();Vector2 point=Position.Floor();
                if(((int)point.Y&255)<176&&((int)point.X&255)<240){AdvanceAnimation();return;}
                FlewOut=true;Finish();return;
            case >=1 and <=7:return;
            default:throw new System.NotSupportedException($"veranChildBee.s: ENEMY $1f:${SubId:x2}, state ${State:x2}.");
        }
    }
    private void Move()=>Position=ApplyMovementSpeed(OracleObjectPosition.FromPixels(Position),Speed,Angle).PrecisePosition;
    internal void BeginEmberHit(int damage)
    {
        Health=System.Math.Max(0,Health-damage);_burnCollisionDisabled=Health==0;
        _stun=90;InvincibilityCounter=-90;DeferNativeHitStatus();
    }
    internal void ReleaseBurn(){InvincibilityCounter=0;_stun=1;}
    internal void BeginPegasusHit(){_stun=240;InvincibilityCounter=-16;}
    internal void BeginGaleHit(){_stun=0;State=5;Speed=5;}
    internal void ApplyItemBump(Vector2 origin)
    {ApplyCollisionBump(origin,EnemyKnockbackStrength.Normal);DeferNativeHitStatus();}
}
