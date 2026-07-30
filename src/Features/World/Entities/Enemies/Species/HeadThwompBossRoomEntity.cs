using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class HeadThwompBossRoomEntity
    : CombatEnemyRoomEntityAdapter<HeadThwompBoss>, IFixedRoomEntity,
        IBombCatchRoomEntity
{
    internal HeadThwompBossRoomEntity(HeadThwompBoss boss)
        : base(
            boss,
            boss.SetTransitionDrawOffset,
            EnemyCombatDescriptor.Special(
                EnemyCombatComponent.WithContactDamage(
                    () => boss.IsDead,
                    () => boss.CollisionBounds,
                    boss.TakeSwordHit,
                    boss.TakeBurnHit,
                    boss.OverlapsLink,
                    () => boss.Position,
                    boss.Record.DamageQuarters,
                    () => null),
                countsAsEnemy: true,
                killableEnemyIndex: 0,
                completedOutcome: () =>
                    RoomEnemyOutcome.BossTeardown(
                        killableEnemyIndex: 0)))
    {
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
        => Entity.UpdateFrame(frame.Player, frame.Counter, spawns);

    public override bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        EnemyKnockbackStrength knockbackStrength,
        ICollection<RoomEntitySpawn> spawns)
    {
        bool hit = base.ApplySwordHit(
            hitbox,
            sourcePosition,
            damage,
            knockbackStrength,
            spawns);
        if (!hit)
            return false;

        // ENEMYCOLLISION_HEAD_THWOMP maps every sword state to
        // COLLISIONEFFECT_$1b. LINKDMG_$1c applies no attacker recoil; the
        // visible and audible response is the midpoint INTERAC_CLINK.
        spawns.Add(new EnemyClinkSpawn(
            CollisionMidpoint(Entity.Position, hitbox.GetCenter())));
        return true;
    }

    public bool TryCatchBomb(BombEffect bomb) =>
        Entity.TryCatchBomb(bomb);
}
