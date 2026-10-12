using Godot;
using System;

namespace oracleofages;

internal sealed partial class AnglerFishCharacter : EnemyCharacter
{
    private readonly AnglerFishProfile _data=AnglerFishProfile.Shared;
    private AnglerFishEnvironment _world=null!;
    private AnglerFishCharacter? _related;
    private bool _collision=true,_dying;
    private int _lastCollision,_speedZ;
    internal ImportedEnemyDefinition Record {get;private set;}
    internal int State {get;private set;}
    internal int Counter1 {get;set;}
    internal int Counter2 {get;private set;}
    internal int Angle {get;private set;}
    internal int Direction {get;private set;}
    internal int Speed {get;private set;}
    internal int Slot {get;set;}=-1;
    internal int SubId {get;private set;}
    internal int CollisionMode {get;private set;}
    internal bool IsBubble=>Record.Id==EnemyId.AnglerFishBubble;
    internal bool IsAntenna=>!IsBubble && SubId==1;
    internal bool Counted {get;private set;}
    internal bool BossExploded {get;private set;}
    internal override bool InitializationPending=>State==0;
    internal override bool CollisionEnabled=>!IsDead && _collision;
    public override Rect2 CollisionBounds=>new(Position.Floor()-new Vector2(IsAntenna?3:Record.RadiusX,IsAntenna?3:Record.RadiusY),
        new Vector2((IsAntenna?3:Record.RadiusX)*2,(IsAntenna?3:Record.RadiusY)*2));
    internal void Initialize(ImportedEnemyDefinition record,Vector2 position,AnglerFishEnvironment world,bool counted,
        AnglerFishCharacter? related=null)
    {
        Record=record;SubId=record.SubId;_world=world;Counted=counted;_related=related;
        InitializeEnemy(position,EnemyCharacterConfiguration.FromImported(record),positionedOam:true);
        CollisionMode=IsBubble?0x24:0x64;Visible=false;
    }
    internal void InitializeNative(bool scrolling)
    {
        if(State!=0 || IsDead)return;
        _world.Random.Next();
        if(IsBubble)
        {
            if(_related is null)throw new InvalidOperationException("anglerFishBubble.s: missing relatedObj1 ENEMY$76.");
            State=1;Counter1=5;Speed=0x28;
            CopyHigh(_related.Position.Floor()+new Vector2((_related.Direction&1)==0?-12:12,0));
            Visible=true;return;
        }
        _world.InitializeBossRoom(scrolling);
        int initialSpeed=0;
        if((SubId&0x80)!=0) {SubId&=0x7f;initialSpeed=SubId;}
        else if(SubId==0)
        {
            var child=_world.Antenna(this);if(child is null)return;
            _related=child;child._related=this;
            initialSpeed=0xd0+child.Slot;
            if(child.Slot<Slot) {child.SubId=0x80;SubId=1;initialSpeed=1;}
        }
        // The source tail-calls ecom_setSpeedAndState8 with A still holding
        // the child's page (or the role-swap byte); it does not load zero.
        State=8;Speed=initialSpeed;
    }
    internal void UpdateFrame(Player player,Vector2 target,int frame)
    {
        if(IsDead)return;
        try
        {
            if(State==0)
            {
                InitializeNative(false);
                if(IsBubble && State==1)Angle=OracleObjectMovement.Shared.RelativeAngle(Position,target);
                return;
            }
            bool hit=NativeHitPending,recoil=HasActiveKnockback;
            BeginFrame(advanceInvincibility:false,continueDuringHitAndKnockback:true);
            if(IsDead)return;
            if(IsBubble)
            {
                if(hit || recoil || Health==0)PopBubble();
                UpdateBubble();return;
            }
            if(!hit && !recoil && Health==0)
            {
                if(IsAntenna)
                {if(_related is {} body){body.Health=0;body._collision=false;}Finish();}
                else UpdateDeath();
                return;
            }
            if(hit || recoil)
            {
                if(IsAntenna && _related is {} body)body.InvincibilityCounter=InvincibilityCounter;
                else if(_lastCollision==(0x80|ItemCollisionType.ScentSeed))
                {State=13;_collision=false;Direction=(Direction&1)+4;SetAnimation(Direction);_world.Deflation(Position.Floor());}
            }
            if(IsAntenna) {UpdateAntenna(frame);return;}
            switch(State)
            {
                case 8:
                    if(!player.NativeObjectVulnerable || _world.Entry.IsActive)return;
                    _world.DisableLink();_world.Tile(0x80,0x42);_world.Tile(0x90,0x52);
                    State=9;Counter1=_data.State[0];_world.Sound(SoundId.SndDoorClose);return;
                case 9:
                    if(!DecrementCounter())return;
                    State=10;Angle=_data.State[1];Speed=_data.State[2];Visible=true;return;
                case 10:
                    if(!Fall(_data.State[4],_data.State[3]))
                    {if((_speedZ>>8)>=_data.State[5])_speedZ=(_data.State[5]<<8)|(_speedZ&255);return;}
                    _world.BeginBattle();State=11;Counter2=_data.State[7];Bounce();return;
                case 11:
                    Counter2=(Counter2-1)&255;
                    if(Counter2==0 && (int)Position.Y<_data.State[11] && (((int)Position.X-_data.State[12])&255)<_data.State[13])
                    {Counter2=_data.State[7];State=12;Direction+=2;SetAnimation(Direction);}
                    UpdateBounce();return;
                case 12:
                    if(AnimationParameter==0xff) {State=11;Direction-=2;SetAnimation(Direction);}
                    else if(AnimationParameter!=0) {Animation.SetParameter(0);if(_world.Bubble(this))_world.Sound(SoundId.SndFallInHole);}
                    UpdateBounce();return;
                case 13:
                    if(Fall(_data.State[8],6)) {State=14;Counter1=_data.State[9];}return;
                case 14:
                    if(!DecrementCounter()) {AdvanceAnimation();return;}
                    State=15;_speedZ=_data.State[10];return;
                case 15:
                    Fall(_data.State[8],6);
                    if((_speedZ>>8)==0)
                    {State=11;_collision=true;Counter1=_data.State[7];Direction-=4;SetAnimation(Direction);_world.Sound(SoundId.SndPoof);return;}
                    ApplySpeedAndBounce();return;
                default:throw new InvalidOperationException($"anglerFish.s $76:${SubId:x2}: unsupported state${State:x2}.");
            }
        }
        finally {AdvanceInvincibilityCounter();QueueRedraw();}
    }
    private void UpdateAntenna(int frame)
    {
        if(_related is null || _related.IsDead)throw new InvalidOperationException("anglerFish_antenna: lost relatedObj1 ENEMY$76.");
        if(State==8)
        {
            int health=Health,inv=InvincibilityCounter;
            InitializeEnemy(Position,EnemyCharacterConfiguration.FromImported(Record with {TileBase=0x1e,Palette=5}),initialAnimation:6,positionedOam:true);
            Health=health;InvincibilityCounter=inv;State=9;CollisionMode=0x47;Visible=false;return;
        }
        CopyHigh(_related.Position.Floor()+_data.AntennaOffset(_related.Direction));
        if(_related.InvincibilityCounter!=0)Visible=false;
        else if((frame&1)==0)Visible=!Visible;
    }
    private void UpdateBubble()
    {
        if(State==2) {if(DecrementCounter())Finish();else AdvanceAnimation();return;}
        if(_related is null || _related.IsDead) {PopBubble();return;}
        ApplySpeed();
        var probe=EnemyAdjacentWallResolver.Shared.Probe(Position,Angle,WallOrHole);
        Angle=EnemyAdjacentWallResolver.Shared.BounceAngle(Angle,probe);
        if(probe.Bitset!=0 && DecrementCounter()) {PopBubble();return;}
        AdvanceAnimation();
    }
    private void PopBubble()
    {
        State=2;Counter1=8;_collision=false;KnockbackCounter=0;
        if(_world.Random.Next().Value<0x40)_world.ScentDrop(Position.Floor());
        SetAnimation(1);Visible=true;
    }
    private void UpdateBounce()
    {
        if(Fall(_data.State[4],_data.State[3]))
        {
            Bounce();Angle=(_world.Random.Next().Value&0x10)+8;
            int direction=((Angle&0x10)^0x10)>>4;
            if((Direction&1)!=direction) {Direction^=1;SetAnimation(Direction);}
        }
        ApplySpeedAndBounce();
    }
    private void Bounce() {_speedZ=_data.State[6];_world.Sound(SoundId.SndPoof);}
    private void ApplySpeedAndBounce()
    {
        ApplySpeed();var probe=EnemyAdjacentWallResolver.Shared.Probe(Position,Angle,WallOrHole);
        Angle=EnemyAdjacentWallResolver.Shared.BounceAngle(Angle,probe);
        if(probe.Bitset==0)AdvanceAnimation();else {Direction^=1;SetAnimation(Direction);}
    }
    private void ApplySpeed()
    {var position=OracleObjectMovement.Shared.ApplySpeed(OracleObjectMovement.Shared.PositionFromPixels(Position),Speed,Angle);Position=position.PrecisePosition;}
    private bool Fall(int gravity,int offset)
    {
        var position=OracleObjectMovement.Shared.PositionFromPixels(Position);
        bool landed=OracleObjectMovement.Shared.UpdateSpeedZSidescroll(ref position,ref _speedZ,gravity,
            point=>_world.Room.IsSolidForEnemyMovement(point,holesAreWalls:false),offset);
        Position=position.PrecisePosition;return landed;
    }
    private bool WallOrHole(Vector2I point)=>point.X<0 || point.Y<0 || point.X>=_world.Room.Width || point.Y>=_world.Room.Height ||
        _world.Room.IsSolidForEnemyMovement(point,holesAreWalls:true);
    private bool DecrementCounter() {Counter1=(Counter1-1)&255;return Counter1==0;}
    private void CopyHigh(Vector2 point)=>Position=new Vector2(((int)point.X)&255,((int)point.Y)&255)+Position-Position.Floor();
    internal void PublishCollision(int item,int damage=0,int invincibility=0)
    {
        Health=Math.Max(0,Health-damage);if(Health==0)_collision=false;
        if(invincibility!=0)InvincibilityCounter=invincibility;
        _lastCollision=item|0x80;DeferNativeHitStatus();
    }
    internal void PublishSomariaHit(Vector2 origin,int damage)
    {
        PublishCollision(ItemCollisionType.SomariaBlock,damage,0x15);
        KnockbackCounter=0x0b;KnockbackAngle=OracleObjectMovement.Shared.RelativeAngle(Position,origin)^0x10;
        _world.Sound(SoundId.SndDamageEnemy);
    }
    private void UpdateDeath()
    {
        if(!_dying) {_dying=true;_collision=false;Counter1=120;_world.DisableCollisionsAndMenu();_world.Sound(SoundId.SndBossDead);}
        if(!DecrementCounter()) {Visible=!Visible;return;}
        Counter1=1;if(!_world.Explosion(Position.Floor()))return;
        BossExploded=true;_world.RestoreMusic();Finish();
    }
}
