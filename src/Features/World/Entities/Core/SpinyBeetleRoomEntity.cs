using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class SpinyBeetleRoomEntity
    : CombatEnemyRoomEntityAdapter<SpinyBeetleCharacter>,
        IFixedRoomEntity, IBraceletInteractableRoomEntity
{
    private readonly Action<int> _soundRequested;

    public SpinyBeetleRoomEntity(
        SpinyBeetleCharacter beetle,
        EnemyCombatSourceDescriptor combatSource,
        Action<int> soundRequested)
        : base(
            beetle,
            beetle.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                beetle,
                beetle.Record.DamageQuarters,
                beetle.TakeSwordHit,
                beetle.TakeBurnHit,
                beetle.ApplyAcceptedSwordResponse,
                soundRequested,
                EnemySwordResponse.Knockback,
                acceptedHitSound: 0))
    {
        _soundRequested = soundRequested;
    }

    public override bool FreezesDuringSeedBurn => false;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player, spawns);

    public override void HandleLinkContact(Player player)
    {
        Entity.RegisterLinkContact(player.Position);
        base.HandleLinkContact(player);
    }

    public override bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        EnemyKnockbackStrength knockbackStrength,
        ICollection<RoomEntitySpawn> spawns)
    {
        bool struckCover = Entity.CoverProtects;
        bool hit = base.ApplySwordHit(
            hitbox,
            sourcePosition,
            damage,
            knockbackStrength,
            spawns);
        if (hit && struckCover && SeedBurning)
            CancelSeedBurn();
        if (hit && !struckCover)
            _soundRequested(OracleSoundEngine.SndDamageEnemy);
        SpawnCoverDebris(spawns);
        return hit;
    }

    public override void CompleteSeedBurn(
        ICollection<RoomEntitySpawn> spawns)
    {
        base.CompleteSeedBurn(spawns);
        SpawnCoverDebris(spawns);
    }

    public bool TryUseBracelet(Player player)
    {
        bool used = Entity.TryUseBracelet(player);
        if (used && Entity.CoverHeld && SeedBurning)
            CancelSeedBurn();
        return used;
    }

    private void SpawnCoverDebris(ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.TakeCoverDebris(out Vector2 position))
            spawns.Add(new GrassDebrisSpawn(position));
    }
}
