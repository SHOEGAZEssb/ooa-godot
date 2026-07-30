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

    public bool TryCatchBomb(BombEffect bomb) =>
        Entity.TryCatchBomb(bomb);
}
