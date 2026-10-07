using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class GiantBladeTrapRoomEntity : CombatEnemyRoomEntityAdapter<GiantBladeTrapCharacter>,
    IFixedRoomEntity, IScreenTransitionPreloadRoomEntity, IUpdatesDuringDialogueRoomEntity,
    IUpdatesDuringRoomEntityFreeze, ISwordAttackerKnockbackRoomEntity, ISeedCollisionTarget,
    IPostObjectItemCollisionRoomEntity, IExpertPunchHittableRoomEntity, ISomariaBlockCollisionRoomEntity,
    IBoomerangCollisionRoomEntity
{
    private readonly Action<int> _sound;
    private readonly GiantBladeTrapBehaviorProfile _behavior = EnemyBehaviorTables.Shared.GiantBladeTrap;
    internal GiantBladeTrapRoomEntity(GiantBladeTrapCharacter enemy,
        EnemyCombatSourceDescriptor source, Action<int> sound)
        : base(enemy, enemy.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(source, enemy, enemy.Record.DamageQuarters,
                enemy.TakeSwordHit, enemy.TakeBurnHit, (_, _) => { }, sound,
                EnemySwordResponse.Armored, acceptedHitSound: 0)) => _sound = sound;

    public bool UpdatesDuringDialogue => Entity.InitializationPending;
    public bool UpdatesDuringRoomEntityFreeze => Entity.InitializationPending;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame();
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.InitializationPending) Entity.UpdateFrame();
        return ScreenTransitionPresentation.Visible;
    }

    public override void HandleLinkContact(Player player)
    {
        if (!Entity.CollisionEnabled || !player.EnemyContactHeightOverlaps(0)) return;
        if (player.IsUsingShield && CombatDescriptor.Combat.Intersects(player.ShieldCollisionBounds))
        {
            if (player.CanAcceptShieldCollision)
            {
                // collisionEffect07 uses LINKDMG_18 for the Wooden Shield;
                // Iron/Mirror use effect06/LINKDMG_14. ENEMYDMG_1c only
                // publishes JUST_HIT, which this trap's handler ignores.
                bool wooden = player.Inventory.ShieldLevel == 1;
                player.ApplyShieldCollisionRecoil(Entity.Position, wooden ? 0x16 : 0x0f, wooden ? 0x19 : 0x13);
                Entity.DeferNativeHitStatus();
                _sound(SoundId.SndBombLand);
            }
            return;
        }
        if (Entity.OverlapsLink(player.EnemyContactPosition))
            player.ApplyEnemyContactDamage(Entity.Position, Entity.Record.DamageQuarters, RingDamageSource.BladeTrap);
    }

    public override bool ApplySwordHit(Rect2 bounds, Vector2 origin, int damage,
        EnemyKnockbackStrength strength, ICollection<RoomEntitySpawn> spawns)
    {
        if (!base.ApplySwordHit(bounds, origin, damage, strength, spawns)) return false;
        Entity.DeferNativeHitStatus();
        spawns.Add(new EnemyClinkSpawn(CollisionMidpoint(Entity.Position, bounds.GetCenter())));
        _sound(SoundId.SndBombLand);
        return true;
    }

    public bool TryGetSwordAttackerKnockback(EnemyKnockbackStrength strength, out SwordAttackerKnockback response)
    {
        var profile = EnemyBehaviorTables.Shared.ArmoredSwordAttackerKnockback;
        int frames = strength switch
        {
            EnemyKnockbackStrength.Low => profile.LowFrames,
            EnemyKnockbackStrength.Normal => profile.NormalFrames,
            EnemyKnockbackStrength.High => profile.HighFrames,
            _ => 0
        };
        response = new(Entity.Position, frames);
        return frames != 0;
    }

    protected override bool TryApplySwitchHookEffect(int effect, SwitchHookItem hook, Vector2 linkPosition)
    {
        if (effect != CollisionEffect.Effect1b || !Entity.TakeDeflectionHit()) return false;
        Entity.DeferNativeHitStatus();
        hook.NotifyObjectCollision(CollisionMidpoint(Entity.Position, hook.Position));
        return true;
    }

    public bool ApplyItemCollision(RoomEntityItemCollision collision, Rect2 bounds, Vector2 origin,
        int damage, ICollection<RoomEntitySpawn> spawns) =>
        ApplyIneffectiveWeaponCollision((int)collision, bounds, _behavior.ActiveCollisions, _behavior.CollisionEffects, out _);
    public bool ApplyExpertPunch(Rect2 bounds, Vector2 origin, int damage, ICollection<RoomEntitySpawn> spawns) =>
        ApplyIneffectiveWeaponCollision(ItemCollisionType.ExpertPunch, bounds, _behavior.ActiveCollisions, _behavior.CollisionEffects, out _);
    public bool ApplySomariaBlockCollision(SomariaBlock block, ICollection<RoomEntitySpawn> spawns) =>
        ApplySomariaBlockCollision(block, Entity.Record.RawDamage, Entity.NativeHitPending, spawns);

    public override SeedHitResult ApplySeedHit(Rect2 bounds, Vector2 origin, int seedItem,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (seedItem == ItemId.MysterySeed) throw new InvalidOperationException("Giant Blade Trap $2a requires Mystery Seed's live collision type.");
        if (!new SeedSatchelDatabase().TryGet(seedItem, out var seed)) return SeedHitResult.None;
        return ApplySeedCollision(bounds, origin, seed, seed.Collision & ObjectCollisionFlags.TypeMask, spawns).Effect;
    }

    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds, Vector2 origin, SeedRecord seed,
        int collisionType, ICollection<RoomEntitySpawn> spawns)
    {
        bool hit = ApplyIneffectiveWeaponCollision(collisionType, bounds,
            _behavior.ActiveCollisions, _behavior.CollisionEffects, out _);
        if (!hit) return default;
        int effect = _behavior.CollisionEffects[collisionType].Value;
        return effect == 0 ? new(true, SeedHitResult.None, false) :
            new(true, seed.SeedItem == ItemId.MysterySeed ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate, true);
    }
}
