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
        int item = _swordState == SwordActionState.Spin ? (_swordLevel >= 2 ? 8 : 7) :
            _swordState is SwordActionState.Held or SwordActionState.Charged ? 9 : _swordLevel >= 3 ? 6 : _swordLevel >= 2 ? 5 : 4;
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
        MeleeReportsContact = effect != 0;
        switch (effect)
        {
            case 0: return melee;
            case 0x0b: Entity.ApplyNativeHit(item, damage, 32); CombatDescriptor.RequestSound(OracleSoundEngine.SndDamageEnemy); return true;
            case 0x21: Entity.ApplyNativeHit(item, damage, 32); CombatDescriptor.RequestSound(OracleSoundEngine.SndBossDamage); return true;
            case 0x1b:
                Entity.ApplyNativeHit(item, 0, -20); spawns.Add(new EnemyClinkSpawn(CollisionMidpoint(Entity.Position, source))); return true;
            case 0x1c:
            case 0x20: Entity.MarkContact(item); return true;
            default: throw new NotSupportedException($"ENEMY_EYESOAR ${Entity.Record.Id:x2}:${Entity.Record.SubId:x2}: item ${item:x2}, effect ${effect:x2} is unsupported.");
        }
    }
    public override bool ApplySwitchHookHit(SwitchHookItem hook, Vector2 linkPosition)
    {
        if (!RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, 0, 7) || !Eligible(13, hook.CollisionBounds)) return false;
        int effect = Entity.Data.CollisionEffect(Entity.CollisionMode, 13);
        if (effect == 0x2e) { Entity.BeginSwitchHook(linkPosition); hook.LatchEnemy(Entity); return true; }
        if (effect == 0x0b)
        { Entity.ApplyNativeHit(13, hook.HitDamage, 32); hook.NotifyObjectCollision(); CombatDescriptor.RequestSound(OracleSoundEngine.SndDamageEnemy); return true; }
        return effect == 0;
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
        if (effect == 0) return new(true, SeedHitResult.None, false);
        Hit(collisionType, hitbox, sourcePosition, -(sbyte)seed.Damage, spawns);
        return new(true, seed.SeedItem == 0x24 ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate, effect == 0x20);
    }
    public override void HandleLinkContact(Player player)
    {
        if (!Entity.CollisionEnabled || Entity.JustHit || !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, player.EnemyContactZ, 7)) return;
        if (player.IsUsingShield && RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, player.ShieldCollisionBounds))
        {
            if (!player.CanAcceptShieldCollision) return;
            int effect = Entity.Data.CollisionEffect(Entity.CollisionMode, player.Inventory.ShieldLevel);
            var (invincibility, recoil) = effect switch { 5 => (8, 11), 6 => (15, 19), 7 => (22, 25),
                _ => throw new NotSupportedException($"ENEMY_EYESOAR shield effect ${effect:x2} is unsupported.") };
            player.ApplyShieldCollisionRecoil(Entity.Position, invincibility, recoil); Entity.MarkContact(player.Inventory.ShieldLevel); return;
        }
        if (player.AcceptsRoomEntityContact && Player.EnemyCollisionOverlaps(player.EnemyContactPosition, Entity.CollisionBounds))
        { player.ApplyEnemyContactDamage(Entity.Position, Entity.Record.DamageQuarters); Entity.MarkContact(0); }
    }
}
