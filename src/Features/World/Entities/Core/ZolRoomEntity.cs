using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class ZolRoomEntity
    : CombatEnemyRoomEntityAdapter<ZolCharacter>, IFixedRoomEntity,
        IScreenTransitionPreloadRoomEntity, IPostObjectMeleeCollisionRoomEntity, ISomariaBlockCollisionRoomEntity, IBoomerangCollisionRoomEntity
{
    protected override bool Stunned => Entity.StunCounter != 0;
    protected override bool BoomerangHitPending => Entity.DamageHitPending || base.BoomerangHitPending;
    public bool MeleeReportsContact => true;
    public ZolRoomEntity(
        ZolCharacter zol,
        EnemyCombatSourceDescriptor combatSource,
        Action<int> soundRequested)
        : base(
            zol,
            zol.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                zol,
                zol.Record.DamageQuarters,
                zol.TakeSwordHit,
                zol.TakeBurnHit,
                zol.ApplySwordNoKnockback,
                soundRequested,
                EnemySwordResponse.NoKnockback,
                completedOutcome: () => zol.DiedInHazard
                    ? RoomEnemyOutcome.HazardDeletion(
                        combatSource.CountsAsEnemy)
                    : zol.State == ZolState.RedSplitDelay
                        ? RoomEnemyOutcome.ReplacementDeletion(
                            combatSource.CountsAsEnemy)
                        : RoomEnemyOutcome.EnemyDie(
                            combatSource.KillableEnemyIndex)),
            collisionZ: () => zol.ZFixed >> 8)
    { }

    public bool ApplySomariaBlockCollision(SomariaBlock block, ICollection<RoomEntitySpawn> spawns) =>
        ApplySomariaBlockCollision(block, Entity.Record.RawDamage, Entity.DamageHitPending, spawns, deferNativeStatus: false);

    protected override void ApplySomariaEnemyDamage(SomariaBlock block, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.TakeSomariaHit(block.Position, -block.Damage))
            throw new InvalidOperationException("ENEMY$34 rejected an eligible Somaria effect$2f collision.");
        CombatDescriptor.RequestSound(SoundId.SndDamageEnemy);
    }

    protected override bool TryApplySwitchHookEffect(int effect, SwitchHookItem hook, Vector2 linkPosition)
    {
        if (effect != CollisionEffect.SwordNoKnockback || !Entity.TakeSwitchHookHit(linkPosition, hook.HitDamage)) return false;
        hook.NotifyObjectCollision();
        CombatDescriptor.RequestSound(SoundId.SndDamageEnemy);
        return true;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        switch (Entity.UpdateFrame(frame.Player.Position, frame.Counter))
        {
            case UpdateEvent.BeginSplit:
                spawns.Add(new KillEnemyPuffSpawn(Entity.Position));
                break;
            case UpdateEvent.SpawnGels:
                spawns.Add(new GelSpawn(
                    Entity.Position + Vector2.Right * 4.0f,
                    "SplitGelRight", KillableEnemyIndex));
                spawns.Add(new GelSpawn(
                    Entity.Position + Vector2.Left * 4.0f,
                    "SplitGelLeft", KillableEnemyIndex));
                break;
        }
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(
        ICollection<RoomEntitySpawn> spawns)
    {
        Entity.InitializeState();
        return Entity.Visible
            ? ScreenTransitionPresentation.Visible
            : ScreenTransitionPresentation.Hidden;
    }
}

internal sealed record KillEnemyPuffSpawn(Vector2 Position) : RoomEntitySpawn;

internal sealed record GelSpawn(
    Vector2 Position,
    string Name = "Gel",
    int KillableEnemyIndex = 0)
    : RoomEntitySpawn;
