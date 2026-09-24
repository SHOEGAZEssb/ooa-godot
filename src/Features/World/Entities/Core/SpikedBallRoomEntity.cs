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
    private int _swordCollision = 4;
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
        _swordCollision = state == SwordActionState.Spin ? (level >= 2 ? 8 : 7)
            : state is SwordActionState.Held or SwordActionState.Charged ? 9 : Math.Clamp(level, 1, 3) + 3;
    private bool Overlaps(int collision, Rect2 bounds) => Entity.CollisionEnabled && !Entity.PendingCollision &&
        Entity.InvincibilityCounter == 0 && _data.BallMask[collision].Value != 0 &&
        RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, bounds);
    public BoomerangCollisionResponse ApplyBoomerangCollision(BoomerangItem item, ICollection<RoomEntitySpawn> spawns)
    {
        var data = BoomerangCollisionDatabase.Shared;
        if (!item.CollisionEnabled || !data.PartEnabled(0x2a) || !Overlaps(0x17, item.CollisionBounds) ||
            !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, item.ZHigh, 7)) return default;
        int effect = data.Effect(0x74);
        if (effect != 0x1b) throw new NotSupportedException($"PART_SPIKED_BALL $2a: boomerang effect${effect:x2}.");
        Entity.InvincibilityCounter = data.DeflectionInvincibility;
        Entity.PublishCollision(0x17);
        return new(true, true, BoomerangCollisionResponse.Midpoint(Entity.Position, item.Position));
    }
    public bool ApplySomariaBlockCollision(SomariaBlock block, ICollection<RoomEntitySpawn> spawns)
    {
        if (!block.CollisionEnabled || !SomariaCollisionDatabase.Shared.Part(0x2a).Block ||
            !RoomEntityManager.ObjectCollisionZOverlaps(Entity.ZHigh, block.ZHigh, 7) ||
            !Overlaps(0x15, block.CollisionBounds)) return false;
        int effect = SomariaCollisionDatabase.Shared.Effects(0x74).Block;
        switch (effect)
        {
            case 0: return true;
            case 0x2d:
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
        ApplyItem(0x0b, bounds, spawns);
    private bool ApplyItem(int collision, Rect2 bounds, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Overlaps(collision, bounds)) return false;
        int effect = _data.BallEffects[collision].Value;
        _attackerFrames = 0;
        switch (effect)
        {
            case 0: return true;
            case 0x20: return true;
            case 0x1c: Entity.PublishCollision(collision); return true;
            case 0x15:
            case 0x16:
            case 0x17:
                Entity.InvincibilityCounter = -28; // ENEMYDMG_34=$60,$e4,0,0.
                var recoil = EnemyBehaviorTables.Shared.ArmoredSwordAttackerKnockback;
                _attackerFrames = effect == 0x15 ? recoil.LowFrames : effect == 0x16 ? recoil.NormalFrames : recoil.HighFrames;
                sound(OracleSoundEngine.SndBombLand);
                break;
            case 0x1b:
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
        if (effect == 0) return new(true, SeedHitResult.None, false);
        if (effect != 0x20) throw new NotSupportedException($"PART_SPIKED_BALL $2a seed collision ${collision:x2}: effect ${effect:x2} is not represented.");
        return new(true, seed.SeedItem == 0x24 ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate, true);
    }
    public SeedHitResult ApplySeedHit(Rect2 bounds, Vector2 origin, int seedItem, ICollection<RoomEntitySpawn> spawns)
    {
        if (seedItem == 0x24) throw new InvalidOperationException("PART_SPIKED_BALL $2a requires Mystery's live collision type.");
        if (!new SeedSatchelDatabase().TryGet(seedItem, out var seed)) return SeedHitResult.None;
        return ApplySeedCollision(bounds, origin, seed, seed.Collision & 0x7f, spawns).Effect;
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
            if (effect is not (0x16 or 0x17)) throw new NotSupportedException($"PART_SPIKED_BALL $2a shield effect ${effect:x2} is not represented.");
            Entity.InvincibilityCounter = -28;
            Entity.PublishCollision(level);
            _shieldClink = OracleObjectMath.ToPixelPosition((Entity.Position +
                OracleObjectMath.ToPixelPosition(player.ShieldCollisionBounds.GetCenter())) / 2);
            player.ApplyShieldCollisionRecoil(Entity.Position, effect == 0x17 ? 22 : 15, effect == 0x17 ? 25 : 19);
            sound(OracleSoundEngine.SndBombLand);
            return;
        }
        if (player.OverlapsEnemyCollision(Entity.CollisionBounds) &&
            player.ApplyEnemyContactDamage(Entity.Position, 2, RingDamageSource.Generic, 34, 15))
            Entity.PublishCollision(0);
    }
}
