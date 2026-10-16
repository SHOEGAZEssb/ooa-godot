using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// ENEMY_VERAN_FAIRY $06. The native attack subroutines can discard their
// caller's return address: those updates animate without moving the boss.
internal sealed partial class VeranFairyCharacter : EnemyCharacter
{
    internal VeranFairyEnvironment World {get;}
    internal VeranFairyDatabase Data=>World.Data;
    internal int State {get;private set;}
    internal int Substate {get;private set;}
    internal int Counter {get;private set;}
    internal int Counter2 {get;private set;}
    internal int Pattern {get;private set;}
    internal int PatternOffset {get;private set;}
    internal int Attack {get;private set;}
    internal int HealthTier {get;private set;}
    internal int AttackPhase {get;private set;}
    internal int Shots {get;private set;}
    internal int StillCounter {get;private set;}
    internal int Speed {get;private set;}
    internal int Angle {get;private set;}
    internal int ZFixed {get;private set;}
    internal Vector2I Target {get;private set;}
    private bool _hit,_collision=true;
    internal bool JustHit=>_hit;
    private int _palette=1;
    internal override bool InitializationPending=>State==0;
    internal override bool CollisionEnabled=>!IsDead&&_collision;
    protected override Vector2 AnimationDrawOffset=>base.AnimationDrawOffset+Vector2.Down*(ZFixed>>8);
    internal override Texture2D CurrentDrawTexture=>DrawsDamagePalette?base.CurrentDrawTexture:Animation.CurrentTextureForPalette(_palette);
    internal VeranFairyCharacter(VeranFairyEnvironment world,Vector2 position)
    {
        World=world;
        InitializeEnemy(position.Floor(),EnemyCharacterConfiguration.FromImported(Data.Actor(6)),positionedOam:true,paletteVariants:[0,1]);
        Visible=false;
    }
    internal void PublishHit(int damage=0,int invincibility=0)
    {
        _hit=true;
        if(invincibility!=0)InvincibilityCounter=invincibility;
        Health=Math.Max(0,Health-damage);
        if(Health==0)_collision=false;
    }
    internal void UpdateFrame(RoomEntityFrame frame)
    {
        if(IsDead)return;
        try {UpdateCore(frame);}
        finally {_hit=false;AdvanceInvincibilityCounter();QueueRedraw();}
    }
    private void UpdateCore(RoomEntityFrame frame)
    {
        if(_hit)
        {
            HealthTier=Health>=20?0:Health>=10?1:2;
            Speed=Data.Bytes("speeds")[HealthTier];
        }
        else if(Health==0)
        {
            // The source loads invincibilityCounter but never tests A: RET
            // NZ retains Z from SUB ENEMYSTATUS_NO_HEALTH and does not wait.
            if(!frame.Player.PatchCollisionsEnabled)return;
            Write(WramAddress.wDisabledObjects,1);Write(WramAddress.wMenuDisabled,1);
            Health=1;State=5;Substate=0;Counter=60;
        }
        switch(State)
        {
            case 0:
                World.Entities.Random.Next();State=1;Counter=60;Speed=0x32;Pattern=255;
                RestartAnimation(2);Visible=true;ZIndex=ObjectDrawPriority.BehindLinkZIndex;return;
            case 1:Introduction();return;
            case 2:
                Attack=Data.Bytes("attacks")[HealthTier*8+(World.Entities.Random.Next().Value&7)];
                State=3;StillCounter=60;AttackPhase=0;
                int pattern;do {pattern=World.Entities.Random.Next().Value&3;}while(pattern==Pattern);
                Pattern=pattern;PatternOffset=2;var bytes=Data.Bytes($"pattern-{Pattern}");Target=new(bytes[1],bytes[0]);return;
            case 3:
                if(UpdateAttack(out bool animate)){if(animate)AdvanceAnimation();return;}
                Vector2 position=Position.Floor();
                if((((int)position.X-Target.X+2)&255)<5&&(((int)position.Y-Target.Y+2)&255)<5)
                {
                    Position=new(Target.X+Position.X%1,Target.Y+Position.Y%1);Wrap();
                    var next=Data.Bytes($"pattern-{Pattern}");int y=next[PatternOffset++];
                    if(y==0){RestartAnimation(5);State=4;return;}
                    Target=new(next[PatternOffset++],y);
                }
                Angle=OracleObjectMovement.Shared.RelativeAngle(Position.Floor(),Target);
                Position=ApplyMovementSpeed(OracleObjectPosition.FromPixels(Position),Speed,Angle).PrecisePosition;
                AdvanceAnimation();return;
            case 4:
                StillCounter=(StillCounter-1)&255;if(StillCounter==0)State=2;AdvanceAnimation();return;
            case 5:Death(frame.Player);return;
            default:throw new NotSupportedException($"veranFairy.s: ENEMY $06 state ${State:x2}.");
        }
    }
    private void Introduction()
    {
        switch(Substate)
        {
            case 0:
                if(DecCounter()!=0){Visible=!Visible;return;}
                Counter=8;Substate=1;Visible=true;ZIndex=ObjectDrawPriority.FixedLowPriorityZIndex;return;
            case 1:
                if(DecCounter()!=0)return;Substate=2;Text(0x560f);return;
            case 2:Substate=3;Counter=30;RestartAnimation(4);return;
            case >=3 and <=7:
                if(DecCounter()!=0)return;
                int[] lightning=[0x33,0x7b,0x55,0x3b,0x73];
                Strike(lightning[Substate-3]);Counter=10;Substate++;return;
            case 8:
                if(DecCounter()!=0)return;Substate=9;Strike(0x59);World.Entities.BeginPalacePossessionFade(true,1);return;
            case 9:
                foreach(int packed in Data.Bytes("pillars"))
                    World.Entities.ActiveRoom.SetPositionTileAndCollision(ShortPosition(packed),0xa5,null,World.Entities.AnimationTick());
                World.Entities.OnRoomTileChanged();Substate=10;return;
            case 10:
                foreach(int packed in Data.Bytes("mimics"))SpawnMimic(ShortPosition(packed));
                Substate=11;Counter=30;_palette=0;ZFixed=unchecked((short)(ZFixed-0x100));
                Visible=true;ZIndex=ObjectDrawPriority.FixedLowPriorityZIndex;RestartAnimation(5);
                World.Entities.BeginPalacePossessionFade(false,4);return;
            case 11:
                if(World.Transitions.PaletteFadeActive||DecCounter()!=0)return;Substate=12;Text(0x5610);return;
            case 12:
                State=2;Counter2=120;Write(WramAddress.wDisabledObjects,0);Write(WramAddress.wMenuDisabled,0);
                World.Entities.SetNativeActiveMusic(SoundId.MusBoss);World.Entities.OnSoundRequested(SoundId.MusBoss);return;
            default:throw new NotSupportedException($"veranFairy.s: introduction substate ${Substate:x2}.");
        }
    }
    private void SpawnMimic(Vector2 position)=>World.Entities.TryAllocateEnemy(_=>
    {
        var actor=new ArmMimicCharacter();
        actor.Initialize(Data.Actor(0x64),World.Entities.ActiveRoom,position,paletteOverrides:Data.MimicPalettes,
            nativeMemory:World.Entities.RuntimeState,nativeRandom:World.Entities.Random);
        var entity=new ArmMimicRoomEntity(actor,new(0x64,0,0x39,0,0,EnemyHandlerKind.ArmMimic,"veranFairy.s:state1@nextMimic",0x10,0x0f,0x0f),World.Entities.OnSoundRequested);
        World.Own(entity);return entity;
    });
    private bool UpdateAttack(out bool animate)
    {
        animate=true;
        if(Counter2!=0)Counter2--;
        if(Counter2!=0)return false;
        if(Attack==0)
        {
            if(AttackPhase==0)
            {
                int roll=World.Entities.Random.Next().Value&15;Counter2=60;
                if(8+HealthTier*2>=roll)return false;
                Counter2=0;Counter=1;AttackPhase=1;Shots=4;
            }
            if(DecCounter()!=0){if(Counter==14)RestartAnimation(5);return false;}
            if(!WithinBoundary())return false;
            if(--Shots==0){Counter2=90;AttackPhase=0;return false;}
            Counter=30;Projectile(0x2d);RestartAnimation(6);return false;
        }
        if(Attack==1)
        {
            if(AttackPhase==0)
            {
                if(!WithinBoundary())return false;
                int roll=World.Entities.Random.Next().Value&15;Counter2=90;
                if(6+HealthTier*2>=roll)return false;
                Counter2=0;Counter=180;AttackPhase=1;Projectile(0x37);RestartAnimation(6);
            }
            if(DecCounter()==0){Counter2=120;AttackPhase=0;RestartAnimation(5);animate=false;}
            return true;
        }
        if(Attack==2)
        {
            if((AttackPhase&1)==0)
            {
                if(!WithinBoundary())return false;
                AttackPhase=1;Counter=30;Projectile(0x2f);RestartAnimation(6);
            }
            if(DecCounter()==0){Counter2=240;AttackPhase=0;RestartAnimation(5);animate=false;}
            return true;
        }
        throw new NotSupportedException($"veranFairy.s: attack ${Attack:x2}.");
    }
    private void Projectile(int id)
    {
        if(!World.Entities.PartSlotAvailable)return;
        if(id==0x37)World.Entities.TryCreateVeranProjectile(this,0,Position,ZFixed,Angle);
        else
        {
            var entity=new VeranFairyProjectileRoomEntity(World.Entities,Data,this,id,Position,ZFixed,Angle);
            World.Own(entity);World.Entities.AddEntity(entity);
        }
    }
    private bool WithinBoundary()=>(((int)Position.Y-0x10)&255)<0x90&&(((int)Position.X-0x10)&255)<0xd0;
    private void Wrap()
    {
        int packed=(((int)Position.Y&0xf0)|(((int)Position.X>>4)&15));var keys=Data.Bytes("wrap-keys");
        for(int i=0;i<keys.Length-1;i+=2)
        {
            if(keys[i]!=packed)continue;
            var positions=Data.Bytes("wrap-positions");int at=keys[i+1];
            Position=new(positions[at+1]+Position.X%1,positions[at]+Position.Y%1);return;
        }
    }
    private void Death(Player player)
    {
        switch(Substate)
        {
            case 0:
                if(DecCounter()!=0){Visible=!Visible;return;}
                Substate=1;Visible=true;ZIndex=ObjectDrawPriority.BehindLinkZIndex;return;
            case 1:Substate=2;Counter2=65;Text(0x5612);return;
            case 2:
                if(Counter2!=0)Counter2--;
                if(Counter2==0)
                {
                    if(World.Transitions.PaletteFadeActive)return;
                    player.ClearNativeItemParents();player.DropHeldItemsForScript();World.EscapeAttempt();
                    // @triggerCutscene calls enemyDelete, without decNumEnemies.
                    // Transfer this body's contribution to the existing byte owner.
                    byte enemyCount=(byte)World.Entities.RoomEnemyCount;Finish();World.Entities.SetNativeRoomEnemyCount(enemyCount);return;
                }
                if((Counter2&15)!=0)return;
                int index=(Counter2>>4)-1;if(index==1)World.Entities.BeginPalacePossessionFade(true,1);
                var offsets=Data.Bytes("explosions");
                World.Explosion(new(((int)Position.X+unchecked((sbyte)offsets[index*2+1]))&255,
                    ((int)Position.Y+unchecked((sbyte)offsets[index*2]))&255),ZFixed>>8);return;
            default:throw new NotSupportedException($"veranFairy.s: death substate ${Substate:x2}.");
        }
    }
    private int DecCounter()=>Counter=(Counter-1)&255;
    private void Write(int address,int value)=>World.Entities.RuntimeState.SetWramByte(address,(byte)value);
    private void Strike(int packed)=>World.Lightning(ShortPosition(packed));
    private void Text(int id)=>World.ShowText(id,Data.Text(id));
    private static Vector2 ShortPosition(int packed)=>new((packed&15)*16+8,(packed&0xf0)+8);
}
