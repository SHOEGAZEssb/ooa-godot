using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class SpinyBeetleCoverRoomEntity : CombatEnemyRoomEntityAdapter<SpinyBeetleCoverCharacter>,
    IFixedRoomEntity, INativeBraceletRoomEntity, IItemCollisionHittableRoomEntity, IScrollPersistentRoomEntity,
    ISeedCollisionTarget, IBurningEnemyTarget, IPostObjectMeleeCollisionRoomEntity,
    IPostObjectItemCollisionRoomEntity, IBoomerangCollisionRoomEntity, ISomariaBlockCollisionRoomEntity
{
    private Action<INativeBraceletRoomEntity>? _publishGrabbable;
    private readonly Func<bool> _freePart;
    public bool MeleeReportsContact => !Entity.Rock;
    public bool PersistsAcrossScroll => Entity.Held;
    public void EnterRoom(OracleRoomData room) => Entity.EnterRoom(room);
    public override bool FreezesDuringSeedBurn => false;
    public override int DimitriCollisionType => EnemyBehaviorTables.Shared.SpinyCoverAttributes[0].Value;
    public override int DimitriCollisionMode => Entity.Rock ? EnemyCollisionMode.Rock : EnemyCollisionMode.Bush;
    internal SpinyBeetleCoverRoomEntity(SpinyBeetleCoverCharacter actor, Action<int> sound, Func<bool> freePart)
        : base(actor, actor.SetTransitionDrawOffset, EnemyCombatDescriptor.Special(
            new EnemyCombatComponent(() => actor.IsDead, () => actor.CollisionBounds,
                actor.BreakCover, damage => actor.BreakCover(actor.Position, damage), _ => { }, () => null,
                (_, _) => { }), false, 0,
            completedOutcome: () => RoomEnemyOutcome.SilentDeletion(decrementsRoomCount: false),
            soundRequested: sound,
            source: new EnemyCombatSourceDescriptor(0x58, actor.Parent.Record.SubId,
                0x80 | (actor.Rock ? EnemyCollisionMode.Rock : EnemyCollisionMode.Bush), 2, 0,
                EnemyHandlerKind.None, "object_code/common/enemies/bushOrRock.s:enemyCode58")),
            collisionZ: () => actor.ZHigh) { _freePart = freePart; }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        bool wasGrabbable = Entity.Grabbable;
        Entity.UpdateFrame(frame.Player, frame.Counter, spawns);
        if (wasGrabbable && Entity.Grabbable) _publishGrabbable?.Invoke(this);
    }
    public void BindGrabbablePublisher(Action<INativeBraceletRoomEntity> publish) => _publishGrabbable = publish;
    public void UpdateHeldPosition(Player player) => Entity.UpdateHeldPosition(player);
    public bool TryUseBracelet(Player player, Vector2I direction) => Entity.UseBracelet(player, direction);
    public override bool ApplySwordHit(Rect2 bounds,Vector2 origin,int damage,EnemyKnockbackStrength strength,ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.Rock) return NativeCollisionEligible(ItemCollisionType.L1Sword,bounds,
            EnemyBehaviorTables.Shared.SpinyCoverActiveCollisions,Entity.NativeHitPending);
        return base.ApplySwordHit(bounds,origin,damage,strength,spawns);
    }
    public bool ApplySomariaBlockCollision(SomariaBlock block,ICollection<RoomEntitySpawn> spawns) =>
        ApplySomariaBlockCollision(block,EnemyBehaviorTables.Shared.SpinyCoverAttributes[5].Value,Entity.NativeHitPending,spawns);
    public int BurnTargetId => DimitriCollisionType;
    public int BurnZFixed { get => Entity.ZFixed; set => Entity.ZFixed = value; }
    public override void ReleaseBurn() => Entity.ReleaseBurn();
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds,Vector2 origin,SeedRecord seed,int collision,ICollection<RoomEntitySpawn> spawns)
    {
        var data = EnemyBehaviorTables.Shared;
        if (!NativeCollisionEligible(collision,bounds,data.SpinyCoverActiveCollisions,Entity.NativeHitPending)) return default;
        int effect = (Entity.Rock ? data.SpinyRockEffects : data.SpinyBushEffects)[collision].Value;
        switch(effect)
        {
            case CollisionEffect.None: return new(true,SeedHitResult.None,false);
            case CollisionEffect.Effect20: break;
            case CollisionEffect.Burn:
                Entity.BeginBurn(-unchecked((sbyte)seed.Damage));
                if (_freePart()) spawns.Add(new BurningEnemySpawn(this));
                break;
            case CollisionEffect.SwordNoKnockback: Entity.BreakCover(origin,-unchecked((sbyte)seed.Damage)); break;
            default: throw new NotSupportedException($"bushOrRock.s: ENEMY$58:${Entity.Parent.Record.SubId:x2} seed effect${effect:x2}.");
        }
        return new(true,seed.SeedItem==ItemId.MysterySeed?SeedHitResult.ActivateRandomSeed:SeedHitResult.Activate,effect==CollisionEffect.Effect20);
    }
    public bool ApplyItemCollision(RoomEntityItemCollision collision, Rect2 bounds, Vector2 origin,
        int damage, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.CollisionEnabled || !RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, bounds)) return false;
        var data = EnemyBehaviorTables.Shared;
        if (data.SpinyCoverActiveCollisions[(int)collision].Value == 0) return false;
        int effect = (Entity.Rock ? data.SpinyRockEffects : data.SpinyBushEffects)[(int)collision].Value;
        if (effect is CollisionEffect.None or CollisionEffect.Effect20 or CollisionEffect.Effect1c) return true;
        return Entity.BreakCover(origin, damage);
    }
}
