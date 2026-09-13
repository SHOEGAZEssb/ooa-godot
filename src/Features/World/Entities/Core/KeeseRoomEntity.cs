using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class KeeseRoomEntity
    : CombatEnemyRoomEntityAdapter<KeeseCharacter>, IFixedRoomEntity
{
    public KeeseRoomEntity(
        KeeseCharacter keese,
        EnemyCombatSourceDescriptor combatSource,
        Action<int> soundRequested)
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
                soundRequested,
                EnemySwordResponse.Knockback,
                deathPuffPosition: () =>
                    keese.Position + Vector2.Down * keese.SpriteHeight))
    { }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player.Position, frame.Counter);

    protected override bool TryApplySwitchHookEffect(int effect, SwitchHookItem hook, Vector2 linkPosition)
    {
        if (effect != 0x08 || !Entity.TakeSwitchHookHit(linkPosition, hook.HitDamage)) return false;
        hook.NotifyObjectCollision();
        CombatDescriptor.RequestSound(OracleSoundEngine.SndDamageEnemy);
        return true;
    }
}
