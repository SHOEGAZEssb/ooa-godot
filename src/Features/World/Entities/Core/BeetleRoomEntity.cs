using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class BeetleRoomEntity : CombatEnemyRoomEntityAdapter<BeetleCharacter>, IFixedRoomEntity
{
    internal BeetleRoomEntity(BeetleCharacter beetle, EnemyCombatSourceDescriptor source,
        Action<int> soundRequested)
        : base(beetle, beetle.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(source, beetle, beetle.Record.DamageQuarters,
                beetle.TakeSwordHit, beetle.TakeBurnHit,
                (position, strength) => { beetle.OnWeaponHit(); beetle.ApplySwordKnockback(position, strength); },
                soundRequested, EnemySwordResponse.Knockback),
            collisionZ: () => beetle.ZFixed >> 8) { }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player.Position,
            OracleObjectMovement.Shared.RelativeAngle(Vector2.Zero, frame.Player.FacingVector) / 8);
}
