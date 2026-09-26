using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class EyesoarRoomEntity : CombatEnemyRoomEntityAdapter<EyesoarActor>, IFixedRoomEntity,
    IPlayerRestriction, IPlayerForcedMovement, IScreenTransitionPreloadRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    ILinkSwordStateAwareRoomEntity, IItemCollisionHittableRoomEntity, IPostObjectMeleeCollisionRoomEntity,
    IExpertPunchHittableRoomEntity, ISeedCollisionTarget
{
    private SwordActionState _swordState;
    private int _swordLevel = 1;
    public bool MeleeReportsContact { get; private set; }
    internal EyesoarRoomEntity(EyesoarActor actor)
        : base(actor, actor.SetTransitionDrawOffset,
            EnemyCombatDescriptor.Special(EnemyCombatComponent.WithContactDamage(
                () => actor.IsDead, () => actor.CollisionBounds, (_, _) => false, _ => false,
                actor.OverlapsLink, () => actor.Position, actor.Record.DamageQuarters, () => null),
                countsAsEnemy: !actor.IsChild, killableEnemyIndex: 0,
                completedOutcome: () => actor.IsSpawner ? RoomEnemyOutcome.SilentDeletion(false) :
                    actor.IsChild ? RoomEnemyOutcome.EnemyDieUncounted() : RoomEnemyOutcome.BossTeardown(0),
                soundRequested: actor.RequestSound),
            collisionZ: () => actor.ZFixed >> 8) { }
    public override int DimitriCollisionType => Entity.Record.Id;
    public override int DimitriCollisionMode => Entity.CollisionMode;
    public bool UpdatesDuringDialogue => Entity.Initializing;
    public bool UpdatesDuringRoomEntityFreeze => Entity.Initializing;
    public bool DisablesSword => Entity.ControlsDisabled;
    public bool DisablesItems => DisablesSword;
    public bool DisablesMovement => DisablesSword;
    public bool DisablesMenus => DisablesSword;
    public void UpdatePlayerForcedMovement(Player player)
    { if (!Entity.IsChild && !Finished) Entity.Entry.Update(player); }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        bool starts = Entity.Initializing && Entity.IsSpawner;
        Entity.UpdateFrame(frame.Player, frame.Counter, spawns);
        if (starts) Entity.Entry.Arm();
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.Initializing)
        {
            Entity.InitializeState(spawns);
            if (Entity.IsSpawner) Entity.Entry.Arm();
        }
        return ScreenTransitionPresentation.Hidden;
    }
    public void SetLinkSwordState(SwordActionState state, int swordLevel) { _swordState = state; _swordLevel = swordLevel; }
    public override bool ApplySwordHit(Rect2 hitbox, Vector2 sourcePosition, int damage,
        EnemyKnockbackStrength strength, ICollection<RoomEntitySpawn> spawns)
    {
        int item = SwordCollision.Type(_swordState, _swordLevel);
        return Hit(item, hitbox, sourcePosition, damage, spawns, melee: true);
    }
    public bool ApplyItemCollision(RoomEntityItemCollision collision, Rect2 hitbox, Vector2 sourcePosition,
        int damage, ICollection<RoomEntitySpawn> spawns) => Hit((int)collision, hitbox, sourcePosition, damage, spawns);
    public bool ApplyExpertPunch(Rect2 hitbox, Vector2 sourcePosition, int damage,
        ICollection<RoomEntitySpawn> spawns) => Hit(0x0b, hitbox, sourcePosition, damage, spawns, melee: true);
    private bool Eligible(int item, Rect2 hitbox) => Entity.CollisionEnabled && !Entity.JustHit && Entity.InvincibilityCounter == 0 &&
        Entity.Data.CollisionEnabled(Entity.Record.Id, item) && RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, hitbox);
    private bool Hit(int item, Rect2 hitbox, Vector2 source, int damage, ICollection<RoomEntitySpawn> spawns, bool melee = false)
    {
        if (!Eligible(item, hitbox)) return false;
        int effect = Entity.Data.CollisionEffect(Entity.CollisionMode, item);
        MeleeReportsContact = effect != CollisionEffect.None;
        switch (effect)
        {
            case CollisionEffect.None: return melee;
            case CollisionEffect.SwordNoKnockback: Entity.ApplyNativeHit(item, damage, 32); CombatDescriptor.RequestSound(SoundId.SndDamageEnemy); return true;
            case CollisionEffect.Effect21: Entity.ApplyNativeHit(item, damage, 32); CombatDescriptor.RequestSound(SoundId.SndBossDamage); return true;
            case CollisionEffect.Effect1b:
                Entity.ApplyNativeHit(item, 0, -20); spawns.Add(new EnemyClinkSpawn(CollisionMidpoint(Entity.Position, source))); return true;
            case CollisionEffect.Effect1c:
            case CollisionEffect.Effect20: Entity.MarkContact(item); return true;
            default: throw new NotSupportedException($"ENEMY_EYESOAR ${Entity.Record.Id:x2}:${Entity.Record.SubId:x2}: item ${item:x2}, effect ${effect:x2} is unsupported.");
        }
    }
    public override bool ApplySwitchHookHit(SwitchHookItem hook, Vector2 linkPosition)
    {
        if (!RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, 0, 7) || !Eligible(13, hook.CollisionBounds)) return false;
        int effect = Entity.Data.CollisionEffect(Entity.CollisionMode, ItemCollisionType.SwitchHook);
        if (effect == CollisionEffect.SwitchHook) { Entity.BeginSwitchHook(linkPosition); hook.LatchEnemy(Entity); return true; }
        if (effect == CollisionEffect.SwordNoKnockback)
        { Entity.ApplyNativeHit(13, hook.HitDamage, 32); hook.NotifyObjectCollision(); CombatDescriptor.RequestSound(SoundId.SndDamageEnemy); return true; }
        return effect == CollisionEffect.None;
    }
    public override SeedHitResult ApplySeedHit(Rect2 hitbox, Vector2 sourcePosition, int seedItem, ICollection<RoomEntitySpawn> spawns)
        => throw new InvalidOperationException("ENEMY_EYESOAR $7b / CHILD $11 seed collision requires the projectile's imported attributes and live collision type.");
    public SeedCollisionResponse ApplySeedCollision(Rect2 hitbox, Vector2 sourcePosition, SeedRecord seed,
        int collisionType, ICollection<RoomEntitySpawn> spawns)
    {
        // Neither body nor child sets var3f bit5: func_07_47b7 leaves a
        // Mystery Seed's selected collision type intact, including damage.
        if (!Eligible(collisionType, hitbox)) return default;
        int effect = Entity.Data.CollisionEffect(Entity.CollisionMode, collisionType);
        if (effect == CollisionEffect.None) return new(true, SeedHitResult.None, false);
        Hit(collisionType, hitbox, sourcePosition, -(sbyte)seed.Damage, spawns);
        return new(true, seed.SeedItem == ItemId.MysterySeed ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate, effect == CollisionEffect.Effect20);
    }
    public override void HandleLinkContact(Player player)
    {
        if (!Entity.CollisionEnabled || Entity.JustHit || !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, player.EnemyContactZ, 7)) return;
        if (player.IsUsingShield && RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, player.ShieldCollisionBounds))
        {
            if (!player.CanAcceptShieldCollision) return;
            int effect = Entity.Data.CollisionEffect(Entity.CollisionMode, player.Inventory.ShieldLevel);
            var (invincibility, recoil) = effect switch { CollisionEffect.Effect05 => (8, 11), CollisionEffect.Effect06 => (15, 19), CollisionEffect.Effect07 => (22, 25),
                _ => throw new NotSupportedException($"ENEMY_EYESOAR shield effect ${effect:x2} is unsupported.") };
            player.ApplyShieldCollisionRecoil(Entity.Position, invincibility, recoil); Entity.MarkContact(player.Inventory.ShieldLevel); return;
        }
        if (player.AcceptsRoomEntityContact && Player.EnemyCollisionOverlaps(player.EnemyContactPosition, Entity.CollisionBounds))
        { player.ApplyEnemyContactDamage(Entity.Position, Entity.Record.DamageQuarters); Entity.MarkContact(0); }
    }
}
