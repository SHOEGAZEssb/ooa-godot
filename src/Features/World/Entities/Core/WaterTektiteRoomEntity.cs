using System;
using System.Collections.Generic;
using Godot;

namespace oracleofages;

internal sealed class WaterTektiteRoomEntity : CombatEnemyRoomEntityAdapter<WaterTektiteCharacter>,
    IFixedRoomEntity, IScreenTransitionPreloadRoomEntity, IBoomerangCollisionRoomEntity,
    ISomariaBlockCollisionRoomEntity
{
    protected override bool Stunned => Entity.StunCounter != 0;
    private readonly Func<Vector2?> _scentTarget;
    internal WaterTektiteRoomEntity(WaterTektiteCharacter enemy,
        EnemyCombatSourceDescriptor source, Action<int> soundRequested, Func<Vector2?> scentTarget)
        : base(enemy, enemy.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(source, enemy, enemy.Record.DamageQuarters,
                enemy.TakeSwordHit, enemy.TakeBurnHit, enemy.ApplySwordKnockback,
                soundRequested, EnemySwordResponse.Knockback)) { _scentTarget = scentTarget; }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.ScentSeedTarget, frame.Counter);

    public bool ApplySomariaBlockCollision(SomariaBlock block, ICollection<RoomEntitySpawn> spawns) =>
        ApplySomariaBlockCollision(block, Entity.Record.RawDamage, Entity.NativeHitPending, spawns);

    protected override bool TryApplySwitchHookEffect(int effect, SwitchHookItem hook, Vector2 linkPosition)
    {
        if (effect != CollisionEffect.SwordLowKnockback || !Entity.TakeSwitchHookHit(linkPosition, hook.HitDamage)) return false;
        // Mode $14 uses effect08/ENEMYDMG_00, rather than a hook exchange.
        // var3e=$01 signals the item while collision remains enabled.
        hook.NotifyObjectCollision();
        CombatDescriptor.RequestSound(SoundId.SndDamageEnemy);
        return true;
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.InitializationPending) Entity.UpdateFrame(_scentTarget());
        return ScreenTransitionPresentation.Visible;
    }
}
