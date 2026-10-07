using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class BariRoomEntity : CombatEnemyRoomEntityAdapter<BariCharacter>,
    IFixedRoomEntity, IScreenTransitionPreloadRoomEntity, IPostObjectLinkContactRoomEntity,
    IPostObjectMeleeCollisionRoomEntity, IPostObjectItemCollisionRoomEntity,
    IBoomerangCollisionRoomEntity, ISomariaBlockCollisionRoomEntity, ISeedCollisionTarget,
    IExpertPunchHittableRoomEntity
{
    private readonly BariBehaviorProfile _behavior = EnemyBehaviorTables.Shared.Bari;
    private readonly Func<byte> _random;
    private Player? _player;
    protected override bool Stunned => Entity.StunCounter != 0;
    public bool MeleeReportsContact => true;
    public override int DimitriCollisionMode => Entity.CollisionMode;
    private IReadOnlyList<EnemyBehaviorValue> Effects => Entity.CollisionMode == _behavior.ElectricMode ?
        _behavior.ElectricEffects : _behavior.NormalEffects;
    internal BariRoomEntity(BariCharacter enemy, EnemyCombatSourceDescriptor source, Action<int> sound, Func<byte> random)
        : base(enemy, enemy.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(source, enemy, enemy.Record.DamageQuarters,
                enemy.TakeSwordHit, enemy.TakeBurnHit, enemy.ApplySwordKnockback, sound, EnemySwordResponse.Knockback,
                deathPuffAllowed: () => !enemy.SplitCompleted,
                completedOutcome: () => enemy.SplitCompleted ? RoomEnemyOutcome.ReplacementDeletion(source.CountsAsEnemy) :
                    RoomEnemyOutcome.EnemyDie(source.KillableEnemyIndex)), collisionZ: () => enemy.ZFixed >> 8)
    { _random = random; }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        _player = frame.Player;
        switch (Entity.UpdateFrame(frame.Player.Position, frame.Counter))
        {
            case BariUpdateEvent.BeginSplit:
                spawns.Add(new KillEnemyPuffSpawn(Entity.Position.Floor(), Entity.ZFixed >> 8));
                CombatDescriptor.RequestSound(SoundId.SndKillEnemy);
                break;
            case BariUpdateEvent.SpawnChildren:
                foreach (int offset in new[] { _behavior.ChildOffset, -_behavior.ChildOffset })
                    spawns.Add(new BariChildSpawn(new((Mathf.FloorToInt(Entity.Position.X) + offset) & 0xff,
                        Mathf.FloorToInt(Entity.Position.Y)), (Entity.Angle + offset) & ObjectAngle.Mask,
                        KillableEnemyIndex, CombatDescriptor.Source!.Value.ObjectFlags, Entity.ZFixed >> 8));
                break;
        }
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Entity.PrepareForScreenTransition();
        return ScreenTransitionPresentation.Visible;
    }
    public override void HandleLinkContact(Player player)
    {
        if (GaleCaught || Entity.NativeHitPending || !Entity.CollisionEnabled ||
            !player.EnemyContactHeightOverlaps(CollisionZ)) return;
        int shield = player.Inventory.ShieldLevel;
        if (player.IsUsingShield && shield is >= 1 and <= 3 && _behavior.ActiveCollisions[shield].Value != 0 &&
            RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, player.ShieldCollisionBounds))
        {
            int effect = Effects[shield].Value;
            if (effect == 0) return;
            if (effect == CollisionEffect.Effect05)
            {
                if (player.CanAcceptShieldCollision)
                {
                    player.ApplyShieldCollisionRecoil(Entity.Position, 8, 11);
                    Entity.NotifyCollision(shield);
                }
                return;
            }
            int inv = Entity.InvincibilityCounter;
            base.HandleLinkContact(player);
            if (Entity.InvincibilityCounter != inv) Entity.NotifyCollision(shield);
            return;
        }
        if (Entity.StunCounter != 0 || !player.OverlapsEnemyCollision(Entity.CollisionBounds, CollisionZ)) return;
        if (Entity.CollisionMode == _behavior.ElectricMode)
        {
            if (!player.PatchCollisionsEnabled || player.IsDying || player.InvincibilityFrames != 0 || player.KnockbackFrames != 0) return;
            Entity.ReceiveElectricCollision();
            player.ApplyElectricShock(Entity.Position);
        }
        else if (player.ApplyEnemyContactDamage(Entity.Position, Entity.Record.DamageQuarters))
            Entity.NotifyCollision(ItemCollisionType.Link);
    }
    public override bool ApplySwordHit(Rect2 bounds, Vector2 origin, int damage,
        EnemyKnockbackStrength strength, ICollection<RoomEntitySpawn> spawns)
    {
        if (!CanHit(bounds)) return false;
        if (Entity.CollisionMode == _behavior.ElectricMode) return Shock();
        return base.ApplySwordHit(bounds, origin, damage, strength, spawns);
    }
    public override bool ApplyBiggoronSwordCollision(Rect2 bounds, Vector2 origin, int damage, ICollection<RoomEntitySpawn> spawns)
    {
        if (!CanHit(bounds) || _behavior.ActiveCollisions[ItemCollisionType.BiggoronSword].Value == 0) return false;
        return ApplyEffect(ItemCollisionType.BiggoronSword, bounds, origin, damage, spawns);
    }
    public bool ApplyExpertPunch(Rect2 bounds, Vector2 origin, int damage, ICollection<RoomEntitySpawn> spawns) =>
        ApplyEffect(ItemCollisionType.ExpertPunch, bounds, origin, damage, spawns);
    public bool ApplyItemCollision(RoomEntityItemCollision collision, Rect2 bounds, Vector2 origin,
        int damage, ICollection<RoomEntitySpawn> spawns) => ApplyEffect((int)collision, bounds, origin, damage, spawns);
    private bool CanHit(Rect2 bounds) => !GaleCaught && Entity.CollisionEnabled && !Entity.NativeHitPending &&
        Entity.InvincibilityCounter == 0 && RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, bounds);
    private bool Shock()
    {
        if (_player is null) throw new InvalidOperationException("ENEMY_BARI $3c effect$36 requires the live Link collision owner.");
        Entity.ReceiveElectricCollision();
        _player.ApplyElectricShock(Entity.Position);
        return true;
    }
    private bool ApplyEffect(int collision, Rect2 bounds, Vector2 origin, int damage, ICollection<RoomEntitySpawn> spawns)
    {
        if (!CanHit(bounds) || _behavior.ActiveCollisions[collision].Value == 0) return false;
        int effect = Effects[collision].Value;
        if (effect is CollisionEffect.None or CollisionEffect.Effect20) return true;
        if (effect == CollisionEffect.ElectricShock) return Shock();
        if (!Entity.TakeSwordHit(origin, damage)) return false;
        Entity.NotifyCollision(collision);
        switch (effect)
        {
            case CollisionEffect.SwordLowKnockback: Entity.ApplySwordKnockback(origin, EnemyKnockbackStrength.Low); break;
            case CollisionEffect.Sword: Entity.ApplySwordKnockback(origin, EnemyKnockbackStrength.Normal); break;
            case CollisionEffect.SwordHighKnockback: Entity.ApplySwordKnockback(origin, EnemyKnockbackStrength.High); break;
            case CollisionEffect.SwordNoKnockback: break;
            default: throw new NotSupportedException($"ENEMY_BARI $3c mode${Entity.CollisionMode:x2}, collision${collision:x2}, effect${effect:x2} requires its native response.");
        }
        CombatDescriptor.RequestSound(SoundId.SndDamageEnemy);
        return true;
    }
    protected override bool TryApplySwitchHookEffect(int effect, SwitchHookItem hook, Vector2 linkPosition)
    {
        if (effect == CollisionEffect.ElectricShock)
        {
            bool hit = Shock(); hook.NotifyObjectCollision(); return hit;
        }
        if (!ApplyEffect(ItemCollisionType.SwitchHook, hook.CollisionBounds, linkPosition, hook.HitDamage, new List<RoomEntitySpawn>())) return false;
        hook.NotifyObjectCollision(); return true;
    }
    public bool ApplySomariaBlockCollision(SomariaBlock block, ICollection<RoomEntitySpawn> spawns) =>
        ApplySomariaBlockCollision(block, Entity.Record.RawDamage, Entity.NativeHitPending, spawns);
    public override SeedHitResult ApplySeedHit(Rect2 bounds, Vector2 origin, int seedItem, ICollection<RoomEntitySpawn> spawns)
    {
        if (seedItem == ItemId.MysterySeed) throw new InvalidOperationException("ENEMY_BARI $3c requires Mystery Seed's live collision type.");
        if (!new SeedSatchelDatabase().TryGet(seedItem, out var seed)) return SeedHitResult.None;
        return ApplySeedCollision(bounds, origin, seed, seed.Collision & ObjectCollisionFlags.TypeMask, spawns).Effect;
    }
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds, Vector2 origin, SeedRecord seed, int collision,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (!CanHit(bounds) || _behavior.ActiveCollisions[collision].Value == 0) return default;
        int effect = Effects[collision].Value;
        if (effect == CollisionEffect.GaleSeed)
        {
            if (!TryCatchGale(bounds, CollisionZ, _random)) return default;
        }
        else if (!ApplyEffect(collision, bounds, origin, -unchecked((sbyte)seed.Damage), spawns)) return default;
        if (effect == 0) return new(true, SeedHitResult.None, false);
        return new(true, seed.SeedItem == ItemId.MysterySeed ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate,
            effect is CollisionEffect.Effect20 or CollisionEffect.GaleSeed);
    }
}

internal sealed record BariChildSpawn(Vector2 Position, int Angle, int KillableEnemyIndex, int ObjectFlags, int ZHigh) : RoomEntitySpawn;
