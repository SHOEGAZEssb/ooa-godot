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
    EnemyCombatComponent combat)
    : RoomEntityAdapter<T>(entity, setTransitionDrawOffset),
        ILinkContactEntity, ISwordHittableRoomEntity, IRoomEntityLifetime
    where T : Node2D
{
    public bool Finished => combat.Finished;
    public void HandleLinkContact(Player player) => combat.HandleLinkContact(player);
    public bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        ICollection<RoomEntitySpawn> spawns) =>
        combat.ApplySwordHit(hitbox, sourcePosition, spawns);
    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
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
    public KeeseRoomEntity(KeeseCharacter keese)
        : base(keese, keese.SetTransitionDrawOffset, CreateCombat(keese))
    { }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player.Position, frame.Counter);

    private static EnemyCombatComponent CreateCombat(KeeseCharacter keese) =>
        EnemyCombatComponent.WithContactDamage(
            () => keese.IsDead,
            () => keese.CollisionBounds,
            _ => keese.TakeSwordHit(),
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
    public OctorokRoomEntity(OctorokCharacter octorok)
        : base(octorok, octorok.SetTransitionDrawOffset, CreateCombat(octorok))
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
    public bool ApplySwordHit(Rect2 hitbox, Vector2 sourcePosition, ICollection<RoomEntitySpawn> spawns) =>
        hitbox.Intersects(Entity.CollisionBounds) && Entity.DeflectWithSword();
    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}

internal sealed class ZolRoomEntity
    : CombatEnemyRoomEntityAdapter<ZolCharacter>, IFixedRoomEntity
{
    public ZolRoomEntity(ZolCharacter zol)
        : base(zol, zol.SetTransitionDrawOffset, CreateCombat(zol))
    { }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        switch (Entity.UpdateFrame(frame.Player.Position))
        {
            case ZolCharacter.UpdateEvent.BeginSplit:
                spawns.Add(new KillEnemyPuffSpawn(Entity.Position));
                break;
            case ZolCharacter.UpdateEvent.SpawnGels:
                spawns.Add(new GelSpawn(Entity.Position + Vector2.Right * 4.0f, "SplitGelRight"));
                spawns.Add(new GelSpawn(Entity.Position + Vector2.Left * 4.0f, "SplitGelLeft"));
                break;
        }
    }

    private static EnemyCombatComponent CreateCombat(ZolCharacter zol) =>
        EnemyCombatComponent.WithContactDamage(
            () => zol.IsDead,
            () => zol.CollisionBounds,
            _ => zol.TakeSwordHit(),
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
    public GelRoomEntity(GelCharacter gel)
        : base(gel, gel.SetTransitionDrawOffset, CreateCombat(gel))
    { }

    public bool DisablesSword => Entity.IsAttached;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player.Position, frame.Player.FacingVector, frame.AnyButtonJustPressed);

    private static EnemyCombatComponent CreateCombat(GelCharacter gel) =>
        new(
            () => gel.IsDead,
            () => gel.CollisionBounds,
            _ => gel.TakeSwordHit(),
            player =>
            {
                if (gel.OverlapsLink(player.Position))
                    gel.AttachToLink(player.Position);
            },
            () => gel.IsDead && !gel.DiedInHazard
                ? new EnemyDeathPuffSpawn(gel.Position, EnemyId: gel.Definition.Id)
                : null);
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
