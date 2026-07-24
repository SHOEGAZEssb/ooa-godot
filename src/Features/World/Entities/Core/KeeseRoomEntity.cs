using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class KeeseRoomEntity
    : CombatEnemyRoomEntityAdapter<KeeseCharacter>, IFixedRoomEntity
{
    public KeeseRoomEntity(KeeseCharacter keese, int killableEnemyIndex = 0)
        : base(
            keese, keese.SetTransitionDrawOffset, CreateCombat(keese),
            (keese.Record.Flags & 0x02) == 0, killableEnemyIndex)
    { }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player.Position, frame.Counter);

    private static EnemyCombatComponent CreateCombat(KeeseCharacter keese) =>
        EnemyCombatComponent.WithContactDamage(
            () => keese.IsDead,
            () => keese.CollisionBounds,
            (_, damage) => keese.TakeSwordHit(damage),
            keese.TakeSwordHit,
            keese.OverlapsLink,
            () => keese.Position,
            keese.Record.DamageQuarters,
            () => keese.IsDead
                ? new EnemyDeathPuffSpawn(
                    keese.Position + Vector2.Down * keese.SpriteHeight,
                    EnemyId: keese.Record.Id)
                : null,
            keese.ApplySwordKnockback);
}
