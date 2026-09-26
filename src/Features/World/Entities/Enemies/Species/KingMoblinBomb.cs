using Godot;

namespace oracleofages;

internal sealed partial class KingMoblinBomb : EnemyCharacter
{
    internal KingMoblinBoss Boss { get; private set; } = null!;
    internal KingMoblinMinion? Minion { get; private set; }
    internal int State { get; private set; }
    internal int Fuse { get; private set; }
    internal int Flashes { get; private set; }
    internal int ZFixed => _motion.ZFixed;
    internal int Radius { get; private set; }
    internal bool Held => !Small && State==2 && !_released;
    internal bool ReservedBraceletChildActive => !Small && !IsDead && State == 2 && _released && !_settled;
    private CarriedObjectMotion _motion;
    private int _angle, _speed, _flashLimit;
    private bool _released, _damagedLink;
    private bool _settled;
    private Player? _holder;
    private bool Small => Minion is not null;
    internal override bool CollisionEnabled => false;
    protected override Vector2 AnimationDrawOffset => base.AnimationDrawOffset+new Vector2(0,_motion.ZFixed>>8);
    internal void Initialize(KingMoblinBoss boss,KingMoblinMinion? minion)
    {
        Boss=boss; Minion=minion;
        if(minion is null) boss.Bomb=this; else minion.Bomb=this;
        InitializeEnemy(minion?.Position ?? boss.Position,
            EnemyCharacterConfiguration.FromImported(boss.Data.Actor(minion is null?0x3f:0x47)),positionedOam:true);
        Name=minion is null?"KingMoblinBomb":"KingMoblinMinionBomb";
    }
    internal void Throw(int angle,int? speedZ,int? speed=null)
    {
        State=Small?2:3; _angle=angle;
        if(speedZ.HasValue) _motion.SpeedZ=speedZ.Value;
        if(speed.HasValue) _speed=speed.Value;
    }
    internal void GrabByBoss(Vector2 position) {State=1; Position=position;}
    internal void UpdateFrame(RoomEntityFrame frame)
    {
        if(IsDead) return;
        if(Small) {UpdateSmall(frame); QueueRedraw(); return;}
        switch(State)
        {
            case 0:
                State=1; _speed=0x55; Position+=new Vector2(0,8);
                Fuse=Boss.Data.Bytes("fuses")[Boss.World.Random.Next().Value&3];
                _flashLimit=Boss.Data.Bytes("flashes")[Boss.Health-1]; break;
            case 1: TickFuse(frame.Counter); break;
            case 2:
                if(_settled) State=4;
                else if(_released && Position.Y<0x30 && (_motion.ZFixed>>8)==0)
                { _motion.SpeedZ >>= 1; _motion.SpeedRaw=ObjectSpeed.Speed40; }
                TickFuse(frame.Counter); break;
            case 3:
                if(!TickFuse(frame.Counter)) break;
                if(OracleObjectMath.UpdateSpeedZ(ref _motion.ZFixed,ref _motion.SpeedZ,0x20))
                {
                    Boss.World.Sound(SoundId.SndBombLand);
                    int rebound=(-_motion.SpeedZ)>>1;
                    if(rebound> -0x80) {State=4; _motion.ZFixed=0; TickFuse(frame.Counter); break;}
                    _motion.SpeedZ=rebound;
                }
                Move(); break;
            case 4: TickFuse(frame.Counter); break;
            case 5:
                if(AnimationParameter==255) {Finish(); break;}
                Radius=AnimationParameter;
                if(Radius!=0)
                {
                    DamageLink(frame.Player);
                    if(Boss.CollisionEnabled && Boss.InvincibilityCounter==0 &&
                        RoomEntityManager.ObjectCollisionXYOverlaps(BlastBounds(),Boss.CollisionBounds)) Boss.BombHit();
                }
                AdvanceAnimation(); break;
            default: throw new System.NotSupportedException($"PART_KING_MOBLIN_BOMB $3f: unsupported native state ${State:x2}.");
        }
        QueueRedraw();
    }
    private bool TickFuse(int frame)
    {
        if(Fuse!=0 && (frame&1)!=0) return true;
        if(Fuse!=0) Fuse--;
        if(Fuse!=0) return true;
        if((AnimationParameter&1)!=0)
        {
            Animation.ConsumeParameter();
            if(++Flashes>=_flashLimit) {Explode(); return false;}
        }
        AdvanceAnimation(); return true;
    }
    internal void UpdateBraceletChild(Player player)
    {
        // PART $47 state 2 is its own ballistic throw, not the reserved
        // bracelet child used by PART $3f state 2.
        if(Small || IsDead || State!=2 || _settled) return;
        if(!_released)
        {
            if(!player.IsCarryingObject) {_released=true; _motion.SpeedZ=0;}
            else _motion.Hold(player);
        }
        if(_released)
        {
            _motion.AdvanceHorizontal(Boss.World.Throwing, point => Boss.World.Room.IsSolid(point) &&
                !Boss.World.Throwing.CanPassSolidTile(Boss.World.Room,point));
            if(_motion.AdvanceVertical(Boss.World.Bracelet))
            {
                Boss.World.Sound(SoundId.SndBombLand);
                _settled=!_motion.Bounce(Boss.World.Throwing);
            }
        }
        Position=_motion.GroundPosition; QueueRedraw();
    }
    private void UpdateSmall(RoomEntityFrame frame)
    {
        if(State==0) {State=1; _speed=0x50; _motion.SpeedZ=-0x280;}
        switch(State)
        {
            case 1: Position=Minion!.Position; _motion.ZFixed=Minion.ZFixed; break;
            case 2:
                Move();
                if(OracleObjectMath.UpdateSpeedZ(ref _motion.ZFixed,ref _motion.SpeedZ,0x20)) Explode();
                else AdvanceAnimation(); break;
            case 3:
                AdvanceAnimation(); Radius=AnimationParameter;
                if(Radius==255) Finish(); break;
        }
    }
    private void Explode()
    {
        if(Held) _holder?.EndCarriedObjectPose();
        _holder=null;
        // Both native handlers write oamFlags=$0a: bit 3 selects the
        // persistent common-sprites bank, not the common-items bomb sheet.
        var explosion=Boss.World.Throwing;
        var record=Boss.Data.Actor(Small?0x47:0x3f) with {
            Sprites=[explosion.ExplosionSprite],TileBase=explosion.ExplosionTileBase,Palette=explosion.ExplosionPalette};
        InitializeEnemy(Position,EnemyCharacterConfiguration.FromImported(record),initialAnimation:1,positionedOam:true);
        ZIndex=Small?NpcCharacter.FixedLowPriorityZIndex:NpcCharacter.BehindLinkZIndex;
        State=Small?3:5; Radius=0; Boss.World.Sound(SoundId.SndExplosion);
    }
    private Rect2 BlastBounds() => new(Position-new Vector2(Radius,Radius),new Vector2(Radius*2,Radius*2));
    internal void HandleLinkContact(Player player)
    {
        // PART_BOMB $47 uses collisionEffect02 after all objects; $3f
        // explicitly damages Link inside its part handler instead.
        if(Small && !IsDead && State==3) DamageLink(player);
    }
    private void DamageLink(Player player)
    {
        if(!Small && Radius==0 || _damagedLink || !player.NativeObjectVulnerable ||
            Small && !RoomEntityManager.ObjectCollisionZOverlaps(ZFixed>>8,player.EnemyContactZ,7) ||
            !RoomEntityManager.ObjectCollisionXYOverlaps(BlastBounds(),new Rect2(player.EnemyContactPosition-new Vector2(5,5),new Vector2(10,10)))) return;
        _damagedLink=player.ApplyEnemyContactDamage(Position,Boss.Data.Actor(Small?0x47:0x3f).DamageQuarters,
            RingDamageSource.Generic,Small?0x22:20,Small?0x0f:0x10);
    }
    internal bool TryUseBracelet(Player player,Vector2I direction)
    {
        if(Small || IsDead) return false;
        if(Held)
        {
            _motion.Release(player,direction,Boss.World.Bracelet); _released=true; _holder=null; return true;
        }
        if(State!=4 || player.IsCarryingObject || player.CutsceneControlled) return false;
        Vector2 delta=Position-(player.Position+(Vector2)player.FacingVector*6);
        if(System.Math.Abs(delta.X)>=13 || System.Math.Abs(delta.Y)>=13) return false;
        State=2; _released=false; _settled=false; _holder=player; _motion=new(Position);
        player.BeginCarriedObjectPose(); return true;
    }
    private void Move() {var p=Position; OracleObjectMovement.Shared.ApplySpeed(ref p,_speed,_angle); Position=p;}
    public override void _ExitTree() {if(Held) _holder?.EndCarriedObjectPose(); base._ExitTree();}
}
