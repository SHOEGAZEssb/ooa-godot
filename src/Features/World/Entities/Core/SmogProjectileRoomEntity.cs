using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class SmogProjectileRoomEntity(SmogProjectilePart projectile, SmogProjectileDatabase data,
    Func<Vector2I> camera, Func<int> roomFlags, Func<int> enemyCount, Action<int> sound)
    : RoomEntityAdapter<SmogProjectilePart>(projectile, projectile.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime, INativePartHealthRoomEntity, IObjectCollisionHeightRoomEntity,
        ISwordHittableRoomEntity, IPostObjectMeleeCollisionRoomEntity, ILinkSwordStateAwareRoomEntity,
        IPostObjectLinkContactRoomEntity, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private int _swordCollision = 4;
    public bool Finished => Entity.Finished;
    public int CollisionZ => 0;
    public bool MeleeReportsContact => Entity.SubId == 0;
    public bool UpdatesDuringDialogue => Entity.State == 0;
    public bool UpdatesDuringRoomEntityFreeze => Entity.State == 0;
    public void ClearHealthAndCollision() => Entity.ClearHealthAndCollision();
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player.EnemyContactPosition, camera(), roomFlags(), enemyCount());
    public void SetLinkSwordState(SwordActionState state, int level) =>
        _swordCollision = state == SwordActionState.Spin ? (level >= 2 ? 8 : 7)
            : state is SwordActionState.Held or SwordActionState.Charged ? 9 : Math.Clamp(level,1,3) + 3;
    private bool Eligible(int collision, Rect2 bounds) => Entity.CollisionEnabled && !Entity.PendingCollision &&
        Entity.InvincibilityCounter == 0 && data.Enabled(collision) &&
        RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, bounds);
    public bool ApplySwordHit(Rect2 hitbox, Vector2 sourcePosition, int damage,
        EnemyKnockbackStrength strength, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Eligible(_swordCollision, hitbox)) return false;
        int effect = data.Effect(Entity.SubId, _swordCollision);
        if (effect == 0) return true;
        if (effect != 0x1f) throw new NotSupportedException($"PART$4a sword collision ${_swordCollision:x2}: effect ${effect:x2} is not represented.");
        Entity.PublishCollision(0x80 | _swordCollision);
        Entity.InvincibilityCounter = -28; // ENEMYDMG_34; LINKDMG_20 has sound, no recoil.
        sound(OracleSoundEngine.SndClink2);
        return true;
    }
    public void HandleLinkContact(Player player)
    {
        // checkEnemyAndPartCollisions skips the entire PART scan while its
        // invincibility counter is nonzero, before calling partCheckCollisions.
        if (!player.AcceptsRoomEntityContact || !player.EnemyContactHeightOverlaps(0) ||
            !Eligible(0, new(player.EnemyContactPosition - new Vector2(6,6), new(12,12)))) return;
        int effect = data.Effect(Entity.SubId, 0);
        if (effect != 2) throw new NotSupportedException($"PART$4a Link collision: effect ${effect:x2} is not represented.");
        if (player.ApplyEnemyContactDamage(Entity.Position, (0x100 - data.RawDamage) / 2,
            RingDamageSource.Generic, 34, 15)) Entity.PublishCollision(0x80);
    }
}

internal sealed record SmogProjectileSpawn(Vector2 Position, int SubId) : RoomEntitySpawn;
