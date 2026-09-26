using System.Collections.Generic;
using System;
using Godot;

namespace oracleofages;

internal sealed class MoldormTailRoomEntity
    : CombatEnemyRoomEntityAdapter<MoldormTailCharacter>, IFixedRoomEntity,
        IScreenTransitionPreloadRoomEntity, IUpdatesDuringDialogueRoomEntity,
        IUpdatesDuringRoomEntityFreeze, INativeEnemySlotRoomEntity, IPostObjectMeleeCollisionRoomEntity
{
    public bool MeleeReportsContact => true;
    public void BindEnemySlot(int slot, Func<int, IRoomEntity?> resolve) => Entity.BindEnemySlot(slot, resolve);
    internal MoldormTailRoomEntity(MoldormTailCharacter tail, EnemyCombatSourceDescriptor source, Action<int> sound)
        : base(tail, tail.SetTransitionDrawOffset, EnemyCombatDescriptor.WithContactDamage(
            source, tail, tail.Record.DamageQuarters, tail.TakeSwordHit, _ => false,
            tail.ApplySwordKnockback, sound, EnemySwordResponse.Knockback,
            deathPuffAllowed: () => false, completedOutcome: RoomEnemyOutcome.RoomCountDecrement)) { }
    public bool UpdatesDuringDialogue => Entity.State == 0;
    public bool UpdatesDuringRoomEntityFreeze => Entity.State == 0;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame();
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Entity.InitializeState();
        return ScreenTransitionPresentation.Visible;
    }
    protected override bool TryApplySwitchHookEffect(int effect, SwitchHookItem hook, Vector2 linkPosition)
    {
        if (effect != CollisionEffect.SwordLowKnockback || !Entity.TakeSwordHit(linkPosition, hook.HitDamage)) return false;
        Entity.ApplySwordKnockback(linkPosition, EnemyKnockbackStrength.Low);
        hook.NotifyObjectCollision();
        CombatDescriptor.RequestSound(SoundId.SndDamageEnemy);
        return true;
    }
}
