using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class ColorChangingGelRoomEntity
    : CombatEnemyRoomEntityAdapter<ColorChangingGelCharacter>, IFixedRoomEntity
{
    internal ColorChangingGelRoomEntity(
        ColorChangingGelCharacter gel,
        EnemyCombatSourceDescriptor combatSource,
        Action<int> soundRequested)
        : base(
            gel,
            gel.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                gel,
                gel.Record.DamageQuarters,
                gel.TakeSwordHit,
                gel.TakeBurnHit,
                gel.ApplySwordNoKnockback,
                soundRequested,
                EnemySwordResponse.NoKnockback),
            collisionZ: () => gel.ZHigh)
    { }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame();
}
