using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>ENEMY_PINCER $45: spawner, head, and three independently dispatched body slots.</summary>
internal sealed partial class PincerCharacter : EnemyCharacter,ITerrainShadowSource
{
    private readonly IReadOnlyList<EnemyBehaviorValue> _data=EnemyBehaviorTables.Shared.PincerState;
    private OracleRandom _random=null!;
    private Func<bool> _spawn=null!;
    private Action<Vector2,ICollection<RoomEntitySpawn>> _dig=null!;
    private Func<PincerCharacter?>? _parent;
    private Vector2 _origin;
    private bool _collision;
    private int _stun,_speedZ;
    internal ImportedEnemyDefinition Record {get;private set;}
    internal int State {get;private set;}
    internal int Counter1 {get;set;}
    internal int Angle {get;private set;}
    internal int Extended {get;private set;}
    internal int ZFixed {get;set;}
    internal int Speed {get;private set;}
    internal int CollisionMode {get;private set;}
    internal bool Spawned {get;private set;}
    internal bool BodyDeleted {get;private set;}
    internal int NativeSlot {get;set;}=-1;
    internal int StunCounter=>_stun;
    internal override bool InitializationPending=>State==0;
    internal override bool CollisionEnabled=>_collision && base.CollisionEnabled;
    internal override void ApplyBoomerangStun(int updates)=>_stun=updates;
    int? ITerrainShadowSource.TerrainShadowZHigh=>ZFixed>>8;
    protected override Vector2 AnimationDrawOffset=>base.AnimationDrawOffset+Vector2.Down*(ZFixed>>8);

    internal void Initialize(ImportedEnemyDefinition record,OracleRoomData room,Vector2 position,OracleRandom random,
        Func<bool> spawn,Action<Vector2,ICollection<RoomEntitySpawn>> dig,Func<PincerCharacter?>? parent=null)
    {
        Record=record;_random=random;_spawn=spawn;_dig=dig;_parent=parent;
        InitializeEnemy(position,EnemyCharacterConfiguration.FromImported(record));Visible=false;
    }
    internal bool Hit(Vector2 origin,int damage)
    {
        if(!TakeDeferredNoKnockbackHit(origin,damage))return false;
        DeferNativeHitStatus();return true;
    }
    internal void BeginEmberHit()
    {Health=Math.Max(0,Health-2);if(Health==0)_collision=false;InvincibilityCounter=-90;_stun=90;DeferNativeHitStatus();}

    internal void UpdateFrame(Vector2 link,int frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(IsDead)return;
        try
        {
            bool hit=NativeHitPending;
            bool stunned=State!=0 && !hit && !HasActiveKnockback && Health>0 && _stun!=0;
            if(stunned)
            {int z=ZFixed;Position=EnemyStunMotion.Update(Position,State,frame,ref _stun,ref z,ref _speedZ);ZFixed=z;}
            if(BeginFrame(advanceInvincibility:false,continueDuringHitAndKnockback:true) || stunned)return;
            if(State==0)
            {
                _random.Next();CollisionMode=_data[6].Value;
                if(Record.SubId==0)State=1;
                else {State=8;Speed=Record.SubId;return;}
            }
            if(Record.SubId==0)
            {
                if(!_spawn())return;
                Spawned=true;Finish();return;
            }
            if(Record.SubId>=2)
            {
                var parent=_parent?.Invoke();
                if(State==8)
                {
                    if(parent is null)throw new InvalidOperationException("pincer.s: initializing body lost relatedObj1 ENEMY$45.");
                    _origin=parent.Position.Floor();State=9;SetAnimation(_data[7].Value);return;
                }
                if(parent is null || parent.IsDead) {BodyDeleted=true;Finish();return;}
                Angle=parent.Angle;InvincibilityCounter=parent.InvincibilityCounter;
                if(parent.State>=11)Visible=parent.Visible;
                Extended=(parent.Extended>>2)*(5-Record.SubId);UpdatePosition();return;
            }
            switch(State)
            {
                case 8:_origin=Position.Floor();State=9;return;
                case 9:
                    int distance=Math.Abs((int)link.Y-(int)Position.Y)+Math.Abs((int)link.X-(int)Position.X);
                    if(distance<_data[1].Value) {State=10;Visible=true;}
                    return;
                case 10:
                    if(AnimationParameter!=1) {AdvanceAnimation();return;}
                    State=11;_collision=true;Extended=0;_dig(Position.Floor(),spawns);
                    Angle=OracleObjectMovement.Shared.RelativeAngle(Position,link);
                    SetAnimation((((Angle+2)&0x1c)>>2)+1);return;
                case 11:
                    UpdatePosition();
                    if(Extended+_data[3].Value>=_data[2].Value) {State=12;Counter1=_data[4].Value;}
                    else Extended+=_data[3].Value;
                    return;
                case 12:if(DecrementCounter())State=13;return;
                case 13:
                    UpdatePosition();
                    if(--Extended==0) {State=14;Counter1=_data[5].Value;_collision=false;Visible=false;}
                    return;
                case 14:
                    if(!DecrementCounter())return;
                    State=9;Position=_origin+Position-Position.Floor();SetAnimation(0);return;
                default:throw new InvalidOperationException($"pincer.s $45:${Record.SubId:x2}: unsupported state${State:x2}.");
            }
        }
        finally {AdvanceInvincibilityCounter();}
    }
    private bool DecrementCounter() {Counter1=(Counter1-1)&255;return Counter1==0;}
    private void UpdatePosition()
    {
        Vector2I offset=MovementCircleArcOffset(Extended,Angle);
        Position=new Vector2(((int)_origin.X+offset.X)&255,((int)_origin.Y+offset.Y)&255)+Position-Position.Floor();
    }
}
