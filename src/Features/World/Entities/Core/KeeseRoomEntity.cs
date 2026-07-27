using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class KeeseRoomEntity
    : CombatEnemyRoomEntityAdapter<KeeseCharacter>, IFixedRoomEntity
{
    public KeeseRoomEntity(
        KeeseCharacter keese,
        EnemyCombatSourceDescriptor combatSource)
        : base(
            keese,
            keese.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                keese,
                keese.Record.DamageQuarters,
                (_, damage) => keese.TakeSwordHit(damage),
                keese.TakeSwordHit,
                keese.ApplySwordKnockback,
                EnemySwordResponse.Knockback,
                deathPuffPosition: () =>
                    keese.Position + Vector2.Down * keese.SpriteHeight))
    { }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player.Position, frame.Counter);
}
