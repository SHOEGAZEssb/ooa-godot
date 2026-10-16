using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// ENEMY_VERAN_FINAL_FORM $02: transformations retain this enemy slot and
// restore its saved turtle health when a temporary form is defeated.
internal sealed partial class VeranFinalCharacter : EnemyCharacter
{
    internal VeranFinalEnvironment World {get;}
    internal VeranFinalDatabase Data=>World.Data;
    private readonly EnemyTerrainMovement _terrain;
    private int _z,_speedZ,_palette=6;
    private bool _collision=true,_hit;
    internal int SubId {get;private set;}
    internal int State {get;private set;}
    internal int Substate {get;private set;}
    internal int Counter {get;private set;}
    internal int Counter2 {get;private set;}
    internal int Speed {get;private set;}
    internal int Angle {get;private set;}
    internal int CollisionMode {get;private set;}=0x6a;
    internal int CollisionType {get;private set;}=2;
    internal int TurtleHealth {get;private set;}
    internal int SpiderHealth {get;private set;}
    internal int BeeHealth {get;private set;}
    internal int TurtleHit {get;private set;}
    internal int JumpCount {get;private set;}
    internal int TransformIndex {get;private set;}
    internal Vector2I Target {get;private set;}
    internal int WebSignal {get;set;}
    internal int Corner {get;private set;}
    internal int Attack {get;private set;}
    internal int ZFixed=>_z;
    internal int SpeedZ=>_speedZ;
    internal int Slot {get;set;}
    internal int StunCounter {get;private set;}
    internal void SignalWebArrived(bool caught)
    {WebSignal=(WebSignal+1)&255;if(caught)Counter=8;}
    internal bool JustHit=>_hit;
    internal override bool InitializationPending=>State==0;
    internal override bool CollisionEnabled=>!IsDead&&_collision;
    protected override Vector2 AnimationDrawOffset=>base.AnimationDrawOffset+Vector2.Down*(_z>>8);
    internal override Texture2D CurrentDrawTexture=>DrawsDamagePalette?base.CurrentDrawTexture:Animation.CurrentTextureForPalette(_palette);
    internal VeranFinalCharacter(VeranFinalEnvironment world,Vector2 position)
    {
        World=world;_terrain=new(this,world.Room);
        InitializeEnemy(position.Floor(),EnemyCharacterConfiguration.FromImported(Data.Actor(2)),
            paletteOverrides:Data.Palettes,positionedOam:true,paletteVariants:[6,7]);
        ConfigureSwordKnockback(world.Room,EnemyKnockbackMotion.Terrain,nativeSpeed:()=>Speed);Visible=false;
    }
    internal void PublishHit(Vector2 origin,int damage,int invincibility=0,EnemyKnockbackStrength knockback=EnemyKnockbackStrength.None)
    {
        _hit=true;Health=Math.Max(0,Health-damage);
        if(knockback!=EnemyKnockbackStrength.None)ApplySwordKnockback(origin,knockback);
        else if(invincibility!=0)InvincibilityCounter=invincibility;
        if(Health==0)_collision=false;
    }
    internal void PublishBump(Vector2 origin,int invincibility,int recoil)
    {_hit=true;ApplyCollisionBump(origin,invincibility,recoil);}
    internal void PublishBump(Vector2 origin,EnemyKnockbackStrength strength)
    {_hit=true;ApplyCollisionBump(origin,strength);}
    internal void UpdateFrame(RoomEntityFrame frame)
    {
        if(IsDead)return;
        try
        {
            if(State==0)World.Entities.Random.Next(); // enemyStandardUpdate initializes var3d on each retry.
            if(_hit)
            {
                if(SubId==0){if(InvincibilityCounter!=0)TurtleHit=1;}
                else if(KnockbackCounter!=0)
                {
                    State=3;Counter=105;CollisionMode=0x6b;
                    if(frame.Player.EnemyGrabActive)frame.Player.ReleaseVeranSpiderGrab();
                    RestartAnimation(6);return;
                }
            }
            else if(HasActiveKnockback)
            {
                OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x20);BeginFrame(advanceInvincibility:false);return;
            }
            else if(Health==0)TransformOrDie();
            switch(SubId)
            {
                case 0:Turtle(frame);return;
                case 1:Spider(frame);return;
                case 2:Bee(frame);return;
                default:throw new NotSupportedException($"veranFinalForm.s: subid ${SubId:x2}.");
            }
        }
        finally {_hit=false;AdvanceInvincibilityCounter();QueueRedraw();}
    }
    private void Turtle(RoomEntityFrame frame)
    {
        switch(State)
        {
            case 0:
                World.Entities.SetNativeObjectPalette(Data.Palettes);World.Entities.OnSoundRequested(SoundId.SndCtrlStopMusic);Input(false);
                if(!World.Entities.TryCreatePart(new BossShadowSpawn(()=>Position,()=>_z>>8,()=>!IsDead,2,8)))return;
                State=1;bool linked=World.Entities.SaveData!.IsLinkedGame;
                if(!linked)Health=20;TurtleHealth=Health;SpiderHealth=linked?12:8;BeeHealth=linked?24:15;
                Visible=true;ZIndex=ObjectDrawPriority.InFrontOfLinkZIndex;return;
            case 1:
                switch(Substate)
                {
                    case 0:if(World.Entities.PaletteFadeActiveSource())return;
                        World.Entities.OnSoundRequested(SoundId.SndLightning);Text(0x5614);Substate=1;return;
                    case 1:Substate=2;Input(true);RestartAnimation(3);
                        World.Entities.SetNativeActiveMusic(SoundId.MusFinalBoss);World.Entities.OnSoundRequested(SoundId.MusFinalBoss);return;
                    case 2:AdvanceAnimation();if(AnimationParameter!=255)return;
                        State=2;Counter=30;Speed=0x46;RestartAnimation(1);return;
                    default:throw Unsupported();
                }
            case 2:
                if(AnimationParameter!=0){AdvanceAnimation();return;}
                if(Dec1()!=0)return;State=3;_speedZ=-0x400;Toward(frame.EnemyTarget??frame.Player.Position);
                ZIndex=ObjectDrawPriority.FixedLowPriorityZIndex;World.Entities.OnSoundRequested(SoundId.SndUnknown4);RestartAnimation(2);return;
            case 3:
                OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x20);
                if(_speedZ!=0){_terrain.MoveUsingAdjacentWalls(Angle,Speed,allowHoles:false,topDown:true);return;}
                _speedZ=0x100;State=4;Speed=0x78;
                var target=(frame.EnemyTarget??frame.Player.Position).Floor();Target=new(((int)target.X&0xf0)+8,((int)target.Y&0xf0)+8);RestartAnimation(1);return;
            case 4:
                if(!OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x10)){if(MoveTowardTarget())SnapTarget();return;}
                World.Entities.BeginScreenShake(16);State=5;Counter=12;ZIndex=ObjectDrawPriority.InFrontOfLinkZIndex;
                World.Entities.OnSoundRequested(SoundId.SndPoof);World.SpawnPart(this,0x57,0);return;
            case 5:
                if(Dec1()!=0)return;Speed=0x46;if((TurtleHit&1)!=0)JumpCount=(JumpCount+1)&255;
                var probabilities=Data.Bytes("transform-probability");
                if((uint)JumpCount>=probabilities.Length)throw new NotSupportedException($"veranFinalForm.s: transform probability index ${JumpCount:x2} exceeds source table.");
                int mask=probabilities[JumpCount];
                if(mask!=255)
                {
                    if((World.Entities.Random.Next().Value&mask)==0){BeginTransformation();return;}
                    if((TurtleHit&1)!=0){JumpAgain();return;}
                }
                if(World.Entities.Random.Next().Value>=90){JumpAgain();return;}
                State=6;Counter=8;World.Entities.OnSoundRequested(SoundId.SndGoron);RestartAnimation(4);return;
            case 6:
                AdvanceAnimation();if((AnimationParameter&128)==0){if((AnimationParameter&1)!=0)CollisionMode=9;return;}
                State=7;Counter=90;RestartAnimation(0);return;
            case 7:if(Dec1()!=0){AdvanceAnimation();return;}State=8;RestartAnimation(3);return;
            case 8:
                AdvanceAnimation();if((AnimationParameter&128)==0){if((AnimationParameter&1)!=0)CollisionMode=0x6a;return;}
                State=2;Counter=30;RestartAnimation(1);return;
            case 9:
                AdvanceAnimation();if(AnimationParameter!=255)return;TurtleHit=JumpCount=0;State=8;
                CollisionType=2;CollisionMode=9;_collision=true;_palette=6;RestartAnimation(3);return;
            case 10:Death(frame.Player);return;
            default:throw Unsupported();
        }
    }
    private void BeginTransformation()
    {
        State=1;CollisionType=0x16;TurtleHealth=Health;_palette=7;
        int bit=TransformIndex*4+(World.Entities.Random.Next().Value&3);TransformIndex=(TransformIndex+1)&7;
        SubId=(Data.Bytes("transform-sequence")[bit>>3]&(1<<(bit&7)))==0?1:2;
        RestartAnimation(SubId+9);World.Entities.OnSoundRequested(SoundId.SndTransform);
    }
    private void TransformOrDie()
    {
        if(SubId==0){State=10;Substate=0;Health=1;World.Entities.OnSoundRequested(SoundId.SndCtrlStopMusic);return;}
        int previous=SubId;SubId=0;State=9;Health=TurtleHealth;CollisionType=0x16;_collision=true;
        SetNativeCollisionRadii(10,8);_palette=7;RestartAnimation(previous+7);World.Entities.OnSoundRequested(SoundId.SndTransform);
    }
    private void JumpAgain(){State=2;Counter=30;}
    private void InitializeForm(int health,int mode,int yRadius,int xRadius)
    {Health=health;CollisionType=2;CollisionMode=mode;_collision=true;SetNativeCollisionRadii(xRadius,yRadius);_palette=6;}
    private int Dec1()=>Counter=(Counter-1)&255;
    private int Dec2()=>Counter2=(Counter2-1)&255;
    private void Move()=>Position=ApplyMovementSpeed(OracleObjectPosition.FromPixels(Position),Speed,Angle).PrecisePosition;
    private void Toward(Vector2 target)=>Angle=OracleObjectMovement.Shared.RelativeAngle(Position.Floor(),target.Floor());
    private bool MoveTowardTarget()
    {
        if((((int)Position.X-Target.X+2)&255)<5&&(((int)Position.Y-Target.Y+2)&255)<5)return true;
        Toward(Target);Move();return false;
    }
    private void SnapTarget()=>Position=new(Target.X+Position.X%1,Target.Y+Position.Y%1);
    private void Input(bool enabled)
    {World.Entities.RuntimeState.SetWramByte(WramAddress.wDisabledObjects,enabled?(byte)0:(byte)1);World.Entities.RuntimeState.SetWramByte(WramAddress.wMenuDisabled,enabled?(byte)0:(byte)1);}
    private void Text(int id)=>World.Entities.OnRoomEntityDialogueRequested(id,Data.Text(id),Position);
    private NotSupportedException Unsupported()=>new($"veranFinalForm.s: form ${SubId:x2}, state ${State:x2}, substate ${Substate:x2}.");
    private void Death(Player player)
    {
        switch(Substate)
        {
            case 0:
                if(InvincibilityCounter!=0||!player.NativeObjectVulnerable||World.Entities.RuntimeState.ReadWramByte(WramAddress.wMenuDisabled)!=0)return;
                Input(false);player.DropHeldItemsForScript();player.ClearNativeItemParents();Substate=1;
                Text(World.Entities.SaveData!.IsLinkedGame?0x5616:0x5615);return;
            case 1:Substate=2;Counter2=40;World.Entities.CreateEnergySwirl(Position,outgoing:true);return;
            case 2:if(Dec2()!=0)return;if(World.DeathFade(this))Substate=3;return;
            case 3:
                if(World.Entities.PaletteFadeActiveSource())return;
                World.Entities.SaveData!.SetRoomFlag(4,0xfc,0x80);
                // bank0.s:incMakuTreeState saturates at $10 on repeat entry.
                World.Entities.SaveData.SetMakuTreeState(Math.Min(World.Entities.SaveData.MakuTreeState+1,0x10));
                World.BeginEscape();byte count=(byte)World.Entities.RoomEnemyCount;Finish();World.Entities.SetNativeRoomEnemyCount(count);return;
            default:throw Unsupported();
        }
    }
}
