using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class BladeTrapRoomEntity : CombatEnemyRoomEntityAdapter<BladeTrapCharacter>,
    IFixedRoomEntity, ISwordAttackerKnockbackRoomEntity,
    IItemCollisionHittableRoomEntity, IExpertPunchHittableRoomEntity,
    IScreenTransitionPreloadRoomEntity, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly Action<int> _sound;
    internal BladeTrapRoomEntity(BladeTrapCharacter trap, EnemyCombatSourceDescriptor source,
        Action<int> sound) : base(trap, trap.SetTransitionDrawOffset,
        EnemyCombatDescriptor.WithContactDamage(source, trap, trap.Record.DamageQuarters,
            trap.TakeSwordHit, trap.TakeBurnHit, (_, _) => { }, sound,
            EnemySwordResponse.Armored, acceptedHitSound: 0)) => _sound = sound;

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player.Position);

    public bool UpdatesDuringDialogue => Entity.State == BladeTrapState.Uninitialized;
    public bool UpdatesDuringRoomEntityFreeze => Entity.State == BladeTrapState.Uninitialized;

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Entity.PrepareForScreenTransition();
        return ScreenTransitionPresentation.Visible;
    }

    protected override bool TryApplySwitchHookEffect(int effect, SwitchHookItem hook, Vector2 linkPosition)
    {
        if (effect != CollisionEffect.Effect1b || !Entity.TakeSwitchHookHit()) return false;
        // LINKDMG_$1c ORs the blue trap's var3e=$08 into item.var2a;
        // ENEMYDMG_$28 gives invincibility without health loss or recoil.
        hook.NotifyObjectCollision(CollisionMidpoint(Entity.Position, hook.Position));
        return true;
    }

    public override void HandleLinkContact(Player player)
    {
        if (!Entity.CollisionEnabled || !player.EnemyContactHeightOverlaps(0)) return;
        // Collision row $13 uses effect $06 for every shield level. The
        // enemy receives no damage/knockback; Link receives LINKDMG_$14.
        if (player.IsUsingShield && CombatDescriptor.Combat.Intersects(player.ShieldCollisionBounds))
        {
            if (player.CanAcceptShieldCollision)
            {
                player.ApplyShieldCollisionRecoil(Entity.Position, 0x0f, 0x13);
                _sound(SoundId.SndBombLand);
            }
            return;
        }
        if (Entity.OverlapsLink(player.EnemyContactPosition))
            player.ApplyEnemyContactDamage(Entity.Position, Entity.Record.DamageQuarters,
                RingDamageSource.BladeTrap);
    }

    public override bool ApplySwordHit(Rect2 hitbox, Vector2 sourcePosition, int damage,
        EnemyKnockbackStrength strength, ICollection<RoomEntitySpawn> spawns)
    {
        if (!base.ApplySwordHit(hitbox, sourcePosition, damage, strength, spawns)) return false;
        spawns.Add(new EnemyClinkSpawn(CollisionMidpoint(Entity.Position, hitbox.GetCenter())));
        _sound(SoundId.SndBombLand);
        return true;
    }

    public bool TryGetSwordAttackerKnockback(EnemyKnockbackStrength strength,
        out SwordAttackerKnockback response)
    {
        var profile = EnemyBehaviorTables.Shared.ArmoredSwordAttackerKnockback;
        int frames = strength switch
        {
            EnemyKnockbackStrength.Low => profile.LowFrames,
            EnemyKnockbackStrength.Normal => profile.NormalFrames,
            EnemyKnockbackStrength.High => profile.HighFrames,
            _ => 0
        };
        response = new SwordAttackerKnockback(Entity.Position, frames);
        return frames != 0;
    }

    public override SeedHitResult ApplySeedHit(Rect2 hitbox, Vector2 sourcePosition,
        int seedItem, ICollection<RoomEntitySpawn> spawns) =>
        seedItem is >= 0x20 and <= 0x24 && Entity.CollisionEnabled &&
        Entity.InvincibilityCounter == 0 && CombatDescriptor.Combat.Intersects(hitbox)
            ? SeedHitResult.Activate : SeedHitResult.None;

    public bool ApplyExpertPunch(Rect2 hitbox, Vector2 sourcePosition, int damage,
        ICollection<RoomEntitySpawn> spawns) => false;

    public bool ApplyItemCollision(RoomEntityItemCollision collision, Rect2 hitbox,
        Vector2 sourcePosition, int damage, ICollection<RoomEntitySpawn> spawns)
    {
        int effect = EnemyBehaviorTables.Shared.BladeTrapCollisionEffects[(int)collision].Value;
        if (!Entity.CollisionEnabled || Entity.InvincibilityCounter != 0 ||
            !CombatDescriptor.Combat.Intersects(hitbox)) return false;
        return effect switch
        {
            CollisionEffect.None => false,
            CollisionEffect.Effect1c or CollisionEffect.Effect20 => true,
            _ => throw new InvalidOperationException(
                $"Blade trap $0e:$01 item collision ${(int)collision:x2} has unsupported effect ${effect:x2}.")
        };
    }
}
