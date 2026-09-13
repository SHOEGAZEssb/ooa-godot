using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class PeahatRoomEntity
    : CombatEnemyRoomEntityAdapter<PeahatCharacter>, IFixedRoomEntity
{
    internal PeahatRoomEntity(
        PeahatCharacter peahat,
        EnemyCombatSourceDescriptor combatSource,
        Action<int> soundRequested)
        : base(
            peahat,
            peahat.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                peahat,
                peahat.Record.DamageQuarters,
                peahat.TakeSwordHit,
                peahat.TakeBurnHit,
                peahat.ApplySwordNoKnockback,
                soundRequested,
                EnemySwordResponse.NoKnockback),
            collisionZ: () => peahat.ZHigh)
    { }

    public override int DimitriCollisionMode => Entity.CollisionMode;

    protected override bool TryApplySwitchHookEffect(int effect, SwitchHookItem hook, Vector2 linkPosition)
    {
        if (effect is not (0x0b or 0x1c) || !Entity.TakeSwitchHookHit(linkPosition, hook.HitDamage)) return false;
        hook.NotifyObjectCollision();
        if (effect == 0x0b) CombatDescriptor.RequestSound(OracleSoundEngine.SndDamageEnemy);
        return true;
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Counter);
}
