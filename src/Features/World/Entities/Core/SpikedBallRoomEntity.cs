using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class SpikedBallRoomEntity(SpikedBallPart part, Action<int> sound)
    : RoomEntityAdapter<SpikedBallPart>(part, part.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime, INativePartHealthRoomEntity,
        IPostObjectLinkContactRoomEntity, ILinkContactEntity, IObjectCollisionHeightRoomEntity,
        IPostObjectMeleeCollisionRoomEntity, ISwordHittableRoomEntity, ILinkSwordStateAwareRoomEntity,
        ISwordAttackerKnockbackRoomEntity, IExpertPunchHittableRoomEntity, IPostObjectItemCollisionRoomEntity, ISeedCollisionTarget,
        ISomariaBlockCollisionRoomEntity, IBoomerangCollisionRoomEntity,
        IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    private readonly BallChainBehaviorProfile _data = EnemyBehaviorTables.Shared.BallChain;
    private int _swordCollision = ItemCollisionType.L1Sword;
    private int _attackerFrames;
    private Vector2? _shieldClink;
    public bool Finished => Entity.Finished;
    public bool MeleeReportsContact => true;
    public int CollisionZ => Entity.ZHigh;
    public bool UpdatesDuringDialogue => Entity.State == 0;
    public bool UpdatesDuringRoomEntityFreeze => Entity.State == 0;
    public void ClearHealthAndCollision() => Entity.ClearHealthAndCollision();
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        Entity.UpdateFrame(frame.Player.EnemyContactPosition);
    }
    public void CollectContactSpawns(ICollection<RoomEntitySpawn> spawns)
    {
        if (_shieldClink is { } clink) { spawns.Add(new EnemyClinkSpawn(clink)); _shieldClink = null; }
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.State == 0) Entity.UpdateFrame(Vector2.Zero); // State0 does not read the target.
        return ScreenTransitionPresentation.Visible;
    }
    public void SetLinkSwordState(SwordActionState state, int level) =>
        _swordCollision = SwordCollision.Type(state, level);
    private bool Overlaps(int collision, Rect2 bounds) => Entity.CollisionEnabled && !Entity.PendingCollision &&
        Entity.InvincibilityCounter == 0 && _data.BallMask[collision].Value != 0 &&
        RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, bounds);
    public BoomerangCollisionResponse ApplyBoomerangCollision(BoomerangItem item, ICollection<RoomEntitySpawn> spawns)
    {
        var data = BoomerangCollisionDatabase.Shared;
        if (!item.CollisionEnabled || !data.PartEnabled(0x2a) || !Overlaps(ItemCollisionType.L1Boomerang, item.CollisionBounds) ||
            !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, item.ZHigh, 7)) return default;
        int effect = data.Effect(EnemyCollisionMode.SpikedBall);
        if (effect != CollisionEffect.Effect1b) throw new NotSupportedException($"PART_SPIKED_BALL $2a: boomerang effect${effect:x2}.");
        Entity.InvincibilityCounter = data.DeflectionInvincibility;
        Entity.PublishCollision(ItemCollisionType.L1Boomerang);
        return new(true, true, BoomerangCollisionResponse.Midpoint(Entity.Position, item.Position));
    }
    public bool ApplySomariaBlockCollision(SomariaBlock block, ICollection<RoomEntitySpawn> spawns)
    {
        if (!block.CollisionEnabled || !SomariaCollisionDatabase.Shared.Part(0x2a).Block ||
            !RoomEntityManager.ObjectCollisionZOverlaps(Entity.ZHigh, block.ZHigh, 7) ||
            !Overlaps(ItemCollisionType.SomariaBlock, block.CollisionBounds)) return false;
        int effect = SomariaCollisionDatabase.Shared.Effects(EnemyCollisionMode.SpikedBall).Block;
        switch (effect)
        {
            case CollisionEffect.None: return true;
            case CollisionEffect.Effect2d:
                // collisionEffect2d only marks the ITEM for deletion. It does
                // not publish a PART hit, recoil, sound, or pending damage.
                block.Flags |= 0x20;
                return true;
            default: throw new NotSupportedException($"PART_SPIKED_BALL $2a Somaria collision: effect ${effect:x2} is not represented.");
        }
    }
    public bool ApplySwordHit(Rect2 bounds, Vector2 origin, int damage, EnemyKnockbackStrength strength,
        ICollection<RoomEntitySpawn> spawns) => ApplyItem(_swordCollision, bounds, spawns);
    public bool ApplyItemCollision(RoomEntityItemCollision collision, Rect2 bounds, Vector2 origin, int damage,
        ICollection<RoomEntitySpawn> spawns) => ApplyItem((int)collision, bounds, spawns);
    public bool ApplyExpertPunch(Rect2 bounds, Vector2 origin, int damage, ICollection<RoomEntitySpawn> spawns) =>
        ApplyItem(ItemCollisionType.ExpertPunch, bounds, spawns);
    private bool ApplyItem(int collision, Rect2 bounds, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Overlaps(collision, bounds)) return false;
        int effect = _data.BallEffects[collision].Value;
        _attackerFrames = 0;
        switch (effect)
        {
            case CollisionEffect.None: return true;
            case CollisionEffect.Effect20: return true;
            case CollisionEffect.Effect1c: Entity.PublishCollision(collision); return true;
            case CollisionEffect.Effect15:
            case CollisionEffect.Effect16:
            case CollisionEffect.Effect17:
                Entity.InvincibilityCounter = -28; // ENEMYDMG_34=$60,$e4,0,0.
                var recoil = EnemyBehaviorTables.Shared.ArmoredSwordAttackerKnockback;
                _attackerFrames = effect == CollisionEffect.Effect15 ? recoil.LowFrames : effect == CollisionEffect.Effect16 ? recoil.NormalFrames : recoil.HighFrames;
                sound(SoundId.SndBombLand);
                break;
            case CollisionEffect.Effect1b:
                Entity.InvincibilityCounter = -20; // ENEMYDMG_28=$60,$ec,0,0.
                break;
            default: throw new NotSupportedException($"PART_SPIKED_BALL $2a collision ${collision:x2}: effect ${effect:x2} is not represented.");
        }
        Entity.PublishCollision(collision);
        spawns.Add(new EnemyClinkSpawn(OracleObjectMath.ToPixelPosition(
            (Entity.Position + OracleObjectMath.ToPixelPosition(bounds.GetCenter())) / 2)));
        return true;
    }
    public bool TryGetSwordAttackerKnockback(EnemyKnockbackStrength strength, out SwordAttackerKnockback response)
    {
        response = new(Entity.Position, _attackerFrames);
        bool result = _attackerFrames != 0;
        _attackerFrames = 0;
        return result;
    }
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds, Vector2 origin, SeedRecord seed, int collision,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (!Overlaps(collision, bounds)) return default;
        int effect = _data.BallEffects[collision].Value;
        if (effect == CollisionEffect.None) return new(true, SeedHitResult.None, false);
        if (effect != CollisionEffect.Effect20) throw new NotSupportedException($"PART_SPIKED_BALL $2a seed collision ${collision:x2}: effect ${effect:x2} is not represented.");
        return new(true, seed.SeedItem == ItemId.MysterySeed ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate, true);
    }
    public SeedHitResult ApplySeedHit(Rect2 bounds, Vector2 origin, int seedItem, ICollection<RoomEntitySpawn> spawns)
    {
        if (seedItem == ItemId.MysterySeed) throw new InvalidOperationException("PART_SPIKED_BALL $2a requires Mystery's live collision type.");
        if (!new SeedSatchelDatabase().TryGet(seedItem, out var seed)) return SeedHitResult.None;
        return ApplySeedCollision(bounds, origin, seed, seed.Collision & ObjectCollisionFlags.TypeMask, spawns).Effect;
    }
    public void HandleLinkContact(Player player)
    {
        if (!Entity.CollisionEnabled || Entity.PendingCollision ||
            !RoomEntityManager.ObjectCollisionZOverlaps(Entity.ZHigh, player.EnemyContactZ, 7)) return;
        if (Entity.InvincibilityCounter == 0 && player.IsUsingShield &&
            RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, player.ShieldCollisionBounds))
        {
            if (!player.CanAcceptShieldCollision) return;
            int level = Math.Clamp(player.Inventory.ShieldLevel, 1, 3);
            int effect = _data.BallEffects[level].Value;
            if (effect is not (CollisionEffect.Effect16 or CollisionEffect.Effect17)) throw new NotSupportedException($"PART_SPIKED_BALL $2a shield effect ${effect:x2} is not represented.");
            Entity.InvincibilityCounter = -28;
            Entity.PublishCollision(level);
            _shieldClink = OracleObjectMath.ToPixelPosition((Entity.Position +
                OracleObjectMath.ToPixelPosition(player.ShieldCollisionBounds.GetCenter())) / 2);
            player.ApplyShieldCollisionRecoil(Entity.Position, effect == CollisionEffect.Effect17 ? 22 : 15, effect == CollisionEffect.Effect17 ? 25 : 19);
            sound(SoundId.SndBombLand);
            return;
        }
        if (player.OverlapsEnemyCollision(Entity.CollisionBounds) &&
            player.ApplyEnemyContactDamage(Entity.Position, 2, RingDamageSource.Generic, 34, 15))
            Entity.PublishCollision(ItemCollisionType.Link);
    }
}
