namespace Oracles;

// seasons/parts/aquamentusProjectile.s: an invisible delayed three-shot
// producer. Each shot samples the physical body page again at launch.
internal sealed partial class AquamentusProjectileRoomEntity : TransitionOffsetNode2D,IRoomEntity,IFixedRoomEntity,
    IRoomEntityLifetime,IPostObjectLinkContactRoomEntity,IObjectCollisionHeightRoomEntity,
    IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze,INativePartHealthRoomEntity
{
    private const int NativePartId=0x40; // constants/seasons/parts.s: PART_AQUAMENTUS_PROJECTILE.
    private readonly RoomEntityManager _world;
    private readonly EnemyAnimationPlayer _animation;
    private readonly int _parentSlot;
    private readonly ObjectCollisionDatabase _collisions;
    private bool _pendingDelete;
    private int _slot=-1;
    internal int State {get;private set;}
    internal int Counter {get;private set;}
    internal int Angle {get;private set;}
    internal int Palette {get;private set;}=2;
    internal int SubId {get;}
    internal int ZHigh {get;private set;}
    public Node2D Node=>this;
    public bool Finished {get;private set;}
    public bool UpdatesDuringDialogue=>State==0;
    public bool UpdatesDuringRoomEntityFreeze=>State==0;
    public int CollisionZ=>ZHigh;
    internal Rect2 Bounds=>new(Position.Floor()-new Vector2(2,2),new(4,4));
    internal AquamentusProjectileRoomEntity(AquamentusProjectileSpawn spawn,EnemyProjectileVisualRecord visual,RoomEntityManager world)
    {
        _world=world;_parentSlot=spawn.ParentSlot;SubId=spawn.SubId;Angle=spawn.Angle;_collisions=ObjectCollisionDatabase.For(visual.AssetRoot);
        // Native PART$40 accepts Link and shield levels 2/3 only.
        if(Enumerable.Range(0,32).Any(type=>_collisions.PartEnabled(NativePartId,type)!=(type is 0 or 2 or 3)))
            throw new NotSupportedException("PART_AQUAMENTUS_PROJECTILE $40: unsupported native partActiveCollisions mask.");
        AssetRoot=visual.AssetRoot;Position=spawn.Position.Floor();Visible=false;
        _animation=new(this,1);_animation.Load(EnemyVisualSource.LoadComposite(visual.Sprites,AssetRoot),visual.Animations,
            visual.TileBase,visual.Palette,sourceGrayscaleInverted:visual.SourceGrayscaleInverted,paletteVariants:[0,1,2,3]);
        _animation.SetAnimation(0);
        if(_world.ResolveEnemySlot(_parentSlot)?.Node is AquamentusCharacter parent)ZHigh=parent.ZFixed>>8;
    }
    internal void BindSlot(int slot)=>_slot=slot;
    public void ClearHealthAndCollision()=>_pendingDelete=true;
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(Finished)return;
        if(_pendingDelete){Finished=true;Visible=false;return;}
        if(State==0)
        {
            State=1;Counter=40;
            if(SubId!=0)return;
            Angle=OracleObjectMovement.Shared.RelativeAngle(Position,frame.LinkTarget??frame.Player.EnemyContactPosition);
            foreach(int offset in new[]{3,-3})_world.TryCreatePart(new AquamentusProjectileSpawn(_parentSlot,Position,(Angle+offset)&31,1));
            return;
        }
        if(State==1)
        {
            if(Counter!=0&&--Counter!=0)return;
            State=2;
            var parent=_world.ResolveEnemySlot(_parentSlot)?.Node;
            Position=((parent?.Position??Vector2.Zero).Floor()+new Vector2(240,240)).PosMod(256);
            ZHigh=parent is AquamentusCharacter body?body.ZFixed>>8:0;
            Visible=true;ZIndex=ObjectDrawPriority.FixedHighPriorityZIndex;return;
        }
        Position=NativeObjectMovement.ApplySpeed(_world.RuntimeState,OracleObjectPosition.FromPixels(Position),ObjectSpeed.Speed200,Angle).PrecisePosition;
        if(!NativePartBoundary.IsInside(_world.ActiveRoom,Position,Angle,CommonEnemyBehavior.Shared.ProjectileBoundaryOffsets))
        {Finished=true;Visible=false;return;}
        if(((frame.Counter^(0xd0+_slot))&1)!=0){Palette=(Palette+1)&3;QueueRedraw();}
    }
    public void HandleLinkContact(Player player)
    {
        if(State==0||Finished||_pendingDelete||!player.EnemyContactHeightOverlaps(ZHigh))return;
        if(player.IsUsingShield&&_collisions.PartEnabled(NativePartId,player.Inventory.ShieldLevel)&&player.CanAcceptShieldCollision&&
            RoomEntityManager.ObjectCollisionXYOverlaps(Bounds,player.ShieldCollisionBounds))
        {_pendingDelete=true;_world.OnSoundRequested(SoundId.SndClink2);return;}
        if(_collisions.PartEnabled(NativePartId,0)&&player.NativeObjectVulnerable&&player.OverlapsEnemyCollision(Bounds,ZHigh)&&
            player.ApplyEnemyContactDamage(Position,2,RingDamageSource.Generic,34,15))_pendingDelete=true;
    }
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset)=>SetTransitionDrawOffset(offset);
    public override void _Draw(){if(Visible&&!Finished)DrawTexture(_animation.CurrentTextureForPalette(Palette),new Vector2(-16,-16+ZHigh)+TransitionDrawOffset);}
}
