using Godot;
using System;

namespace Oracles;

// common/enemies/ganon.s, ENEMY $04. Its revival retains the boss slot.
internal sealed partial class GanonCharacter : EnemyCharacter
{
    internal GanonEnvironment World {get;}
    internal int Slot {get;set;}
    internal int State {get;private set;}
    internal int Substate {get;private set;}
    internal int Angle {get;private set;}
    internal int Speed {get;private set;}
    internal int BaseX {get;private set;}
    internal int SlashSide {get;private set;}
    internal int AttackChoice {get;private set;}
    internal int SequenceIndex {get;private set;}
    private bool _hit;
    internal bool JustHit=>_hit;
    internal int Counter {get;set;}
    internal int Counter2 {get;private set;}
    internal int ZFixed {get;private set;}
    internal int SpeedZ {get;private set;}
    internal int Palette {get;private set;}=1;
    internal override Texture2D CurrentDrawTexture=>DrawsDamagePalette?base.CurrentDrawTexture:Animation.CurrentTextureForPalette(Palette);
    internal int CollisionType {get;private set;}=4;
    private BossShadowEffect? _shadow;
    internal override bool InitializationPending=>State==0;
    internal override bool CollisionEnabled=>!IsDead&&(CollisionType&128)!=0;
    protected override Vector2 AnimationDrawOffset=>base.AnimationDrawOffset+Vector2.Down*(ZFixed>>8);
    internal GanonCharacter(GanonEnvironment world)
    {
        World=world;InitializeEnemy(Vector2.Zero,world.Data.Visual,positionedOam:true,animationSourceOffsets:world.Data.SourceOffsets);
        Health=0;Visible=false;Animation.SetFrameCounter(0);ZIndex=ObjectDrawPriority.FromVisible(0x83);
    }
    internal void PublishHit(int item,int damage=0,int invincibility=0)
    {
        _hit=true;Health=Math.Max(0,Health-damage);
        if(Health==0)CollisionType&=127;
        if(invincibility!=0)InvincibilityCounter=invincibility;
    }
    internal void UpdateFrame(RoomEntityFrame frame)
    {
        if(IsDead)return;
        try
        {
            if(State==0){World.Entities.Random.Next();Health=World.Data.Actor(4).Health;RestartAnimation(0);CollisionType|=128;}
            else if(!_hit&&Health==0){BeginDeath(frame);return;}
            switch(State)
            {
                case 0:
                    State=1;Position=new(0x78,0x48);ZFixed=-256;
                    if(!World.Entities.PartSlotAvailable)throw new NotSupportedException("ganon_state_uninitialized: unchecked shadow allocation aliases an occupied PART slot.");
                    _shadow=(BossShadowEffect)World.Entities.Spawn(new BossShadowSpawn(()=>Position,()=>ZFixed>>8,()=>!IsDead,0,0x12));
                    World.Initialize(this);Visible=true;World.Fade(false,1,false);return;
                case 1:
                    if(World.Entities.TryAllocateEnemy(_=>new GanonRevivalRoomEntity(new(World,0,Slot,Vector2.Zero,0))) is null)return;
                    State=2;Counter2=60;return;
                case 2:
                    if(Counter==0)return;
                    if(Counter2!=0)Counter2--;
                    if(Counter2!=0){Visible=!Visible;return;}
                    State=3;Counter=193;RestartAnimation(13);Visible=true;return;
                case 3:
                    Counter=(Counter-1)&255;
                    if(Counter!=0)
                    {
                        if((Counter&63)==0)World.Entities.OnSoundRequested(SoundId.SndRumble2);
                        AdvanceAnimation();return;
                    }
                    State=4;ZFixed=0;_shadow!.SetSize(2);RestartAnimation(1);return;
                case 4:
                    if(AnimationParameter!=255){AdvanceAnimation();return;}
                    State=5;Counter=15;RestartAnimation(2);return;
                case 5:
                    Counter=(Counter-1)&255;if(Counter!=0)return;
                    Counter=120;World.Entities.SetScreenShake(120,World.Entities.HorizontalScreenShakeCounter,0);
                    State=6;World.Entities.OnSoundRequested(SoundId.SndBossDead);RestartAnimation(3);
                    World.StatusBar(true);World.ClearPalette();return;
                case 6:
                    Counter=(Counter-1)&255;if(Counter!=0){AdvanceAnimation();return;}
                    State=7;Counter=30;
                    World.Entities.RuntimeState.SetWramByte(AgesWramAddress.wDisableLinkCollisionsAndMenu,0);
                    World.Entities.RuntimeState.SetWramByte(AgesWramAddress.wDisabledObjects,0);
                    World.Entities.OnRoomEntityDialogueRequested(0x2f0d,World.Data.Text(0x2f0d),Position);RestartAnimation(2);return;
                case 7:
                    World.Entities.SetNativeActiveMusic(SoundId.MusGanon);World.Entities.OnSoundRequested(SoundId.MusGanon);DecideAttack();return;
                case 8:case 9:case 0x0a:case 0x0b:case 0x0c:case 0x0d:CombatFrame(frame);return;
                case 0x0e:DeathFrame();return;
                default:throw new NotSupportedException($"ganon.s: ENEMY $04 state${State:x2} combat continuation is not implemented.");
            }
        }
        finally{_hit=false;AdvanceInvincibilityCounter();QueueRedraw();}
    }
}
