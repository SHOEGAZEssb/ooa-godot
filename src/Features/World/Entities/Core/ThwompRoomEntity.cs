using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class ThwompRoomEntity
    : CombatEnemyRoomEntityAdapter<ThwompCharacter>,
        IFixedRoomEntity, IPlayerRideableRoomEntity,
        ISwordAttackerKnockbackRoomEntity,
        IScreenTransitionPreloadRoomEntity
{
    private readonly Action<int> _soundRequested;
    private bool _linkRiding;

    bool IPlayerRideableRoomEntity.LinkRiding => _linkRiding;

    internal ThwompRoomEntity(
        ThwompCharacter thwomp,
        EnemyCombatSourceDescriptor combatSource,
        Action<int> soundRequested)
        : base(
            thwomp,
            thwomp.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                thwomp,
                thwomp.Record.DamageQuarters,
                thwomp.TakeSwordHit,
                thwomp.TakeBurnHit,
                (_, _) => { },
                soundRequested,
                EnemySwordResponse.Armored,
                acceptedHitSound: 0))
    {
        _soundRequested = soundRequested;
    }

    public override bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        EnemyKnockbackStrength knockbackStrength,
        ICollection<RoomEntitySpawn> spawns)
    {
        bool hit = base.ApplySwordHit(
            hitbox,
            sourcePosition,
            damage,
            knockbackStrength,
            spawns);
        if (!hit)
            return false;

        // ENEMYCOLLISION_THWOMP maps ordinary sword states to
        // COLLISIONEFFECT_$15-$17: allocate INTERAC_CLINK at the
        // enemy/item midpoint and let LINKDMG_$10/$14/$18 request the
        // secondary SND_BOMB_LAND.
        spawns.Add(new EnemyClinkSpawn(
            CollisionMidpoint(Entity.Position, hitbox.GetCenter())));
        _soundRequested(OracleSoundEngine.SndBombLand);
        return true;
    }

    public bool TryGetSwordAttackerKnockback(
        EnemyKnockbackStrength strength,
        out SwordAttackerKnockback response)
    {
        int frames = Entity.ArmoredAttackerKnockbackFrames(strength);
        response = new SwordAttackerKnockback(Entity.Position, frames);
        return frames != 0;
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        Entity.UpdateFrame(frame.Player.Position);
        _linkRiding = Entity.IsLinkRiding(
            frame.Player, out float targetY);
        if (_linkRiding)
        {
            frame.Player.SetMovingPlatformCoordinateHigh(
                horizontal: false,
                Mathf.FloorToInt(targetY));
        }
    }

    public void PrepareForScreenTransition(
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.PrepareForScreenTransition();
}
