namespace Oracles;

// seasons/enemies/brotherGoriyas.s. Related objects retain physical slot identity.
internal sealed partial class BrotherGoriyaCharacter : EnemyCharacter
{
    private RoomEntityManager _world=null!;
    private EnemyTerrainMovement _movement=null!;
    private BrotherGoriyaDatabase _data=null!;
    private Func<int,IRoomEntity?> _resolve=null!;
    private readonly BrotherGoriyaCharacter? _birthParent;
    private bool _collisionEnabled,_deathStarted,_deathDispatchReady;
    internal override bool InitializationPending=>State==0;
    internal override bool CollisionEnabled=>_collisionEnabled&&Health>0&&base.CollisionEnabled;
    internal ImportedEnemyDefinition Record {get;private set;}
    internal int Slot {get;private set;}=-1;
    internal int RelatedSlot {get;private set;}=-1;
    internal int SubId {get;private set;}
    internal int State {get;private set;}
    internal int Substate {get;private set;}
    internal int Counter {get;private set;}
    internal int Counter2 {get;private set;}
    internal int Angle {get;private set;}
    internal int Speed {get;private set;}
    internal int OrbitOffset {get;private set;}
    internal bool HasBoomerang {get;private set;}
    internal byte BoomerangHigh {get;set;}
    internal BrotherGoriyaCharacter? Related=>RelatedSlot<0?null:_resolve(RelatedSlot)?.Node as BrotherGoriyaCharacter;
    private BrotherGoriyaCharacter Partner=>Related??throw new NotSupportedException($"Brother Goriya ENEMY slot${Slot:x2} references missing slot${RelatedSlot:x2}.");
    internal override Texture2D CurrentDrawTexture=>DrawsDamagePalette?base.CurrentDrawTexture:Animation.CurrentTextureForPalette(SubId+1);

    internal BrotherGoriyaCharacter(BrotherGoriyaCharacter? birthParent=null)=>_birthParent=birthParent;
    internal void Initialize(ImportedEnemyDefinition record,OracleRoomData room,Vector2 position,
        RoomEntityManager world,BrotherGoriyaDatabase data)
    {
        Record=record;SubId=record.SubId;_world=world;_data=data;
        InitializeEnemy(position,EnemyCharacterConfiguration.FromImported(record),paletteVariants:[1,2]);
        ConfigureSwordKnockback(room,EnemyKnockbackMotion.Terrain,nativeSpeed:()=>Speed);
        _movement=new(this,room);Visible=false;
    }
    internal void BindSlot(int slot,Func<int,IRoomEntity?> resolve)
    {
        Slot=slot;_resolve=resolve;
        if(_birthParent is not null){RelatedSlot=_birthParent.Slot;_birthParent.RelatedSlot=slot;}
    }
    internal override bool TakeSwordHit(Vector2 origin,int damage)
    {
        if(_deathStarted||!base.TakeSwordHit(origin,damage))return false;
        if(IsDead){IsDead=false;Visible=true;}
        return true;
    }
    internal override bool TakeBurnHit(int damage)
    {
        if(_deathStarted||!base.TakeBurnHit(damage))return false;
        if(IsDead){IsDead=false;Visible=true;}
        return true;
    }
    internal void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(IsDead)return;
        bool consumed=BeginFrame();
        if(consumed&&!_deathDispatchReady)return;
        if(Health==0&&!HasActiveKnockback&&!NativeHitPending)
        {
            UpdateDeath(spawns);return;
        }
        if(consumed)return;
        if(State==0)
        {
            _world.Random.Next();
            if(RelatedSlot<0)
            {
                if(!TrySpawnNativeEnemy(new BrotherGoriyaSpawn(this),spawns))return;
                if(Slot>Partner.Slot){Partner.SubId=0;SubId=1;return;}
            }
            Speed=0x32;State=8;Visible=true;
            if(SubId!=0)
            {
                Position=new(((256-OracleObjectPosition.HighByte(Position.X))&255)+(Position.X-Mathf.Floor(Position.X)),Position.Y);
                return;
            }
            HasBoomerang=true;_world.RuntimeState.SetField("wEnemyIDToLoadExtraGfx",0);_world.InitializeBossRoom();return;
        }
        Vector2 target=frame.LinkTarget??frame.Player.EnemyContactPosition;
        switch(State)
        {
            case 8:
                if(Substate==0)
                {
                    if(OracleObjectPosition.HighByte(frame.Player.Position.Y)>=0x78)return;
                    Substate=1;_collisionEnabled=true;
                    if(SubId==0)_world.OnRoomEntityDialogueRequested(0x2f00,_data.IntroText,Position);
                    return;
                }
                if(SubId==0&&Substate==1){Substate=2;_world.SetNativeActiveMusic(0x2d);_world.OnSoundRequested(0x2d);}
                Vector2 approach=new((OracleObjectPosition.HighByte(frame.Player.Position.X)+(SubId==0?-0x28:0x28))&255,
                    OracleObjectPosition.HighByte(frame.Player.Position.Y));
                int x=OracleObjectPosition.HighByte(Position.X);
                if(((x-0x18)&255)>=0xc0 || Near(Position,approach,8))
                {
                    Counter=(_world.Random.Next().Value&0x30)+0x60;State=9;OrbitOffset=8;Counter2=0x3c;return;
                }
                Angle=OracleObjectMovement.Shared.RelativeAngle(Position,approach);
                Position=ApplyMovementSpeed(OracleObjectPosition.FromPixels(Position),Speed,Angle).PrecisePosition;
                SetAnimationFromAngle();AdvanceAnimation();return;
            case 9:
                if(!AdvanceAttackTimer(target)){AdvanceAnimation();return;}
                Speed=SubId==0?0x23:0x28;
                int tx=OracleObjectPosition.HighByte(target.X),ty=OracleObjectPosition.HighByte(target.Y);
                if(((ty-0x40)&255)>=0x30 || ((tx-0x40)&255)>=0x70)
                {
                    Counter2=(Counter2-1)&255;
                    if(Counter2==0){Counter2=0x3c;RandomCardinal();}
                    if(!_movement.MoveAtAngle(Angle,Speed,false))RandomCardinal();
                    AdvanceAnimation();return;
                }
                int difference=(((Angle+0x10)&31)-Partner.Angle)&31;
                Speed=difference==0?0x28:difference<0x10?0x32:0x1e;
                Vector2 center=new(tx<0x60?0x58:tx<0x90?0x78:0x98,0x58);
                Angle=(OracleObjectMovement.Shared.RelativeAngle(Position,center)+OrbitOffset)&31;
                SetAnimationFromAngle();_movement.MoveAtAngle(Angle,Speed,false);
                int distance=(Math.Abs(OracleObjectPosition.HighByte(target.X)-OracleObjectPosition.HighByte(Position.X))+
                    Math.Abs(OracleObjectPosition.HighByte(target.Y)-OracleObjectPosition.HighByte(Position.Y)))&255;
                if(distance<0x30 || distance>=0x60)
                {
                    Angle=OracleObjectMovement.Shared.RelativeAngle(Position,target);
                    Speed=distance<0x30?0x14:0x0a;
                    if(distance<0x30)Angle^=0x10;
                    _movement.MoveAtAngle(Angle,Speed,false);
                }
                AdvanceAnimation();return;
            case 10:
                if(!HasBoomerang){State=9;AdvanceAnimation();return;}
                if(TrySpawnNativePart(new BrotherGoriyaBoomerangSpawn(this,Partner,target),spawns))
                {
                    State=Partner.State=11;HasBoomerang=false;Partner.HasBoomerang=true;Partner.BoomerangHigh=1;
                }
                AdvanceAnimation();return;
            case 11:
                if(BoomerangHigh!=0)
                {
                    if(BoomerangHigh==1&&(frame.Counter&7)==0)
                    {
                        int slot=Partner.BoomerangHigh-0xd0;
                        if(_world.ResolvePartSlot(slot) is not BrotherGoriyaBoomerangRoomEntity boomerang)
                            throw new NotSupportedException($"Brother Goriya slot${Slot:x2} cannot face PART page${Partner.BoomerangHigh:x2}.");
                        Angle=OracleObjectMovement.Shared.RelativeAngle(Position,boomerang.Position);SetAnimationFromAngle();
                    }
                }
                else {Partner.BoomerangHigh=unchecked((byte)(Partner.BoomerangHigh-1));Partner.State=State=9;}
                AdvanceAnimation();return;
            default:throw new NotSupportedException($"Brother Goriya $70:${SubId:x2} state${State:x2} is not represented.");
        }
    }
    private bool AdvanceAttackTimer(Vector2 target)
    {
        if(Counter!=0&&--Counter!=0)return true;
        var random=_world.Random.Next();Counter=0x20+(random.Low&0x30);
        if((random.High&1)!=0)return true;
        State=10;Partner.Counter=Counter;Angle=OracleObjectMovement.Shared.RelativeAngle(Position,target);SetAnimationFromAngle();return false;
    }
    private void RandomCardinal(){Angle=_world.Random.NextCardinalAngle();SetAnimationFromAngle();}
    private void SetAnimationFromAngle()=>SetNativeAngleAnimation(Angle);
    internal static bool Near(Vector2 first,Vector2 second,int radius)=>
        ((OracleObjectPosition.HighByte(first.X)-OracleObjectPosition.HighByte(second.X)+radius)&255)<radius*2+1 &&
        ((OracleObjectPosition.HighByte(first.Y)-OracleObjectPosition.HighByte(second.Y)+radius)&255)<radius*2+1;
    protected override void CompleteKnockbackDeath()=>_deathDispatchReady=true;
    private void UpdateDeath(ICollection<RoomEntitySpawn> spawns)
    {
        if(RelatedSlot>=0){var partner=Partner;partner.Health=0;partner._collisionEnabled=false;RelatedSlot=-1;}
        if(BoomerangHigh>=2)
        {
            // @dead subtracts $02 before using H; retain the clean-US alias,
            // which can clear an earlier PART's related pointer or WRAM scratch.
            _world.RuntimeState.SetWramByte(((BoomerangHigh-2)<<8)|0xd7,0);BoomerangHigh=0;
        }
        if(!_deathStarted)
        {
            _deathStarted=true;_collisionEnabled=false;Counter=120;
            _world.DisableLinkCollisionsAndMenu();_world.OnSoundRequested(SoundId.SndBossDead);
        }
        Counter--;
        if(Counter!=0){Visible=!Visible;return;}
        Counter=1;
        if(!TrySpawnNativePart(new BossDeathExplosionSpawn(Position,SeasonsEnemyId.BrotherGoriyas),spawns))return;
        int music=_world.NativeRoomMusicSource();_world.SetNativeActiveMusic(music);_world.OnSoundRequested(music);Finish();
    }
}

internal sealed record BrotherGoriyaSpawn(BrotherGoriyaCharacter Parent):RoomEntitySpawn;
internal sealed record BrotherGoriyaBoomerangSpawn(BrotherGoriyaCharacter Parent,BrotherGoriyaCharacter Receiver,Vector2 Target):RoomEntitySpawn;
