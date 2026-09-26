using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class ColorChangingGelRoomEntity
    : CombatEnemyRoomEntityAdapter<ColorChangingGelCharacter>, IFixedRoomEntity,
      IScreenTransitionPreloadRoomEntity, IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    internal ColorChangingGelRoomEntity(
        ColorChangingGelCharacter gel,
        EnemyCombatSourceDescriptor combatSource,
        Action<int> soundRequested)
        : base(
            gel,
            gel.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                gel,
                gel.Record.DamageQuarters,
                gel.TakeSwordHit,
                gel.TakeBurnHit,
                (_, _) => { },
                soundRequested,
                EnemySwordResponse.NoKnockback, acceptedHitSound: 0),
            collisionZ: () => gel.ZHigh)
    { }

    public override int DimitriCollisionMode => Entity.CollisionMode;

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        UpdateDuringScreenTransition();
        return Entity.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }

    public void UpdateDuringScreenTransition(RoomEntityFrame frame = default)
    {
        // bank0._updateEnemiesIfStateIsZero permits enemyCode47 during scrolling.
        // Its color check can postpone state 0 on a non-red floor; preserve each
        // retry's common RNG/property reload, then freeze state $08 (counter 150,
        // animation $03) as soon as initialization makes the gel visible.
        if (Entity.State == ColorChangingGelState.Uninitialized)
            Entity.UpdateFrame();
    }

    protected override bool TryApplySwitchHookEffect(int effect, SwitchHookItem hook, Vector2 linkPosition)
    {
        if (effect is not (CollisionEffect.Effect1c or CollisionEffect.SwordNoKnockback) || !Entity.TakeSwitchHookHit(hook.HitDamage)) return false;
        hook.NotifyObjectCollision();
        return true;
    }

    public override SeedHitResult ApplySeedHit(Rect2 hitbox, Vector2 sourcePosition,
        int seedItem, ICollection<RoomEntitySpawn> spawns)
    {
        if (!CombatDescriptor.Combat.Intersects(hitbox)) return SeedHitResult.None;
        if (seedItem == ItemId.MysterySeed)
            return Entity.TakeMysterySeedHit() ? SeedHitResult.Activate : SeedHitResult.None;
        if (seedItem == ItemId.ScentSeed)
            return Entity.TakeSeedHit(ItemCollisionType.ScentSeed, 2) ? SeedHitResult.Activate : SeedHitResult.None;
        // Matching-color seeds use effect20/ENEMYDMG44. They activate their
        // own effect without igniting, stunning or damaging the Gel.
        if (Entity.CollisionMode == EnemyBehaviorTables.Shared.ColorChangingGel.ImmuneCollisionMode &&
            seedItem is >= 0x20 and <= 0x23)
            return Entity.TakeSeedHit(seedItem - 0x20 + 0x1b, 0) ? SeedHitResult.Activate : SeedHitResult.None;
        return base.ApplySeedHit(hitbox, sourcePosition, seedItem, spawns);
    }

    public override void OnFinished(ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.NormalDeathDispatched && !Entity.DiedInHazard && !GaleCaught &&
            CombatDescriptor.Combat.CreateDeathPuff() is { } puff)
            spawns.Add(puff with { DecrementsRoomCount = CombatDescriptor.CountsAsEnemy });
        base.OnFinished(spawns);
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame();
}
