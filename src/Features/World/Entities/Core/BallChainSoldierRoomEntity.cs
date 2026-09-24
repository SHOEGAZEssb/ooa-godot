using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class BallChainSoldierRoomEntity : CombatEnemyRoomEntityAdapter<BallChainSoldierCharacter>,
    IFixedRoomEntity, IPostObjectLinkContactRoomEntity, IPostObjectMeleeCollisionRoomEntity,
    IPostObjectItemCollisionRoomEntity, ISeedCollisionTarget, IExpertPunchHittableRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity,
    ISomariaBlockCollisionRoomEntity, IBoomerangCollisionRoomEntity
{
    protected override bool BoomerangHitPending => Entity.PendingHit;
    protected override void MarkBoomerangHit() => Entity.MarkContact();
    private readonly Func<int, bool> _enemySlots;
    private readonly SpikedBallDatabase _weapon;
    public bool MeleeReportsContact => true;
    public bool UpdatesDuringDialogue => Entity.State == 0;
    public bool UpdatesDuringRoomEntityFreeze => Entity.State == 0;
    public bool ApplySomariaBlockCollision(SomariaBlock block, ICollection<RoomEntitySpawn> spawns) =>
        ApplySomariaBlockCollision(block, Entity.Record.RawDamage, Entity.PendingHit, spawns, deferNativeStatus: false);
    protected override void ApplySomariaEnemyDamage(SomariaBlock block, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.TakeSomariaHit(block.Position, -block.Damage))
            throw new InvalidOperationException("ENEMY$4b rejected an eligible Somaria effect$2f collision.");
        CombatDescriptor.RequestSound(OracleSoundEngine.SndDamageEnemy);
    }
    internal BallChainSoldierRoomEntity(BallChainSoldierCharacter soldier, EnemyCombatSourceDescriptor source,
        Action<int> sound, Func<int, bool> enemySlots, SpikedBallDatabase weapon)
        : base(soldier, soldier.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(source, soldier, soldier.Record.DamageQuarters,
                soldier.TakeSwordHit, _ => false, (_, _) => { }, sound, EnemySwordResponse.NoKnockback),
            collisionZ: () => soldier.ZFixed >> 8)
    { _enemySlots = enemySlots; _weapon = weapon; }
    private void Advance(Vector2 link, ICollection<RoomEntitySpawn> spawns)
    {
        Entity.UpdateFrame(link, link, _enemySlots(4) ? 4 : 0, () =>
        {
            var head = new SpikedBallPart(Entity, _weapon, 0);
            spawns.Add(new SpikedBallSpawn(head, CombatDescriptor.RequestSound));
            for (int subid = 1; subid <= 3; subid++)
                spawns.Add(new SpikedBallSpawn(new(Entity, _weapon, subid, head), CombatDescriptor.RequestSound));
        });
        if (Entity.IsDead && !Entity.DiedInHazard && CombatDescriptor.Combat.CreateDeathPuff() is { } puff)
            spawns.Add(puff with { DecrementsRoomCount = CombatDescriptor.CountsAsEnemy });
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Advance(frame.Player.EnemyContactPosition, spawns);
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.State == 0) Advance(Vector2.Zero, spawns);
        return Entity.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }
    private bool Overlaps(Rect2 bounds) => Entity.CollisionEnabled && !Entity.PendingHit &&
        Entity.InvincibilityCounter == 0 && RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, bounds);
    private bool Hit(Rect2 bounds, Vector2 origin, int damage, ICollection<RoomEntitySpawn> spawns) =>
        Overlaps(bounds) && base.ApplySwordHit(bounds, origin, damage, EnemyKnockbackStrength.None, spawns);
    public override bool ApplySwordHit(Rect2 bounds, Vector2 origin, int damage, EnemyKnockbackStrength strength,
        ICollection<RoomEntitySpawn> spawns) => Hit(bounds, origin, damage, spawns);
    public bool ApplyExpertPunch(Rect2 bounds, Vector2 origin, int damage, ICollection<RoomEntitySpawn> spawns) => Hit(bounds, origin, damage, spawns);
    public bool ApplyItemCollision(RoomEntityItemCollision collision, Rect2 bounds, Vector2 origin, int damage,
        ICollection<RoomEntitySpawn> spawns)
    {
        var data = EnemyBehaviorTables.Shared.BallChain;
        if (data.BodyMask[(int)collision].Value == 0) return false;
        if (data.BodyEffects[(int)collision].Value != 0x0b)
            throw new NotSupportedException($"ENEMY $4b item collision ${(int)collision:x2}: expected effect$0b.");
        return Hit(bounds, origin, damage, spawns);
    }
    public override SeedHitResult ApplySeedHit(Rect2 bounds, Vector2 origin, int item, ICollection<RoomEntitySpawn> spawns)
    {
        if (item == 0x24) throw new InvalidOperationException("ENEMY $4b requires Mystery's live collision type.");
        if (!new SeedSatchelDatabase().TryGet(item, out var seed)) return SeedHitResult.None;
        return ApplySeedCollision(bounds, origin, seed, seed.Collision & 0x7f, spawns).Effect;
    }
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds, Vector2 origin, SeedRecord seed, int collision,
        ICollection<RoomEntitySpawn> spawns)
    {
        var data = EnemyBehaviorTables.Shared.BallChain;
        if (!Overlaps(bounds) || data.BodyMask[collision].Value == 0) return default;
        int effect = data.BodyEffects[collision].Value;
        if (effect == 0) return new(true, SeedHitResult.None, false);
        if (effect == 0x0b) Hit(bounds, origin, -(sbyte)seed.Damage, spawns);
        else if (effect != 0x20) throw new NotSupportedException($"ENEMY $4b seed collision ${collision:x2}: effect ${effect:x2} is not represented.");
        return new(true, seed.SeedItem == 0x24 ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate, effect != 0x0b);
    }
    public override void HandleLinkContact(Player player)
    {
        if (!Entity.CollisionEnabled || Entity.PendingHit ||
            !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, player.EnemyContactZ, 7)) return;
        if (Entity.InvincibilityCounter == 0 && player.IsUsingShield &&
            RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, player.ShieldCollisionBounds))
        {
            if (!player.CanAcceptShieldCollision) return;
            bool wooden = player.Inventory.ShieldLevel == 1;
            player.ApplyShieldCollisionRecoil(Entity.Position, wooden ? 15 : 8, wooden ? 19 : 11);
            Entity.MarkContact();
            CombatDescriptor.RequestSound(OracleSoundEngine.SndBombLand);
            return;
        }
        if (player.OverlapsEnemyCollision(Entity.CollisionBounds) && player.ApplyEnemyContactDamage(
            Entity.Position, Entity.Record.DamageQuarters, RingDamageSource.Generic, 34, 15)) Entity.MarkContact();
    }
}

internal sealed record SpikedBallSpawn(SpikedBallPart Part, Action<int> Sound) : RoomEntitySpawn;
