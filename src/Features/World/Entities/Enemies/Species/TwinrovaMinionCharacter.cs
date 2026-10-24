using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// common/enemies/twinrovaIce.s ($5d), twinrovaBat.s ($5e).
internal sealed partial class TwinrovaMinionCharacter : EnemyCharacter
{
    internal TwinrovaEnvironment World {get;}
    internal int Id {get;}
    internal int ParentSlot {get;}
    internal int State {get;private set;}
    internal int Counter {get;private set;}
    internal int Counter2 {get;private set;}
    internal int Angle {get;private set;}
    internal int Speed {get;private set;}
    internal int Palette {get;private set;}
    internal int ZFixed {get;}
    internal bool Killed {get;private set;}
    private int _pending=-1;
    internal bool JustHit=>_pending>=0;
    private Func<int,IRoomEntity?> _resolve=null!;
    internal void Bind(Func<int,IRoomEntity?> resolve)=>_resolve=resolve;
    internal override bool InitializationPending=>State==0;
    internal override bool CollisionEnabled=>!IsDead&&State!=0&&Health!=0;
    protected override Vector2 AnimationDrawOffset=>base.AnimationDrawOffset+Vector2.Down*(ZFixed>>8);
    internal override Texture2D CurrentDrawTexture=>DrawsDamagePalette?base.CurrentDrawTexture:Animation.CurrentTextureForPalette(Palette);
    internal TwinrovaMinionCharacter(TwinrovaEnvironment world,int id,int parent,Vector2 position,int z,int angle)
    {
        if(id is not(0x5d or 0x5e))throw new ArgumentOutOfRangeException(nameof(id));
        World=world;Id=id;ParentSlot=parent;Angle=angle;ZFixed=(z>>8)<<8;Palette=world.Data.Actor(id).Palette;
        InitializeEnemy(position.Floor(),EnemyCharacterConfiguration.FromImported(world.Data.Actor(id)),positionedOam:true,paletteVariants:[2,3,Palette]);
        Health=0;Visible=false;Animation.SetFrameCounter(0);ZIndex=ObjectDrawPriority.FromVisible(0x82);
    }
    internal void PublishHit(int item,int damage=0,int invincibility=0,int knockback=0,Vector2? origin=null)
    {
        _pending=item;if(damage!=0)Health=Math.Max(0,Health-damage);
        if(invincibility!=0)InvincibilityCounter=invincibility;
        if(knockback!=0){KnockbackCounter=knockback;KnockbackAngle=OracleObjectMovement.Shared.RelativeAngle(Position.Floor(),(origin??Position).Floor())^16;}
    }
    private void Move()=>Position=ApplyMovementSpeed(OracleObjectPosition.FromPixels(Position),Speed,Angle).PrecisePosition;
    private void BatPalette(){Counter=(Counter-1)&255;Palette=((Counter&4)>>2)+2;}
    internal void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(IsDead)return;
        try
        {
            // JUST_HIT ($04) precedes decrementing recoil ($05). The bat
            // ignores both statuses and continues its own movement, while
            // health-zero dispatch waits until the recoil counter expires.
            bool recoil=State!=0&&!JustHit&&HasActiveKnockback;
            if(recoil)KnockbackCounter--;
            if(State==0){Health=World.Data.Actor(Id).Health;RestartAnimation(0);World.Entities.Random.Next();}
            else if(_pending>=0)
            {
                if(Id==0x5d&&_pending!=0)
                {
                    // The shield table's result is overwritten by this helper.
                    if(_pending is 2 or 3)World.Entities.OnSoundRequested(SoundId.SndClink);
                    Angle=OracleObjectMovement.Shared.RelativeAngle(Position.Floor(),(frame.EnemyTarget??frame.Player.Position).Floor())^16;
                }
            }
            else if(!recoil&&Health==0&&Id==0x5e)
            {
                Killed=true;Finish();
                spawns.Add(new EnemyDeathPuffSpawn(Position,EnemyId:Id,DecrementsRoomCount:false,ZHigh:ZFixed>>8));return;
            }
            var related=_resolve(ParentSlot);
            var parent=related as MergedTwinrovaRoomEntity;
            if(Id==0x5d&&related is not null&&parent is null)
                throw new NotSupportedException($"twinrovaIce.s: related ENEMY slot${ParentSlot:x2} now contains {related.GetType().Name}; native state alias is not represented.");
            if(Id==0x5e)
            {
                if(parent is null){Finish();return;}
                if(((Counter+1)&31)==0)World.Entities.OnSoundRequested(SoundId.SndBoomerang);
            }
            if(State==0)
            {
                State=1;Visible=true;
                if(Id==0x5d){Speed=0x46;Counter=120;World.Entities.OnSoundRequested(SoundId.SndPoof);}
                else{Speed=0x50;Counter2=80;Counter=World.Entities.Random.Next().Value;World.Entities.OnSoundRequested(SoundId.SndVeranFairyAttack);}
                return;
            }
            if(State==1)
            {
                if(Id==0x5d)Counter=(Counter-1)&255;
                else{BatPalette();if(Counter2!=0)Counter2--;}
                if((Id==0x5d?Counter:Counter2)!=0){AdvanceAnimation();return;}
                State=2;
                if(Id==0x5e)Angle=OracleObjectMovement.Shared.RelativeAngle(Position.Floor(),(frame.EnemyTarget??frame.Player.Position).Floor());
            }
            if(State!=2)throw new NotSupportedException($"ENEMY${Id:x2}: state${State:x2}.");
            if(Id==0x5e)
            {
                if((int)Position.Y>=176||(int)Position.X>=240){Finish();return;}
                BatPalette();Move();AdvanceAnimation();return;
            }
            if(parent is null||parent.Actor.Health==0||parent.Actor.State==0x0a)
            {World.Entities.TryCreateVirePuff(Position,ZFixed>>8,0);Finish();return;}
            Move();var room=World.Entities.ActiveRoom;
            var probe=EnemyAdjacentWallResolver.Shared.Probe(Position,Angle,p=>
                p.X<0||p.X>=240||p.Y<0||p.Y>=176||room.IsSolidForEnemyMovement(p,holesAreWalls:true));
            Angle=EnemyAdjacentWallResolver.Shared.BounceAngle(Angle,probe);
            if(probe.XBlocked||probe.YBlocked)World.Entities.OnSoundRequested(SoundId.SndClink);
        }
        finally{_pending=-1;AdvanceInvincibilityCounter();QueueRedraw();}
    }
}
