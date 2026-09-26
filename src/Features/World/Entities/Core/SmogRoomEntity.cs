using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// Native room scheduling and allocation use the shared owners; collision
// interfaces preserve the post-object item-before-Link scan.
internal sealed class SmogRoomEntity(SmogCharacter actor, SmogCollisionDatabase data, Action<int> sound, SmogRoomEnvironment? world = null)
    : RoomEntityAdapter<SmogCharacter>(actor, actor.SetTransitionDrawOffset),
        ISwordHittableRoomEntity, ILinkSwordStateAwareRoomEntity, IPostObjectMeleeCollisionRoomEntity,
        IPostObjectLinkContactRoomEntity, IObjectCollisionHeightRoomEntity,
        IFixedRoomEntity, IRoomEntityLifetime, IRoomEnemyCounterEntity, IRoomEnemyOutcomeSource,
        INativeEnemySlotRoomEntity, INativeEnemyCounter1RoomEntity,
        IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
        IScreenTransitionPreloadRoomEntity, IPlayerForcedMovement
{
    private int _slot = -1;
    private bool _decrement, _marked, _outcomeTaken;
    private bool _sentinelCountReleased;
    public bool Finished => Entity.IsDead;
    public bool CountsAsEnemy => !Finished && !_sentinelCountReleased;
    internal bool IsRoomSentinel => !Finished && Entity.SubId == 5;
    internal bool OwnsBossRoomInitialization => IsRoomSentinel && world?.Entry is not null;
    internal int InitializeBossRoom(bool scrolling) => world?.InitializeBossRoom?.Invoke(scrolling) ??
        throw new NotSupportedException("Smog room initialization requires the active room's boss-entry owner.");
    internal void ReleaseSentinelCount()
    {
        if (!IsRoomSentinel || _sentinelCountReleased)
            throw new InvalidOperationException("INTERAC$33 final decrement requires one counted ENEMY$7c:$05 sentinel.");
        // decNumEnemies does not enemyDelete: retain the native slot and
        // inert subid5 actor after the controller deletes itself.
        _sentinelCountReleased = true;
    }
    public bool UpdatesDuringDialogue => Entity.State == 0;
    public bool UpdatesDuringRoomEntityFreeze => Entity.State == 0;
    public int Counter1 { get => Entity.Counter1; set => Entity.Counter1 = value; }
    public bool RetainsCounter1AfterDeletion => false;
    public void UpdatePlayerForcedMovement(Player player)
    {
        if (!Finished) world?.Entry?.Update(player);
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.State == 0 && Entity.SubId is 5 or 6)
        {
            var environment = world ?? throw new InvalidOperationException("Smog preload requires its room environment.");
            Entity.UpdateNativeFrame(false,environment.FrameCounter?.Invoke() ?? 0,environment.NextRandom,
                () => Entity.UpdateRoomSentinel(() => InitializeBossRoom(true),
                    position => environment.CreatePuff(position),() => _decrement = true),
                () => throw new InvalidOperationException("Smog state0 preload cannot dispatch boss death."));
        }
        return Entity.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }
    public void BindEnemySlot(int slot, Func<int,IRoomEntity?> resolve) => _slot = slot;
    public bool TryTakeEnemyOutcome(out RoomEnemyOutcome outcome)
    {
        outcome = default;
        if (_outcomeTaken || (!_decrement && !_marked)) return false;
        _outcomeTaken = true;
        outcome = _marked ? RoomEnemyOutcome.BossTeardown(0) : RoomEnemyOutcome.RoomCountDecrement();
        return true;
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        var environment = world ?? throw new InvalidOperationException("Smog native room update requires its environment.");
        void Puff(Vector2 position)
        {
            environment.CreatePuff(position);
        }
        void Projectile(Vector2 position, int subid)
        {
            // getFreePartSlot fails with HL=$e0c0. Smog still copies XYZ
            // (and increments subid for large shots), targeting main-stack
            // bytes through echo RAM rather than a seventeenth PART slot.
            if (!environment.PartSlotAvailable())
            {
                (environment.WriteFailedProjectile ?? throw new InvalidOperationException(
                    "Smog full-PART fallthrough requires the runtime WRAM owner."))(position, subid);
                return;
            }
            spawns.Add(new SmogProjectileSpawn(position,subid));
        }
        void Handler()
        {
            // enemyCode7c dispatches states $01..$07 to smog_state_stub
            // before examining subid. Shared status/death handling remains
            // in UpdateNativeFrame, outside this native state dispatch.
            if (Entity.State is >= 1 and <= 7) return;
            if (Entity.State > 8)
                throw new NotSupportedException($"smog.s state${Entity.State:x2} is outside the native jump table.");
            switch (Entity.SubId & 15)
            {
                case 0: case 1:
                    Entity.UpdateIntro(environment.TextActive(), id => environment.ShowText(id,Entity.Position),
                        (position,subid) => environment.SpawnEnemy(new(position,subid)),Puff,() => _decrement = true,environment.NextRandom);
                    break;
                case 2:
                    Entity.UpdateSmallCloud(environment.RoomFlags(),environment.BeginBoss,position => Projectile(position,0),
                        Puff,() => _decrement = true,environment.NextRandom);
                    break;
                case 3:
                    if (Entity.State == 0) Entity.UpdateMergedInitialization(environment.EnemyCount(),
                        value => environment.WriteInteractionCounter(_slot,value),environment.SetTile,environment.NextRandom);
                    else Entity.UpdateMediumCloud(environment.RoomFlags(),position => Projectile(position,0),Puff,() => _decrement = true,environment.NextRandom);
                    break;
                case 4: Entity.UpdateLargeCloud(frame.Player.EnemyContactPosition,position => Projectile(position,1),environment.NextRandom); break;
                case 5: case 6:
                    Entity.UpdateRoomSentinel(
                        () => InitializeBossRoom(false),
                        Puff,() => _decrement = true);
                    break;
                default: throw new NotSupportedException($"Smog native subid${Entity.SubId:x2} is not represented.");
            }
        }
        void Death() => Entity.UpdateBossDeath(position =>
            {
                if (!environment.PartSlotAvailable()) return false;
                spawns.Add(new BossDeathExplosionSpawn(position,EnemyId.Smog)); return true;
            },environment.DisableLinkCollisionsAndMenu,() => _marked = true,environment.RestoreRoomMusic,sound);
        Entity.UpdateNativeFrame(environment.Frozen(),frame.Counter,environment.NextRandom,Handler,Death);
    }
    private int _swordCollision = ItemCollisionType.L1Sword;
    public int CollisionZ => 0;
    public bool MeleeReportsContact => true;
    public void SetLinkSwordState(SwordActionState state, int level) =>
        _swordCollision = SwordCollision.Type(state, level);
    private bool Overlaps(int collision, Rect2 bounds) => Entity.CollisionEnabled && (Entity.ContactFlags & ObjectCollisionFlags.JustHit) == 0 &&
        data.Enabled(collision) && RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, bounds);
    public bool ApplySwordHit(Rect2 hitbox, Vector2 sourcePosition, int damage,
        EnemyKnockbackStrength strength, ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.InvincibilityCounter != 0 || !Overlaps(_swordCollision, hitbox)) return false;
        int effect = data.Effect(Entity.CollisionMode,_swordCollision);
        if (effect == CollisionEffect.Effect1f)
        {
            Entity.PublishCollision(ObjectCollisionFlags.JustHit | _swordCollision);
            Entity.InvincibilityCounter = -28;
            sound(SoundId.SndClink2);
        }
        else if (effect == CollisionEffect.Effect21)
        {
            Entity.ApplyLargeSwordCollision(damage,_swordCollision,sourcePosition);
            sound(SoundId.SndBossDamage);
        }
        else throw new NotSupportedException($"ENEMY_SMOG $7c sword effect${effect:x2} is not represented.");
        return true;
    }
    public void HandleLinkContact(Player player)
    {
        // enemyCheckCollisions skips items during enemy invincibility but still
        // reaches checkLinkVulnerable. Do not reuse the sword's invincibility gate.
        if (!player.NativeObjectVulnerable || !player.EnemyContactHeightOverlaps(0) ||
            !Overlaps(ItemCollisionType.Link,new(player.EnemyContactPosition - new Vector2(6,6),new(12,12)))) return;
        int effect = data.Effect(Entity.CollisionMode,0);
        if (effect == CollisionEffect.ElectricShock)
        {
            Entity.PublishCollision(0xa0); Entity.DisableCollision();
            player.ApplyElectricShock(Entity.Position);
        }
        else if (effect == CollisionEffect.DamageLinkWithRingModifier)
        {
            // $7c is absent from effect3c's ring-protection IDs: ordinary fc damage.
            if (player.ApplyEnemyContactDamage(Entity.Position,2,RingDamageSource.Generic,34,15)) Entity.PublishCollision(ObjectCollisionFlags.JustHit);
        }
        else throw new NotSupportedException($"ENEMY_SMOG $7c Link effect${effect:x2} is not represented.");
    }
}
