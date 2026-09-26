using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class ArmosWarriorRoomEntity : CombatEnemyRoomEntityAdapter<ArmosWarriorActor>,
    IFixedRoomEntity, IPlayerRestriction, IPlayerForcedMovement, IScreenTransitionPreloadRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    ILinkSwordStateAwareRoomEntity, ISwordAttackerKnockbackRoomEntity, IItemCollisionHittableRoomEntity,
    IPostObjectMeleeCollisionRoomEntity, IExpertPunchHittableRoomEntity, ISeedCollisionTarget
{
    private readonly BossEntryMovement _entry;
    private SwordActionState _swordState;
    private int _swordLevel = 1;
    private int _attackerRecoil;
    public bool MeleeReportsContact { get; private set; }
    internal ArmosWarriorRoomEntity(ArmosWarriorActor actor, BossEntryMovement entry)
        : base(actor, actor.SetTransitionDrawOffset,
            EnemyCombatDescriptor.Special(EnemyCombatComponent.WithContactDamage(
                () => actor.IsDead, () => actor.CollisionBounds, (_, _) => false, _ => false,
                actor.OverlapsLink, () => actor.Position, actor.Record.DamageQuarters, () => null),
                countsAsEnemy: actor.SubId is 0 or 1, killableEnemyIndex: 0,
                completedOutcome: () => actor.SubId == 1 ? RoomEnemyOutcome.BossTeardown(0) : RoomEnemyOutcome.SilentDeletion(false),
                soundRequested: actor.RequestSound),
            collisionZ: () => actor.ZFixed >> 8) { _entry = entry; }

    public override int DimitriCollisionType => 0x73;
    public override int DimitriCollisionMode => Entity.CollisionMode;
    public bool UpdatesDuringDialogue => Entity.Initializing;
    public bool UpdatesDuringRoomEntityFreeze => Entity.Initializing;
    public bool DisablesSword => Entity.ControlsDisabled;
    public bool DisablesItems => DisablesSword;
    public bool DisablesMovement => DisablesSword;
    public bool DisablesMenus => DisablesSword;
    public void UpdatePlayerForcedMovement(Player player)
    { if (Entity.SubId is 0 or 1 && !Finished) _entry.Update(player); }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        bool starts = Entity.Initializing && Entity.SubId == 0;
        Entity.UpdateFrame(frame.Player, frame.Counter, spawns);
        if (starts) _entry.Arm();
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.Initializing)
        {
            Entity.InitializeState();
            if (Entity.SubId == 0) _entry.Arm();
        }
        return ScreenTransitionPresentation.Hidden;
    }
    public void SetLinkSwordState(SwordActionState state, int swordLevel)
    { _swordState = state; _swordLevel = swordLevel; }
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
        Entity.Data.CollisionEnabled(item) && RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, hitbox);
    private bool Hit(int item, Rect2 hitbox, Vector2 source, int damage, ICollection<RoomEntitySpawn> spawns, bool melee = false)
    {
        if (!Eligible(item, hitbox)) return false;
        int effect = Entity.Data.CollisionEffect(Entity.CollisionMode, item);
        _attackerRecoil = 0;
        MeleeReportsContact = effect != 0;
        switch (effect)
        {
            case 0: return melee;
            case 0x21:
                Entity.ApplyNativeHit(damage, 32); CombatDescriptor.RequestSound(OracleSoundEngine.SndBossDamage); return true;
            case 0x15:
            case 0x16:
            case 0x17:
                var profile = EnemyBehaviorTables.Shared.ArmoredSwordAttackerKnockback;
                _attackerRecoil = effect == 0x15 ? profile.LowFrames : effect == 0x16 ? profile.NormalFrames : profile.HighFrames;
                Entity.ApplyNativeHit(0, -28);
                spawns.Add(new EnemyClinkSpawn(CollisionMidpoint(Entity.Position, source))); return true;
            case 0x1b:
                Entity.ApplyNativeHit(0, -20);
                spawns.Add(new EnemyClinkSpawn(CollisionMidpoint(Entity.Position, source))); return true;
            case 0x1c:
            case 0x20: Entity.MarkContact(); return true;
            default:
                throw new System.NotSupportedException($"ENEMY_ARMOS_WARRIOR $73:${Entity.SubId:x2}: item ${item:x2}, collision effect ${effect:x2} is unsupported.");
        }
    }
    public bool TryGetSwordAttackerKnockback(EnemyKnockbackStrength strength, out SwordAttackerKnockback response)
    {
        response = new(Entity.Position, _attackerRecoil);
        bool pending = _attackerRecoil != 0; _attackerRecoil = 0; return pending;
    }
    protected override bool TryApplySwitchHookEffect(int effect, SwitchHookItem hook, Vector2 linkPosition)
    {
        if (effect == 0x1b)
        { Entity.ApplyNativeHit(0, -20); hook.NotifyObjectCollision(CollisionMidpoint(Entity.Position, hook.Position)); return true; }
        if (effect == 0x21)
        {
            Entity.ApplyNativeHit(hook.HitDamage, 32); hook.NotifyObjectCollision();
            CombatDescriptor.RequestSound(OracleSoundEngine.SndBossDamage); return true;
        }
        return false;
    }
    public override bool ApplySwitchHookHit(SwitchHookItem hook, Vector2 linkPosition)
    {
        if (!RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, 0, 7) ||
            !Eligible(0x0d, hook.CollisionBounds)) return false;
        int effect = Entity.Data.CollisionEffect(Entity.CollisionMode, 0x0d);
        return effect == 0 || TryApplySwitchHookEffect(effect, hook, linkPosition);
    }
    public override SeedHitResult ApplySeedHit(Rect2 hitbox, Vector2 sourcePosition, int seedItem,
        ICollection<RoomEntitySpawn> spawns)
        => throw new System.InvalidOperationException("ENEMY_ARMOS_WARRIOR $73 seed collision requires the projectile's imported attributes and live collision type.");
    public SeedCollisionResponse ApplySeedCollision(Rect2 hitbox, Vector2 sourcePosition, SeedRecord seed,
        int collisionType, ICollection<RoomEntitySpawn> spawns)
    {
        // All three native actors leave var3f bit5 clear, so Mystery uses
        // its selected collision type before reloading the chosen effect.
        if (!Eligible(collisionType, hitbox)) return default;
        int effect = Entity.Data.CollisionEffect(Entity.CollisionMode, collisionType);
        if (effect == 0) return new(true, SeedHitResult.None, false);
        Hit(collisionType, hitbox, sourcePosition, -(sbyte)seed.Damage, spawns);
        return new(true, seed.SeedItem == 0x24 ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate, effect == 0x20);
    }
    public override void HandleLinkContact(Player player)
    {
        if (!Entity.CollisionEnabled || Entity.JustHit ||
            !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, player.EnemyContactZ, 7)) return;
        if (player.IsUsingShield && RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, player.ShieldCollisionBounds))
        {
            if (!player.CanAcceptShieldCollision) return;
            int effect = Entity.Data.CollisionEffect(Entity.CollisionMode, player.Inventory.ShieldLevel);
            var (invincibility, recoil) = effect switch
            {
                5 => (8, 11), // collisionEffect05: LINK10.
                6 => (15, 19), // collisionEffect06: LINK14.
                7 => (22, 25), // collisionEffect07: LINK18.
                _ => throw new System.NotSupportedException($"ENEMY_ARMOS_WARRIOR $73:${Entity.SubId:x2}: shield collision effect ${effect:x2} is unsupported.")
            };
            player.ApplyShieldCollisionRecoil(Entity.Position, invincibility, recoil);
            Entity.MarkContact(); // ENEMYDMG_1c writes var2a, preserving invincibility.
            return;
        }
        if (player.AcceptsRoomEntityContact && Player.EnemyCollisionOverlaps(player.EnemyContactPosition, Entity.CollisionBounds))
        {
            player.ApplyEnemyContactDamage(Entity.Position, Entity.Record.DamageQuarters);
            Entity.MarkContact();
        }
    }
}
