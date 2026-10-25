using Godot;
using System;

namespace Oracles;

/// <summary>ENEMY_IRON_MASK $1c, including the detached mask and unmasked child.</summary>
internal sealed partial class IronMaskCharacter : EnemyCharacter, ISwitchHookEnemy, ITerrainShadowSource
{
    private readonly EnemyBehaviorTables _data = EnemyBehaviorTables.Shared;
    private OracleRandom _random = null!;
    private EnemyTerrainMovement _movement = null!;
    private Func<IronMaskCharacter,bool> _createChild = null!;
    private Action<int> _sound = null!;
    private bool _collision = true;
    private int _speedZ;
    private int _stun;
    internal int StunCounter=>_stun;
    internal override void ApplyBoomerangStun(int updates)=>_stun=updates;
    internal void ApplyBump(Vector2 origin,EnemyKnockbackStrength strength)
    { ApplyCollisionBump(origin,strength); DeferNativeHitStatus(); }
    internal override bool TakeSwordHit(Vector2 origin,int damage)
    {
        if(!base.TakeSwordHit(origin,damage))return false;
        DeferNativeHitStatus();return true;
    }
    internal ImportedEnemyDefinition Record { get; private set; }
    internal int State { get; private set; }
    internal int Substate { get; private set; }
    internal int Counter1 { get; set; }
    internal int Angle { get; private set; }
    internal int Direction { get; private set; }
    internal int Speed => _data.IronMaskState[0].Value;
    internal int CollisionMode { get; private set; }
    internal int ZFixed { get; set; }
    internal bool MaskDetached { get; private set; }
    internal bool MaskExpired { get; private set; }
    internal void BeginEmberHit()
    {
        Health=Math.Max(0,Health-2); if(Health==0)_collision=false;
        InvincibilityCounter=-90;_stun=90;DeferNativeHitStatus();
    }
    internal void ClearSeedStun()=>_stun=0;
    internal override bool InitializationPending => State == 0;
    internal override bool CollisionEnabled => _collision && State != 3 && base.CollisionEnabled;
    int? ITerrainShadowSource.TerrainShadowZHigh => ZFixed >> 8;
    protected override Vector2 AnimationDrawOffset => base.AnimationDrawOffset + Vector2.Down * (ZFixed >> 8);

    internal void Initialize(ImportedEnemyDefinition record,OracleRoomData room,Vector2 position,
        OracleRandom random,Action<int> sound,Func<IronMaskCharacter,bool> createChild,int knockbackAngle=0)
    {
        Record=record; _random=random; _sound=sound; _createChild=createChild;
        InitializeEnemy(position,EnemyCharacterConfiguration.FromImported(record));
        KnockbackAngle=knockbackAngle;
        _movement=new(this,room);
        ConfigureHazards(room,zPosition:()=>ZFixed);
        ConfigureSwordKnockback(room,EnemyKnockbackMotion.Terrain,checksHazards:true,nativeSpeed:()=>Speed);
        Visible=false;
    }

    internal void InitializeState()
    {
        if (State != 0) return;
        _random.Next(); State=8; Counter1=(Counter1+_data.IronMaskState[1].Value)&255; Visible=true;
        CollisionMode=_data.IronMaskState[2].Value;
        if (Record.SubId == 0) return;
        CollisionMode=_data.IronMaskState[3].Value;
        KnockbackCounter=_data.IronMaskState[6].Value;
        InvincibilityCounter=unchecked((sbyte)_data.IronMaskState[7].Value);
        SetAnimation(4);
    }

    internal void UpdateFrame(Vector2 target,int frame)
    {
        try
        {
        bool stunned=State!=0 && !NativeHitPending && !HasActiveKnockback && Health>0 && _stun!=0;
        if(stunned)
        {
            int z=ZFixed;Position=EnemyStunMotion.Update(Position,State,frame,ref _stun,ref z,ref _speedZ);ZFixed=z;
        }
        if (CheckHazards() || BeginFrame(advanceInvincibility:false,continueDuringHitAndKnockback:NativeHitPending) || stunned) return;
        if (State == 0) { InitializeState(); return; }
        if (State == 3)
        {
            if (Substate == 0)
            {
                bool detached=Record.SubId == 0 && CollisionMode == _data.IronMaskState[2].Value && _createChild(this);
                if (detached) { MaskDetached=true; SetAnimation(5); _sound(SoundId.SndBombLand); }
                Counter1=_data.IronMaskState[detached ? 4 : 5].Value; Substate=1;
            }
            else if (Substate == 3)
            {
                ZFixed=(short)(ZFixed+_speedZ);
                if (ZFixed < 0) _speedZ=(short)(_speedZ+_data.IronMaskState[8].Value);
                else
                {
                    ZFixed=0;
                    State=Record.SubId == 0 && CollisionMode == _data.IronMaskState[2].Value ? 10 : 8;
                    if (State == 10) _collision=false;
                }
            }
            return;
        }
        if (Record.SubId != 0)
        {
            if (DecrementCounter()) ChooseWalk(target);
            _movement.MoveAtAngle(Angle,Speed,false); AdvanceAnimation(); return;
        }
        switch (State)
        {
            case 8:
                if (!DecrementCounter()) { UpdateCollisionMode(target); return; }
                State=9; ChooseWalk(target);
                goto case 9;
            case 9:
                if (DecrementCounter()) { State=8; Counter1=_data.IronMaskStandCounters[_random.Next().Value&3].Value; }
                _movement.MoveAtAngle(Angle,Speed,false); UpdateCollisionMode(target); AdvanceAnimation(); return;
            case 10:
                if (DecrementCounter()) { MaskExpired=true; Finish(); }
                else Visible=!Visible;
                return;
            default: throw new InvalidOperationException($"ironMask.s $1c:${Record.SubId:x2}: state${State:x2} unsupported.");
        }
        }
        finally {AdvanceInvincibilityCounter();}
    }

    private bool DecrementCounter() { Counter1=(Counter1-1)&255; return Counter1==0; }
    private void ChooseWalk(Vector2 target)
    {
        var roll=_random.Next(); Counter1=_data.IronMaskWalkCounters[roll.High&7].Value;
        Angle=Record.SubId == 0 && (roll.Low&3)==0
            ? (OracleObjectMovement.Shared.RelativeAngle(Position,target)+4)&ObjectAngle.CardinalMask
            : _random.NextCardinalAngle();
        if (Record.SubId != 0 || Direction == Angle>>3) return;
        Direction=Angle>>3; SetAnimation(Direction);
    }
    private void UpdateCollisionMode(Vector2 target)
    {
        int delta=(OracleObjectMovement.Shared.RelativeAngle(Position,target)-Angle)&31;
        CollisionMode=_data.IronMaskState[((delta-12)&255)<9 ? 3 : 2].Value;
    }
    public bool SwitchHookHeld => GodotObject.IsInstanceValid(this) && !IsDead && !DiedInHazard && State==3 && Substate<3;
    public Vector2 SwitchHookPosition => Position;
    public void BeginSwitchHook(Vector2 linkPosition)
    {
        _stun=KnockbackCounter=0; KnockbackAngle=OracleObjectMovement.Shared.RelativeAngle(Position.Floor(),linkPosition.Floor())^16;
        State=3; Substate=0;
    }
    public void CopySwitchHookPosition(Vector2 position,int zHigh)
    { Position=position.Floor()+Position-Position.Floor(); ZFixed=(zHigh<<8)|(ZFixed&255); QueueRedraw(); }
    public void SwapSwitchHook() => Substate=2;
    public void ReleaseSwitchHook() { if (SwitchHookHeld) Substate=3; }
}
