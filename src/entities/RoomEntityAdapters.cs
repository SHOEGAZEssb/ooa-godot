using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal abstract class RoomEntityAdapter<T> : IRoomEntity where T : Node2D
{
    private readonly Action<Vector2> _setTransitionDrawOffset;

    protected RoomEntityAdapter(T node, Action<Vector2> setTransitionDrawOffset)
    {
        Entity = node;
        _setTransitionDrawOffset = setTransitionDrawOffset;
    }

    protected T Entity { get; }
    public Node2D Node => Entity;
    public void SetTransitionDrawOffset(Vector2 offset) =>
        _setTransitionDrawOffset(offset);
}

internal abstract class CombatEnemyRoomEntityAdapter<T>(
    T entity,
    Action<Vector2> setTransitionDrawOffset,
    EnemyCombatComponent combat,
    bool countsAsEnemy,
    int killableEnemyIndex,
    Func<bool>? marksEnemyKilled = null,
    Action? finished = null)
    : RoomEntityAdapter<T>(entity, setTransitionDrawOffset),
        ILinkContactEntity, ISwordHittableRoomEntity, ISeedHittableRoomEntity,
        IRoomEntityLifetime,
        IRoomEnemyCounterEntity, IRoomKillTrackedEnemy
    where T : Node2D
{
    public bool Finished => combat.Finished;
    public bool CountsAsEnemy => countsAsEnemy && !combat.Finished;
    public int KillableEnemyIndex => killableEnemyIndex;
    // enemyDie and enemyDie_uncounted both advance the lifetime/special-ring
    // counters. Only the separate recent-defeat reservation requires a
    // nonzero wKillableEnemyIndex.
    public bool MarksEnemyKilled => marksEnemyKilled?.Invoke() ?? true;
    public void HandleLinkContact(Player player) => combat.HandleLinkContact(player);
    public bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        ICollection<RoomEntitySpawn> spawns) =>
        combat.ApplySwordHit(hitbox, sourcePosition, damage, spawns);
    public bool ApplySeedHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        ICollection<RoomEntitySpawn> spawns) =>
        combat.ApplySwordHit(hitbox, sourcePosition, 2, spawns);
    public void OnFinished(ICollection<RoomEntitySpawn> spawns) => finished?.Invoke();
}

internal sealed class EmberSeedRoomEntity(EmberSeedEffect seed)
    : RoomEntityAdapter<EmberSeedEffect>(seed, seed.SetTransitionDrawOffset),
        IFixedRoomEntity, ISeedProjectileRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.Finished;
    public bool CollisionEnabled => Entity.CollisionEnabled;
    public Rect2 CollisionBounds => Entity.CollisionBounds;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(spawns);
    public void OnEnemyCollision() => Entity.OnEnemyCollision();
    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}

internal sealed class SwordBeamRoomEntity(SwordBeamEffect beam)
    : RoomEntityAdapter<SwordBeamEffect>(beam, beam.SetTransitionDrawOffset),
        IFixedRoomEntity, IPlayerProjectileRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.Finished;
    public bool CollisionEnabled => Entity.CollisionEnabled;
    public Rect2 CollisionBounds => Entity.CollisionBounds;
    public int Damage => Entity.Damage;
    public void UpdateFrame(
        RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Counter, spawns);
    public void OnEnemyCollision(ICollection<RoomEntitySpawn> spawns) =>
        Entity.OnEnemyCollision(spawns);
    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}

internal sealed class SwordBeamClinkRoomEntity(ClinkEffect clink)
    : RoomEntityAdapter<ClinkEffect>(clink, static _ => { }),
        IFixedRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.Finished;
    public void UpdateFrame(
        RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.AdvanceFrameForEntityManager();
    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}

internal sealed class RoomTileChangeWatcherRoomEntity
    : RoomEntityAdapter<Node2D>, IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly RoomTileChangeWatcherDatabase.Record _record;
    private readonly OracleRoomData _room;
    private readonly OracleSaveData _save;
    private readonly Vector2 _tilePoint;
    private byte _initialTile;

    internal bool Initialized { get; private set; }
    public bool Finished { get; private set; }

    internal RoomTileChangeWatcherRoomEntity(
        RoomTileChangeWatcherDatabase.Record record,
        OracleRoomData room,
        OracleSaveData save)
        : base(CreateNode(record), static _ => { })
    {
        _record = record;
        _room = room;
        _save = save;
        int x = record.Position & 0x0f;
        int y = record.Position >> 4;
        if (x >= room.WidthInTiles || y >= room.HeightInTiles)
        {
            throw new InvalidOperationException(
                $"{record.Source} watches invalid position ${record.Position:x2} " +
                $"in room {record.Group:x1}:{record.Room:x2}.");
        }
        _tilePoint = new Vector2(
            x * OracleRoomData.MetatileSize + 8,
            y * OracleRoomData.MetatileSize + 8);
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;

        if (!Initialized)
        {
            // State 0 checks the flag before reading wRoomLayout. A watcher
            // whose persistent change was already applied deletes itself.
            if (_save.HasRoomFlag(
                _record.Group, _record.Room, _record.RoomFlag))
            {
                Finished = true;
                return;
            }
            _initialTile = _room.GetMetatile(_tilePoint);
            Initialized = true;
            return;
        }

        if (_room.GetMetatile(_tilePoint) == _initialTile)
            return;

        _save.SetRoomFlag(
            _record.Group, _record.Room, _record.RoomFlag);
        Finished = true;
    }

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }

    private static Node2D CreateNode(
        RoomTileChangeWatcherDatabase.Record record) => new()
    {
        Name = $"TileChangeWatcher_{record.Order}"
    };
}

internal sealed class NpcRoomEntity(NpcCharacter npc)
    : RoomEntityAdapter<NpcCharacter>(npc, npc.SetTransitionDrawOffset),
    IVariableRoomEntity, IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity
{
    public NpcCharacter Npc => Entity;
    public void Update(double delta, Player player) => Entity.UpdateNpc(delta, player.Position);
    public bool BlocksLink(Vector2 linkCenter) => Entity.BlocksLinkCenter(linkCenter);
    public NpcCharacter? FindTalkTarget(Player player) => Entity.CanTalkTo(player) ? Entity : null;
}

internal sealed class LynnaShopItemRoomEntity(LynnaShopItem item)
    : RoomEntityAdapter<LynnaShopItem>(item, item.SetTransitionDrawOffset),
        IFixedRoomEntity
{
    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame(frame.Player);
}

/// <summary>
/// INTERAC_SHOPKEEPER $46:$00 keeps its broad $06/$14 counter collision and
/// script-selected animation instead of using ordinary generic-NPC defaults.
/// </summary>
internal sealed class LynnaShopkeeperRoomEntity
    : RoomEntityAdapter<NpcCharacter>, IVariableRoomEntity, IFixedRoomEntity,
        IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity, IPlayerRestriction
{
    private readonly LynnaShopDatabase _database;

    public LynnaShopkeeperRoomEntity(
        NpcCharacter npc,
        LynnaShopDatabase database)
        : base(npc, npc.SetTransitionDrawOffset)
    {
        _database = database;
        npc.SetDialogue(0, string.Empty, canFace: false, database.TextboxPosition);
        npc.SetScriptButtonSensitive(true);
        npc.SetCollisionRadii(
            database.ShopkeeperRadiusY, database.ShopkeeperRadiusX);
        npc.SetScriptAnimation(database.Animation(0x46, 3));
    }

    public NpcCharacter Npc => Entity;
    public bool DisablesSword => false;
    public bool DisablesItems => true;
    public bool DisablesRingTransformations => true;

    public void Update(double delta, Player player) =>
        Entity.UpdateNpc(delta, player.Position);

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        Entity.PreventPlayerPassing(frame.Player);
        Entity.UpdateDrawPriority(frame.Player.Position);
    }

    public bool BlocksLink(Vector2 linkCenter) => Entity.BlocksLinkCenter(linkCenter);

    public NpcCharacter? FindTalkTarget(Player player) =>
        Entity.CanScriptTalkTo(
            player,
            _database.ShopkeeperRadiusY,
            _database.ShopkeeperRadiusX,
            _database.AButtonPointOffset)
            ? Entity
            : null;
}

/// <summary>
/// Native update behavior shared by the five placed actors in Vasu Jewelers.
/// Vasu uses the original asymmetric solid radius and object-side separation;
/// snakes restart their hidden idle frame while Link is outside the source's
/// $18 Manhattan-distance check.
/// </summary>
internal sealed class VasuShopNpcRoomEntity
    : RoomEntityAdapter<NpcCharacter>, IVariableRoomEntity, IFixedRoomEntity,
        IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity, IPlayerRestriction
{
    private readonly VasuShopDatabase _database;
    private readonly bool _vasu;
    private readonly bool _snake;
    private readonly string _idleAnimation;

    public VasuShopNpcRoomEntity(
        NpcCharacter npc,
        VasuShopDatabase database)
        : base(npc, npc.SetTransitionDrawOffset)
    {
        _database = database;
        _vasu = npc.Record is { Id: 0x89, SubId: 0x00 };
        _snake = npc.Record.Id == 0x89 && npc.Record.SubId != 0;
        npc.SetDialogue(0, string.Empty, canFace: false, database.TextboxPosition);
        npc.SetScriptButtonSensitive(true);
        if (_vasu)
        {
            npc.SetCollisionRadii(database.VasuRadiusY, database.VasuRadiusX);
            _idleAnimation = database.Animation(0x89, 0);
            npc.SetScriptAnimation(_idleAnimation);
        }
        else if (_snake)
        {
            npc.SetCollisionRadii(database.SnakeRadius, database.SnakeRadius);
            _idleAnimation = database.Animation(0x89, npc.Record.SubId);
            npc.SetScriptAnimation(_idleAnimation);
        }
        else
        {
            npc.SetCollisionRadii(database.SnakeRadius, database.SnakeRadius);
            // interactionInitGraphics leaves both books on the graphics
            // record's default animation $00. Subid $01 changes only palette.
            _idleAnimation = database.Animation(0xe5, 0);
            npc.SetScriptAnimation(_idleAnimation);
        }
    }

    public NpcCharacter Npc => Entity;
    public bool DisablesSword => false;
    public bool DisablesItems => true;
    public bool DisablesRingTransformations => true;

    public void Update(double delta, Player player) =>
        Entity.UpdateNpc(delta, player.Position);

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (_vasu)
        {
            Entity.PreventPlayerPassing(frame.Player);
            Entity.UpdateDrawPriority(frame.Player.Position);
            return;
        }

        if (!_snake || Entity.CurrentScriptAnimationSource != _idleAnimation)
            return;
        Vector2 delta = frame.Player.Position - Entity.Position;
        if (Mathf.Abs(delta.X) + Mathf.Abs(delta.Y) >=
            _database.SnakeProximityRadius)
        {
            // interactionSetAnimation runs on every out-of-range update,
            // pinning the snake to the first (hidden) idle frame.
            Entity.SetScriptAnimation(_idleAnimation);
        }
    }

    public bool BlocksLink(Vector2 linkCenter) => Entity.BlocksLinkCenter(linkCenter);

    public NpcCharacter? FindTalkTarget(Player player) =>
        Entity.CanScriptTalkTo(
            player,
            _vasu ? _database.VasuRadiusY : _database.SnakeRadius,
            _vasu ? _database.VasuRadiusX : _database.SnakeRadius,
            _database.AButtonPointOffset)
            ? Entity
            : null;
}

// INTERAC_BIPIN $28:$00 starts at SPEED_100/angle $18 and reverses whenever
// X leaves [$28,$58). Its var3a animation toggles between $04 and $05 at the
// same boundary update.
internal sealed class RunningBipinRoomEntity
    : RoomEntityAdapter<NpcCharacter>, IVariableRoomEntity, IFixedRoomEntity,
        IRoomBlocker, ITalkTarget
{
    private Vector2 _precisePosition;
    private int _angle = 0x18;
    private bool _alternateAnimation;

    public RunningBipinRoomEntity(NpcCharacter npc)
        : base(npc, npc.SetTransitionDrawOffset)
    {
        _precisePosition = npc.Position;
    }

    internal int Angle => _angle;
    internal Vector2 PrecisePosition => _precisePosition;

    public void Update(double delta, Player player) =>
        Entity.UpdateNpc(delta, player.Position);

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.Active)
            return;

        _precisePosition += OracleObjectMath.VectorFromAngle32(_angle);
        Entity.Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        float relativeX = Entity.Position.X - 0x28;
        if (relativeX < 0 || relativeX >= 0x30)
        {
            _angle ^= 0x10;
            _alternateAnimation = !_alternateAnimation;
            Entity.SetScriptAnimation(_alternateAnimation
                ? Entity.Record.RightAnimation
                : Entity.Record.DownAnimation);
        }

        // bipin.s calls objectPreventLinkFromPassing after objectApplySpeed,
        // so Bipin pushes Link to the nearest collision edge when his own
        // movement creates the overlap.
        Entity.PreventPlayerPassing(frame.Player);
        Entity.UpdateDrawPriority(frame.Player.Position);
    }

    public bool BlocksLink(Vector2 linkCenter) => Entity.BlocksLinkCenter(linkCenter);
    public NpcCharacter? FindTalkTarget(Player player) =>
        Entity.CanTalkTo(player) ? Entity : null;
}

// Script-created character interactions animate with the normal NPC renderer.
// Solidity and talking are opt-in because objectSetVisible82-only actors and
// followers do neither, while initialized NPC scripts call objectMarkSolidPosition.
internal sealed class CutsceneNpcRoomEntity(NpcCharacter npc, bool talkable, bool solid)
    : RoomEntityAdapter<NpcCharacter>(npc, npc.SetTransitionDrawOffset),
        IVariableRoomEntity, IRoomBlocker, ITalkTarget
{
    public void Update(double delta, Player player) => Entity.UpdateNpc(delta, player.Position);
    public bool BlocksLink(Vector2 linkCenter) => solid && Entity.BlocksLinkCenter(linkCenter);
    public NpcCharacter? FindTalkTarget(Player player) =>
        talkable && Entity.CanTalkTo(player) ? Entity : null;
}

internal sealed class TimePortalRoomEntity(TimePortal portal, Action<TimePortal> entered)
    : RoomEntityAdapter<TimePortal>(portal, portal.SetTransitionDrawOffset),
        IFixedRoomEntity, ILinkContactEntity
{
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Counter);
    public void HandleLinkContact(Player player)
    {
        if (Entity.CheckLinkContact(player.Position))
            entered(Entity);
    }
}

internal sealed class GroundTreasureRoomEntity(
    GroundTreasurePickup treasure,
    Func<bool> collectionAllowed,
    Action<GroundTreasurePickup, Player> collected)
    : RoomEntityAdapter<GroundTreasurePickup>(
        treasure, treasure.SetTransitionDrawOffset),
        IFixedRoomEntity, ILinkContactEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.Finished;

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player);

    public void HandleLinkContact(Player player)
    {
        if (collectionAllowed() && Entity.TryCollect(player))
            collected(Entity, player);
    }

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}

internal sealed class KeeseRoomEntity
    : CombatEnemyRoomEntityAdapter<KeeseCharacter>, IFixedRoomEntity
{
    public KeeseRoomEntity(KeeseCharacter keese, int killableEnemyIndex = 0)
        : base(
            keese, keese.SetTransitionDrawOffset, CreateCombat(keese),
            (keese.Record.Flags & 0x02) == 0, killableEnemyIndex)
    { }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player.Position, frame.Counter);

    private static EnemyCombatComponent CreateCombat(KeeseCharacter keese) =>
        EnemyCombatComponent.WithContactDamage(
            () => keese.IsDead,
            () => keese.CollisionBounds,
            (_, damage) => keese.TakeSwordHit(damage),
            keese.OverlapsLink,
            () => keese.Position,
            keese.Record.DamageQuarters,
            () => keese.IsDead
                ? new EnemyDeathPuffSpawn(
                    keese.Position + Vector2.Down * keese.SpriteHeight,
                    EnemyId: keese.Record.Id)
                : null);
}

internal sealed class OctorokRoomEntity
    : CombatEnemyRoomEntityAdapter<OctorokCharacter>, IFixedRoomEntity
{
    public OctorokRoomEntity(
        OctorokCharacter octorok,
        Action<int> soundRequested,
        int killableEnemyIndex = 0)
        : base(
            octorok, octorok.SetTransitionDrawOffset, CreateCombat(octorok),
            (octorok.Record.Flags & 0x02) == 0, killableEnemyIndex,
            finished: () => EnemyHazardSounds.PlayHoleSound(
                octorok.DeathHazard, soundRequested))
    { }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.UpdateFrame(frame.Player.Position))
            spawns.Add(new OctorokRockSpawn(Entity.Position, Entity.Angle));
    }

    private static EnemyCombatComponent CreateCombat(OctorokCharacter octorok) =>
        EnemyCombatComponent.WithContactDamage(
            () => octorok.IsDead,
            () => octorok.CollisionBounds,
            octorok.TakeSwordHit,
            octorok.OverlapsLink,
            () => octorok.Position,
            octorok.Record.DamageQuarters,
            () => octorok.IsDead && !octorok.DiedInHazard
                ? new EnemyDeathPuffSpawn(
                    octorok.Position, EnemyId: octorok.Record.Id)
                : null);
}

internal sealed class OctorokRockRoomEntity(OctorokRockProjectile rock)
    : RoomEntityAdapter<OctorokRockProjectile>(rock, rock.SetTransitionDrawOffset),
        IFixedRoomEntity, ISwordHittableRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.Finished;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player);
    public bool ApplySwordHit(Rect2 hitbox, Vector2 sourcePosition, int damage, ICollection<RoomEntitySpawn> spawns) =>
        hitbox.Intersects(Entity.CollisionBounds) && Entity.DeflectWithSword();
    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}

internal sealed class MaskedMoblinRoomEntity
    : CombatEnemyRoomEntityAdapter<MaskedMoblinCharacter>, IFixedRoomEntity
{
    public MaskedMoblinRoomEntity(
        MaskedMoblinCharacter moblin,
        Action<int> soundRequested)
        : base(
            moblin, moblin.SetTransitionDrawOffset, CreateCombat(moblin),
            countsAsEnemy: true, killableEnemyIndex: 0,
            finished: () => EnemyHazardSounds.PlayHoleSound(
                moblin.DeathHazard, soundRequested))
    { }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        int arrowAngle = Entity.UpdateFrame(frame.Player.Position);
        if (arrowAngle >= 0)
            spawns.Add(new EnemyArrowSpawn(Entity.Position, arrowAngle));
    }

    private static EnemyCombatComponent CreateCombat(MaskedMoblinCharacter moblin) =>
        EnemyCombatComponent.WithContactDamage(
            () => moblin.IsDead,
            () => moblin.CollisionBounds,
            moblin.TakeSwordHit,
            moblin.OverlapsLink,
            () => moblin.Position,
            moblin.Record.DamageQuarters,
            () => moblin.IsDead && !moblin.DiedInHazard
                ? new EnemyDeathPuffSpawn(
                    moblin.Position, EnemyId: moblin.Record.Id)
                : null);
}

internal sealed class EnemyArrowRoomEntity(EnemyArrowProjectile arrow)
    : RoomEntityAdapter<EnemyArrowProjectile>(arrow, arrow.SetTransitionDrawOffset),
        IFixedRoomEntity, ISwordHittableRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.Finished;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player);
    public bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        ICollection<RoomEntitySpawn> spawns) =>
        hitbox.Intersects(Entity.CollisionBounds) && Entity.DeflectWithSword();
    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}

internal sealed class StalfosRoomEntity
    : CombatEnemyRoomEntityAdapter<StalfosCharacter>, IFixedRoomEntity
{
    public StalfosRoomEntity(
        StalfosCharacter stalfos,
        Action<int> soundRequested,
        int killableEnemyIndex = 0)
        : base(
            stalfos, stalfos.SetTransitionDrawOffset, CreateCombat(stalfos),
            (stalfos.Record.Flags & 0x02) == 0, killableEnemyIndex,
            finished: () => EnemyHazardSounds.PlayHoleSound(
                stalfos.DeathHazard, soundRequested))
    { }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player.Position);

    private static EnemyCombatComponent CreateCombat(StalfosCharacter stalfos) =>
        EnemyCombatComponent.WithContactDamage(
            () => stalfos.IsDead,
            () => stalfos.CollisionBounds,
            stalfos.TakeSwordHit,
            stalfos.OverlapsLink,
            () => stalfos.Position,
            stalfos.Record.DamageQuarters,
            () => stalfos.IsDead && !stalfos.DiedInHazard
                ? new EnemyDeathPuffSpawn(
                    stalfos.Position, EnemyId: stalfos.Record.Id)
                : null);
}

internal sealed class ZolRoomEntity
    : CombatEnemyRoomEntityAdapter<ZolCharacter>, IFixedRoomEntity
{
    public ZolRoomEntity(
        ZolCharacter zol,
        Action<int> soundRequested,
        int killableEnemyIndex = 0)
        : base(
            zol, zol.SetTransitionDrawOffset, CreateCombat(zol),
            (zol.Record.Flags & 0x02) == 0, killableEnemyIndex,
            () => zol.Record.SubId != 1 || zol.DiedInHazard,
            () => EnemyHazardSounds.PlayHoleSound(
                zol.DeathHazard, soundRequested))
    { }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        switch (Entity.UpdateFrame(frame.Player.Position))
        {
            case ZolCharacter.UpdateEvent.BeginSplit:
                spawns.Add(new KillEnemyPuffSpawn(Entity.Position));
                break;
            case ZolCharacter.UpdateEvent.SpawnGels:
                spawns.Add(new GelSpawn(
                    Entity.Position + Vector2.Right * 4.0f,
                    "SplitGelRight", KillableEnemyIndex));
                spawns.Add(new GelSpawn(
                    Entity.Position + Vector2.Left * 4.0f,
                    "SplitGelLeft", KillableEnemyIndex));
                break;
        }
    }

    private static EnemyCombatComponent CreateCombat(ZolCharacter zol) =>
        EnemyCombatComponent.WithContactDamage(
            () => zol.IsDead,
            () => zol.CollisionBounds,
            (_, damage) => zol.TakeSwordHit(damage),
            zol.OverlapsLink,
            () => zol.Position,
            zol.Record.DamageQuarters,
            () => zol.IsDead && !zol.DiedInHazard
                ? new EnemyDeathPuffSpawn(zol.Position, EnemyId: zol.Record.Id)
                : null);
}

internal sealed class GelRoomEntity
    : CombatEnemyRoomEntityAdapter<GelCharacter>, IFixedRoomEntity, IPlayerRestriction
{
    public GelRoomEntity(
        GelCharacter gel,
        Action<int> soundRequested,
        bool countsAsEnemy = true,
        int killableEnemyIndex = 0)
        : base(
            gel, gel.SetTransitionDrawOffset, CreateCombat(gel),
            countsAsEnemy, killableEnemyIndex,
            finished: () => EnemyHazardSounds.PlayHoleSound(
                gel.DeathHazard, soundRequested))
    { }

    public bool DisablesSword => Entity.IsAttached;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player.Position, frame.Player.FacingVector, frame.AnyButtonJustPressed);

    private static EnemyCombatComponent CreateCombat(GelCharacter gel) =>
        new(
            () => gel.IsDead,
            () => gel.CollisionBounds,
            (_, damage) => gel.TakeSwordHit(damage),
            player =>
            {
                if (gel.OverlapsLink(player.Position))
                    gel.AttachToLink(player.Position);
            },
            () => gel.IsDead && !gel.DiedInHazard
                ? new EnemyDeathPuffSpawn(gel.Position, EnemyId: gel.Definition.Id)
                : null);
}

internal static class EnemyHazardSounds
{
    internal static void PlayHoleSound(
        OracleRoomData.HazardType hazard,
        Action<int> soundRequested)
    {
        if (hazard == OracleRoomData.HazardType.Hole)
            soundRequested(OracleSoundEngine.SndFallInHole);
    }
}

internal sealed class DeathPuffRoomEntity(
    EnemyDeathPuffEffect puff,
    ItemDropDatabase itemDrops,
    OracleRandom random)
    : RoomEntityAdapter<EnemyDeathPuffEffect>(puff, puff.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.Finished;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Counter);
    public void OnFinished(ICollection<RoomEntitySpawn> spawns)
    {
        int? subId = itemDrops.DecideDrop(Entity.EnemyId, random);
        if (subId.HasValue)
            spawns.Add(new ItemDropSpawn(subId.Value, Entity.Position));
    }
}

internal sealed class KillPuffRoomEntity(KillEnemyPuffEffect puff)
    : RoomEntityAdapter<KillEnemyPuffEffect>(puff, puff.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.Finished;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame();
    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}

internal sealed class ItemDropRoomEntity(
    ItemDropEffect drop,
    Action<Vector2, OracleRoomData.HazardType> enteredHazard)
    : RoomEntityAdapter<ItemDropEffect>(drop, drop.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.Finished;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player, frame.Counter);
    public void OnFinished(ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.FinishedHazard is
            OracleRoomData.HazardType.Water or OracleRoomData.HazardType.Lava)
        {
            enteredHazard(Entity.Position, Entity.FinishedHazard);
        }
    }
}
