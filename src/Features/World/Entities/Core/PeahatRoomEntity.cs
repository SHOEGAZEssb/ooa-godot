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
        if (effect is not (CollisionEffect.SwordNoKnockback or CollisionEffect.Effect1c) || !Entity.TakeSwitchHookHit(linkPosition, hook.HitDamage)) return false;
        hook.NotifyObjectCollision();
        if (effect == CollisionEffect.SwordNoKnockback) CombatDescriptor.RequestSound(SoundId.SndDamageEnemy);
        return true;
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Counter);
}
