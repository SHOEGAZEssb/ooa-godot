namespace Oracles;

internal sealed class AquamentusRoomEntity : CombatEnemyRoomEntityAdapter<AquamentusCharacter>,IFixedRoomEntity,
    INativeEnemySlotRoomEntity,IScreenTransitionPreloadRoomEntity,IPostObjectLinkContactRoomEntity,
    IPostObjectMeleeCollisionRoomEntity,IPostObjectItemCollisionRoomEntity,ILinkSwordStateAwareRoomEntity,
    ISwordAttackerKnockbackRoomEntity,ISeedCollisionTarget
{
    private readonly AquamentusDatabase _data;
    private readonly ObjectCollisionDatabase _collisions;
    private int _swordCollision=ItemCollisionType.L1Sword,_attackerFrames;
    public bool MeleeReportsContact {get;private set;}
    public override bool CountsAsEnemy=>Entity.OwnsRoomCount&&!Entity.IsDead;
    public override int DimitriCollisionMode=>Entity.ActiveCollisionMode;
    internal AquamentusRoomEntity(AquamentusCharacter actor,EnemyCombatSourceDescriptor source,RoomEntityManager world)
        :base(actor,actor.SetTransitionDrawOffset,EnemyCombatDescriptor.WithContactDamage(source,actor,actor.Record.DamageQuarters,
            actor.Damage,_=>false,(_,_)=>{},world.OnSoundRequested,deathPuffAllowed:()=>false,
            completedOutcome:()=>actor.SubId==3?RoomEnemyOutcome.EnemyDieUncounted():RoomEnemyOutcome.SilentDeletion(false)),
            collisionZ:()=>actor.ZFixed>>8)
    {_data=AquamentusDatabase.For(source.AssetRoot);_collisions=ObjectCollisionDatabase.For(source.AssetRoot);}
    public void BindEnemySlot(int slot,Func<int,IRoomEntity?> resolve)=>Entity.BindSlot(slot);
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)=>Entity.UpdateFrame(frame,spawns);
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)=>
        throw new InvalidOperationException("Aquamentus preload requires the actual Link owner.");
    public ScreenTransitionPresentation PrepareForScreenTransition(Player? player,ICollection<RoomEntitySpawn> spawns)
    {
        if(player is null)throw new InvalidOperationException("Aquamentus preload requires the actual Link owner.");
        if(Entity.InitializationPending)Entity.UpdateFrame(new(player,0,false),spawns);
        return Entity.Visible?ScreenTransitionPresentation.Visible:ScreenTransitionPresentation.Hidden;
    }
    public override void HandleLinkContact(Player player)=>HandleNativeLinkContact(player,Entity.Record.DamageQuarters);
    public void SetLinkSwordState(SwordActionState state,int level,int? itemCollisionType=null)=>
        _swordCollision=itemCollisionType??SwordCollision.Type(state,level);
    public override bool ApplySwordHit(Rect2 bounds,Vector2 origin,int damage,EnemyKnockbackStrength strength,ICollection<RoomEntitySpawn> spawns)=>
        Hit(_swordCollision,bounds,origin,damage,spawns);
    public bool ApplyItemCollision(RoomEntityItemCollision collision,Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns)=>
        Hit((int)collision,bounds,origin,damage,spawns);
    private bool Overlaps(int collision,Rect2 bounds)=>Entity.CollisionEnabled&&!Entity.NativeHitPending&&Entity.InvincibilityCounter==0&&
        _collisions.EnemyEnabled(SeasonsEnemyId.Aquamentus,collision)&&RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds,bounds);
    private bool Hit(int collision,Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns)
    {
        MeleeReportsContact=false;_attackerFrames=0;
        if(!Overlaps(collision,bounds))return false;
        int effect=_collisions.Effect(DimitriCollisionMode,collision);
        switch(effect)
        {
            case CollisionEffect.None:case CollisionEffect.Effect20:return true;
            case CollisionEffect.Effect21:
                if(!Entity.Damage(origin,damage))return false;
                CombatDescriptor.RequestSound(SoundId.SndBossDamage);MeleeReportsContact=true;return true;
            case CollisionEffect.Effect1c:
                Entity.PublishHarmlessHit();MeleeReportsContact=true;return true;
            case CollisionEffect.Effect15:case CollisionEffect.Effect16:case CollisionEffect.Effect17:
                if(!Entity.ArmorHit(origin))return false;
                _attackerFrames=_data.AttackerRecoil[effect-CollisionEffect.Effect15];
                spawns.Add(new EnemyClinkSpawn(CollisionMidpoint(Entity.Position,origin)));
                MeleeReportsContact=true;return true;
            default:throw new NotSupportedException($"aquamentus.s ENEMY$78:${Entity.SubId:x2}: mode${DimitriCollisionMode:x2}, item${collision:x2}, effect${effect:x2} has no response.");
        }
    }
    public bool TryGetSwordAttackerKnockback(EnemyKnockbackStrength strength,out SwordAttackerKnockback response)
    {response=new(Entity.Position,_attackerFrames);return _attackerFrames!=0;}
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds,Vector2 origin,SeedRecord seed,int collision,ICollection<RoomEntitySpawn> spawns)
    {
        if(!Overlaps(collision,bounds))return default;
        int effect=_collisions.Effect(DimitriCollisionMode,collision);
        return effect switch
        {
            CollisionEffect.None=>new(true,SeedHitResult.None,false),
            CollisionEffect.Effect20=>new(true,seed.SeedItem==ItemId.MysterySeed?SeedHitResult.ActivateRandomSeed:SeedHitResult.Activate,true),
            _=>throw new NotSupportedException($"aquamentus.s: seed collision${collision:x2}, mode${DimitriCollisionMode:x2}, effect${effect:x2}.")
        };
    }
    public override void OnFinished(ICollection<RoomEntitySpawn> spawns)
    {
        if(Entity.SubId==3)spawns.Add(new EnemyDeathPuffSpawn(Entity.Position.Floor(),EnemyId:SeasonsEnemyId.Aquamentus,
            DropsItem:false,ZHigh:Entity.ZFixed>>8));
        base.OnFinished(spawns);
    }
}
