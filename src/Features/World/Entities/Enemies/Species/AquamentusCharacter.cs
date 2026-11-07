namespace Oracles;

// seasons/enemies/aquamentus.s: the body, renderer and horn keep separate
// physical ENEMY slots, status bytes and animation visits.
internal sealed partial class AquamentusCharacter : EnemyCharacter
{
    private RoomEntityManager _world=null!;
    private AquamentusDatabase _data=null!;
    private readonly AquamentusCharacter? _birthParent;
    private readonly AquamentusCharacter?[] _children=new AquamentusCharacter?[4];
    private bool _collision,_deathStarted,_musicStarted;
    private int _footstepCounter,_animationSelector;
    internal ImportedEnemyDefinition Record {get;private set;}
    internal int Slot {get;private set;}=-1;
    internal int SubId=>Record.SubId;
    internal int State {get;private set;}
    internal int Counter {get;private set;}
    internal int Counter2 {get;private set;}
    internal int Angle {get;private set;}
    internal int Speed {get;private set;}
    private int _zFixed;
    internal int ZFixed
    {
        get=>_zFixed;
        private set
        {
            int previous=_zFixed;_zFixed=value;
            // Height is part of Godot's cached draw commands, even during a held pose.
            if((previous>>8)!=(value>>8))QueueRedraw();
        }
    }
    internal int PoseMask {get;private set;}
    internal int RelatedBodySlot {get;private set;}=-1;
    internal int RendererSlot {get;private set;}=-1;
    internal int HornSlot {get;private set;}=-1;
    internal int ActiveCollisionMode {get;private set;}=EnemyCollisionMode.AquamentusHorn;
    internal bool OwnsRoomCount {get;private set;}
    internal override bool InitializationPending=>State==0;
    internal override bool CollisionEnabled=>_collision&&!IsDead&&Health>0;
    protected override Vector2 AnimationDrawOffset=>base.AnimationDrawOffset+Vector2.Down*(ZFixed>>8);
    private AquamentusCharacter At(int slot)=>_world.ResolveEnemySlot(slot)?.Node as AquamentusCharacter??
        throw new NotSupportedException($"aquamentus.s: ENEMY slot${Slot:x2} references unavailable related slot${slot:x2}.");
    internal AquamentusCharacter(AquamentusCharacter? birthParent=null)=>_birthParent=birthParent;
    internal void Initialize(ImportedEnemyDefinition record,Vector2 position,RoomEntityManager world,bool counted)
    {
        Record=record;_world=world;_data=AquamentusDatabase.For(record.AssetRoot);OwnsRoomCount=counted;
        InitializeEnemy(position,EnemyCharacterConfiguration.FromImported(record),paletteOverrides:_data.Palette,positionedOam:true);
        if(_birthParent is not null)ZFixed=unchecked((short)(_birthParent.ZFixed&0xff00));
        Visible=false;
    }
    internal void BindSlot(int slot)
    {
        Slot=slot;
        if(_birthParent is null)return;
        _birthParent._children[SubId]=this;
        if(SubId==2)
        {
            // The spawner's plain enemyDelete retains its one count. Its
            // renderer owns that allocation until PART$04 takes over.
            OwnsRoomCount=_birthParent.OwnsRoomCount;_birthParent.OwnsRoomCount=false;
        }
    }
    internal bool Damage(Vector2 origin,int damage)
    {
        if(!CollisionEnabled||InvincibilityCounter!=0||NativeHitPending)return false;
        Health=Math.Max(0,Health-Math.Max(0,damage));
        if(Health==0)_collision=false;
        ApplySwordNoKnockback(origin,EnemyKnockbackStrength.Normal);DeferNativeHitStatus();return true;
    }
    internal bool ArmorHit(Vector2 origin)
    {
        if(!AcceptArmoredSwordHit(28))return false;
        DeferNativeHitStatus();return true;
    }
    internal void PublishHarmlessHit()=>DeferNativeHitStatus();
    internal void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(IsDead)return;
        bool hit=NativeHitPending;
        int hitInvincibility=InvincibilityCounter;
        AdvanceInvincibilityCounter();
        if(hit)ClearNativeHitStatus();
        if(Health==0&&!hit)
        {
            if(SubId==2){UpdateDeath(spawns);return;}
            if(SubId==3)
            {
                var body=At(RelatedBodySlot);var renderer=At(RendererSlot);
                body.Health=0;body._collision=false;renderer.Health=0;renderer._collision=false;
            }
            Finish();return;
        }
        if(hit&&SubId==3)
        {
            var renderer=At(RendererSlot);renderer.InvincibilityCounter=hitInvincibility;
            renderer.SetStatusPaletteSuppressed(true); // Its earlier visit already published this update's normal OAM flags.
        }
        if(State==0)
        {
            _world.Random.Next();
            _collision=true;
            int z=(-(OracleObjectPosition.HighByte(Position.Y)+0x20))&255;
            int sum=((-Mathf.FloorToInt(_world.ToScreen(Vector2.Zero).Y))&255)+z;
            int high=sum>255?z:sum;
            if((high&128)==0)high=128;
            ZFixed=unchecked((short)(high<<8));
            State=SubId==0?1:8;Speed=SubId;
            if(SubId==0){_world.RuntimeState.SetField("wEnemyIDToLoadExtraGfx",0x78);_world.InitializeBossRoom();}
            return;
        }
        if(State==1)
        {
            if(_world.RuntimeState.ReadField("wcc93")!=0||!_world.CanAllocateEnemies(3))return;
            foreach(int subid in new[]{1,2,3})
                if(!TrySpawnNativeEnemy(new AquamentusSpawn(this,subid),spawns))
                    throw new InvalidOperationException("aquamentus_state_spawner: three admitted ENEMY slots became unavailable during the native child sequence.");
            var body=_children[1]!;var renderer=_children[2]!;var horn=_children[3]!;
            body.RendererSlot=renderer.Slot;body.HornSlot=horn.Slot;
            renderer.RelatedBodySlot=body.Slot;renderer.HornSlot=horn.Slot;
            horn.RelatedBodySlot=body.Slot;horn.RendererSlot=renderer.Slot;
            Finish();return;
        }
        if(SubId==1){UpdateBody(frame,spawns);return;}
        if(SubId==2){UpdateRenderer();return;}
        if(SubId==3){UpdateHorn();return;}
        throw new NotSupportedException($"aquamentus.s: unsupported subid${SubId:x2}/state${State:x2}.");
    }
    private void UpdateBody(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        switch(State)
        {
            case 8:
                if(!TrySpawnNativePart(new BossShadowSpawn(()=>Position,()=>ZFixed>>8,()=>!IsDead,2,12),spawns))return;
                State=9;ActiveCollisionMode=EnemyCollisionMode.AquamentusBody;PoseMask=1;Counter=90;return;
            case 9:
                if(((ZFixed>>8)&255)>=0xf4){State=10;PoseMask=2;return;}
                ZFixed=unchecked((short)(ZFixed+_data.FallingSpeeds[(ZFixed>>12)&15]));HoverSound(frame.Counter);return;
            case 10:
                Counter=(Counter-1)&255;
                if(Counter!=0){HoverSound(frame.Counter);return;}
                Counter=60;ZFixed&=255;State=11;PoseMask=4;
                if(!_musicStarted){_musicStarted=true;_world.SetNativeActiveMusic(SoundId.MusBoss);_world.OnSoundRequested(SoundId.MusBoss);}
                Pound(32);return;
            case 11:
                Counter=(Counter-1)&255;if(Counter!=0)return;
                Counter=150;Counter2=4;_footstepCounter=24;DecideAttack(frame.LinkTarget??frame.Player.EnemyContactPosition);return;
            case 12:
                Footstep(24);UpdateMouth();Counter=(Counter-1)&255;
                if(Counter==0)
                {
                    Counter=_data.FireDelays[Counter2];Counter2=(Counter2-1)&255;
                    if(Counter2==0){State=13;PoseMask=8;Speed=ObjectSpeed.Speed80;return;}
                    ChooseLeftwardAngle();Fire(spawns);
                }
                Move();return;
            case 13:
                Footstep(18);UpdateMouth();
                if(AtTarget()){Counter=30;State=11;PoseMask=4;return;}
                Counter=(Counter-1)&255;if(Counter==0)Fire(spawns);
                MoveTowardTarget();return;
            case 14:
                if(Counter2!=0&&--Counter2!=0)return;
                if(OracleObjectPosition.HighByte(Position.X)>=0x1c)
                {
                    if((frame.Counter&31)==0)_world.OnSoundRequested(SoundId.SndSwordSpin);
                    if((frame.Counter&7)==0)_world.TryCreateKnockbackDust(Position.Floor()+new Vector2(16,16),ZFixed>>8);
                    Move();return;
                }
                Counter=(Counter-2)&255;
                if(Counter!=0){if(Counter==148)Pound(70);return;}
                Counter=240;Counter2=60;State=15;ZFixed=unchecked((short)(0xf800|(ZFixed&255)));
                PoseMask=1;Angle=8;Speed=ObjectSpeed.Speedc0;return;
            case 15:
                HoverSound(frame.Counter);
                if(Counter2!=0&&--Counter2!=0)
                {
                    if(((ZFixed>>8)&255)>=0xe8)ZFixed=unchecked((short)(ZFixed-0x80));return;
                }
                Counter=(Counter-1)&255;
                if(Counter==0){State=9;Counter=30;return;}
                if(Counter>=210)ZFixed=unchecked((short)(ZFixed-0xc0));
                if(!AtTarget())MoveTowardTarget();return;
            default:throw new NotSupportedException($"aquamentus_body: unsupported state${State:x2}.");
        }
    }
    private void DecideAttack(Vector2 target)
    {
        int x=OracleObjectPosition.HighByte(target.X),y=OracleObjectPosition.HighByte(target.Y);
        bool charge=x<OracleObjectPosition.HighByte(Position.X)&&
            (_data.ChargeProbabilities[x>>6]&(1<<(_world.Random.Next().Value&7)))!=0;
        State=charge?14:12;
        if(charge){Angle=0x17+(y<0x48?2:y<0x68?1:0);Counter2=30;PoseMask=0x20;Speed=ObjectSpeed.Speed1c0;}
        else{ChooseLeftwardAngle();PoseMask=4;Speed=ObjectSpeed.Speed40;}
        int index=HighestBit(PoseMask)*2;SetCollisionRadii(_data.CollisionBoxes[index+1],_data.CollisionBoxes[index]);
    }
    private void ChooseLeftwardAngle(){int roll=_world.Random.Next().Value&7;Angle=0x15+(roll==7?3:roll);}
    private void Move()=>Position=NativeObjectMovement.ApplySpeed(_world.RuntimeState,OracleObjectPosition.FromPixels(Position),Speed,Angle).PrecisePosition;
    private bool AtTarget()=>((OracleObjectPosition.HighByte(Position.X)-0xc0+2)&255)<5&&((OracleObjectPosition.HighByte(Position.Y)-0x50+2)&255)<5;
    private void MoveTowardTarget(){Angle=OracleObjectMovement.Shared.RelativeAngle(Position,new(192,80));Move();}
    private void HoverSound(int frame){if((frame&31)==0)_world.OnSoundRequested(SoundId.SndAquamentusHover);}
    private void Footstep(int period){if(--_footstepCounter==0){_footstepCounter=period;_world.OnSoundRequested(SoundId.SndRoller);}}
    private void Pound(int frames){_world.BeginScreenShake(frames);_world.OnSoundRequested(SoundId.SndStrongPound);}
    private void Fire(ICollection<RoomEntitySpawn> spawns)
    {PoseMask=16;_world.OnSoundRequested(SoundId.SndDodongoOpenMouth);TrySpawnNativePart(new AquamentusProjectileSpawn(Slot,Position.Floor(),Angle),spawns);}
    private void UpdateMouth(){if(At(RendererSlot).AnimationParameter==255)PoseMask=State==12?4:8;}
    private void UpdateRenderer()
    {
        if(State==8){State=9;_collision=false;Visible=true;ZIndex=ObjectDrawPriority.FromVisible(0x81);return;}
        var body=At(RelatedBodySlot);
        Position=body.Position.Floor()+Position-Position.Floor();ZFixed=unchecked((short)((body.ZFixed&0xff00)|(ZFixed&255)));
        if(body.PoseMask!=0)
        {
            int animation=_data.BodyAnimations[HighestBit(body.PoseMask)];
            if(_animationSelector!=animation){_animationSelector=animation;RestartAnimation(animation);return;}
        }
        AdvanceAnimation();
    }
    private void UpdateHorn()
    {
        if(State==8){State=9;SetCollisionRadii(3,6);Visible=true;ZIndex=ObjectDrawPriority.FromVisible(0x81);return;}
        var body=At(RelatedBodySlot);var renderer=At(RendererSlot);Visible=renderer.Visible;
        int animation=_data.HornAnimations[(renderer.AnimationParameter+1)&255];
        if(_animationSelector!=animation){_animationSelector=animation;RestartAnimation(animation);}
        ZFixed=unchecked((short)(((((body.ZFixed>>8)-7)&255)<<8)|(ZFixed&255)));
        int pose=renderer.AnimationParameter;if(pose>=9)pose=5;
        int y=(OracleObjectPosition.HighByte(body.Position.Y)+_data.HornOffsets[pose*2])&255;
        int x=(OracleObjectPosition.HighByte(body.Position.X)+_data.HornOffsets[pose*2+1])&255;
        Position=new Vector2(x,y)+Position-Position.Floor();AdvanceAnimation();
    }
    private void UpdateDeath(ICollection<RoomEntitySpawn> spawns)
    {
        if(!_deathStarted){_deathStarted=true;_collision=false;Counter=120;_world.DisableLinkCollisionsAndMenu();_world.OnSoundRequested(SoundId.SndBossDead);}
        Counter=(Counter-1)&255;
        if(Counter!=0){Visible=!Visible;return;}
        Counter=1;
        if(!_world.PartSlotAvailable)return;
        OwnsRoomCount=false;
        if(!TrySpawnNativePart(new BossDeathExplosionSpawn(Position,SeasonsEnemyId.Aquamentus,ZFixed>>8),spawns))
            throw new InvalidOperationException("enemyBoss_dead: admitted PART$04 allocation failed.");
        int music=_world.NativeRoomMusicSource();_world.SetNativeActiveMusic(music);_world.OnSoundRequested(music);Finish();
    }
    private static int HighestBit(int value){int bit=0;while((value>>=1)!=0)bit++;return bit;}
}

internal sealed record AquamentusSpawn(AquamentusCharacter Parent,int SubId):RoomEntitySpawn;
internal sealed record AquamentusProjectileSpawn(int ParentSlot,Vector2 Position,int Angle,int SubId=0):RoomEntitySpawn;
