using Godot;
using System;

namespace oracleofages;

internal sealed partial class PlasmarineCharacter : EnemyCharacter,ISwitchHookEnemy,ITerrainShadowSource
{
    private readonly PlasmarineProfile _data=PlasmarineProfile.Shared;
    private PlasmarineEnvironment _world=null!;
    private bool _collision,_dying;
    private int _lastCollision,_swapDone,_speedZ;
    internal ImportedEnemyDefinition Record {get;private set;}
    internal int State {get;private set;}
    internal int Substate {get;private set;}
    internal int Counter1 {get;private set;}
    internal int Counter2 {get;private set;}
    internal int Angle {get;private set;}
    internal int Speed {get;private set;}
    internal Vector2 Target {get;private set;}
    internal int ColorIndex {get;private set;}
    internal int Palette {get;private set;}=1;
    internal int CurrentOamFlags {get;private set;}=1;
    internal int CollisionMode {get;private set;}=0x4f;
    internal int ContactDamageQuarters {get;private set;}=2;
    internal int ProjectileCount {get;private set;}
    internal int Slot {get;set;}=-1;
    internal int ZFixed {get;private set;}
    internal bool BossExploded {get;private set;}
    internal override bool InitializationPending=>State==0;
    internal override bool CollisionEnabled=>!IsDead && _collision;
    internal override Texture2D CurrentDrawTexture=>Animation.CurrentTextureForPalette(CurrentOamFlags);
    protected override Vector2 AnimationDrawOffset=>base.AnimationDrawOffset+Vector2.Down*(ZFixed>>8);
    int? ITerrainShadowSource.TerrainShadowZHigh=>ZFixed>>8;
    internal void Initialize(ImportedEnemyDefinition record,Vector2 position,PlasmarineEnvironment world)
    {
        Record=record;_world=world;
        InitializeEnemy(position,EnemyCharacterConfiguration.FromImported(record),positionedOam:true,paletteVariants:[1,2,4,5]);
        Visible=false;
    }
    internal void InitializeNative()
    {
        if(State!=0)return;
        _world.Random.Next();State=8;Speed=_data.State[0];Angle=_data.State[1];Counter1=_data.State[2];
        Target=new(_data.State[4],_data.State[3]);_world.InitializeRoom();Visible=true;
    }
    internal void UpdateFrame(Vector2 target,int frame)
    {
        if(IsDead)return;
        try
        {
            if(State==0) {InitializeNative();return;}
            bool hit=NativeHitPending,recoil=HasActiveKnockback;
            BeginFrame(advanceInvincibility:false,continueDuringHitAndKnockback:true);
            if(!hit && !recoil && Health==0) {UpdateDeath();return;}
            if((hit || recoil) && CollisionMode!=0x68 && (_lastCollision&0x7f)>=ItemCollisionType.L1Sword)SwapColor();
            switch(State)
            {
                case 3:
                    if(Substate==0) {_swapDone=0;SetAnimation(0);Substate=1;}
                    else if(Substate==2 && _swapDone==0) {_swapDone=1;_world.Sound(SoundId.SndMysterySeed);SwapColor();}
                    else if(Substate==3)
                    {
                        ZFixed=unchecked((short)(ZFixed+_speedZ));
                        if(ZFixed<0) {_speedZ=unchecked((short)(_speedZ+0x20));return;}
                        ZFixed=0;_collision=true;State=10;Counter1=_data.State[5];DecideShocks();
                    }
                    return;
                case 8:
                    if(CloseToTarget()) {CopyHigh(Target);State=9;Counter1=_data.State[5];}
                    else {if(DecrementCounter()) {Counter1=_data.State[2];NudgeAngle(Target);}ApplySpeed();}
                    AdvanceAnimation();return;
                case 9:
                    if(!DecrementCounter()) {AdvanceAnimation();return;}
                    Counter1=_data.State[5];State=10;_collision=true;DecideShocks();_world.BeginBattle();SetAnimation(0);return;
                case 10:
                    if(!DecrementCounter()) {AdvanceAnimation();return;}
                    Counter1=1;
                    if((AnimationParameter&1)!=0)
                    {Counter1=_data.State[6];State=11;Speed=_data.State[7];Target=target.Floor();}
                    AdvanceAnimation();return;
                case 11:
                    if(DecrementCounter())State=12;
                    Speed=(Speed-_data.State[8])&255;
                    goto case 12;
                case 12:
                    if(!CloseToTarget()) {Angle=OracleObjectMovement.Shared.RelativeAngle(Position,Target);ApplySpeed();return;}
                    Counter2=(Counter2-1)&255;
                    if(Counter2!=0)
                    {State=13;Counter1=_data.State[9];ContactDamageQuarters=4;Palette=ColorIndex+4;CollisionMode=0x68;SetAnimation(2);}
                    else {State=14;ProjectileCount=_data.ProjectileCount(Health);SetAnimation(1);}
                    return;
                case 13:
                    if(DecrementCounter())
                    {Counter1=_data.State[5];State=10;_collision=true;ContactDamageQuarters=2;Palette=ColorIndex+1;CollisionMode=0x4f;SetAnimation(0);return;}
                    if((Counter1&15)==0)_world.Sound(SoundId.SndShock);
                    Palette=ColorIndex+(AnimationParameter==0?4:1);AdvanceAnimation();return;
                case 14:
                    AdvanceAnimation();
                    if(AnimationParameter==1)
                    {Animation.SetParameter(0);if(_world.Projectile(this))_world.Sound(SoundId.SndVeranFairyAttack);}
                    else if(AnimationParameter!=0) {State=15;Counter1=_data.State[5];SetAnimation(0);}
                    return;
                case 15:
                    if(!DecrementCounter()) {AdvanceAnimation();return;}
                    ProjectileCount=(ProjectileCount-1)&255;
                    if(ProjectileCount!=0) {State=14;SetAnimation(1);}
                    // @chargeAtLink falls through into the next label.
                    else {State=10;Counter1=_data.State[10];DecideShocks();}
                    return;
                default:throw new NotSupportedException($"plasmarine.s: unsupported state${State:x2}.");
            }
        }
        finally
        {
            AdvanceInvincibilityCounter();
            CurrentOamFlags=InvincibilityCounter>0 && (frame&4)==0 ? (Palette==5?2:5):Palette;
            QueueRedraw();
        }
    }
    private void SwapColor() {ColorIndex^=1;Palette=ColorIndex+1;}
    private void DecideShocks()=>Counter2=(_world.Random.Next().Value&1)+1;
    private bool DecrementCounter() {Counter1=(Counter1-1)&255;return Counter1==0;}
    private bool CloseToTarget()=>(((int)Position.X-(int)Target.X+4)&255)<9 && (((int)Position.Y-(int)Target.Y+4)&255)<9;
    private void CopyHigh(Vector2 point)=>Position=point.Floor()+Position-Position.Floor();
    private void NudgeAngle(Vector2 target)
    {
        int toward=OracleObjectMovement.Shared.RelativeAngle(Position,target),delta=(Angle-toward)&31;
        if(delta!=0)Angle=(Angle+(delta<16?-1:1))&31;
    }
    private void ApplySpeed()=>Position=OracleObjectMovement.Shared.ApplySpeed(
        OracleObjectMovement.Shared.PositionFromPixels(Position),Speed,Angle).PrecisePosition;
    internal void PublishCollision(int item,int invincibility=0)
    {_lastCollision=item|0x80;if(invincibility!=0)InvincibilityCounter=invincibility;DeferNativeHitStatus();}
    internal void PublishShock() {_collision=false;PublishCollision(0x1f);}
    internal void TakeOppositeProjectile()
    {InvincibilityCounter=24;Health=(Health-1)&255;if(Health==0)_collision=false;_world.Sound(SoundId.SndBossDamage);}
    private void UpdateDeath()
    {
        if(!_dying) {_dying=true;_collision=false;Counter1=120;_world.DisableLinkCollisionsAndMenu();_world.Sound(SoundId.SndBossDead);}
        if(!DecrementCounter()) {Visible=!Visible;return;}
        Counter1=1;if(!_world.Explosion(Position.Floor()))return;
        BossExploded=true;Finish();
    }
    public bool SwitchHookHeld=>GodotObject.IsInstanceValid(this) && !IsDead && State==3 && Substate<3;
    public Vector2 SwitchHookPosition=>Position;
    public void BeginSwitchHook(Vector2 linkPosition) {State=3;Substate=0;KnockbackCounter=0;_collision=false;}
    public void CopySwitchHookPosition(Vector2 position,int zHigh) {CopyHigh(position);ZFixed=(zHigh<<8)|(ZFixed&255);QueueRedraw();}
    public void SwapSwitchHook()=>Substate=2;
    public void ReleaseSwitchHook() {if(SwitchHookHeld)Substate=3;}
}
