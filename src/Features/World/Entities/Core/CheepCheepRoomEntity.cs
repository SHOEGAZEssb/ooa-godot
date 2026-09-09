using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class CheepCheepRoomEntity
    : CombatEnemyRoomEntityAdapter<CheepCheepCharacter>, IFixedRoomEntity,
        IScreenTransitionPreloadRoomEntity
{
    internal CheepCheepRoomEntity(CheepCheepCharacter enemy,
        EnemyCombatSourceDescriptor source, Action<int> soundRequested)
        : base(enemy, enemy.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(source, enemy,
                enemy.Record.DamageQuarters, enemy.TakeSwordHit, enemy.TakeBurnHit,
                enemy.ApplySwordKnockback, soundRequested, EnemySwordResponse.Knockback))
    { }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame();

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Entity.PrepareForScreenTransition();
        return ScreenTransitionPresentation.Visible;
    }
}
