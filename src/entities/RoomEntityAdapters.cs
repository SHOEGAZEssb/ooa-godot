using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal abstract class RoomEntityAdapter<T> : IRoomEntity where T : Node2D
{
    protected RoomEntityAdapter(T node) => Entity = node;

    protected T Entity { get; }
    public Node2D Node => Entity;
    public abstract void SetTransitionDrawOffset(Vector2 offset);
}

internal sealed class NpcRoomEntity(NpcCharacter npc) : RoomEntityAdapter<NpcCharacter>(npc),
    IVariableRoomEntity, IRoomBlocker, ITalkTarget
{
    public void Update(double delta, Player player) => Entity.UpdateNpc(delta, player.Position);
    public bool BlocksLink(Vector2 linkCenter) => Entity.BlocksLinkCenter(linkCenter);
    public NpcCharacter? FindTalkTarget(Player player) => Entity.CanTalkTo(player) ? Entity : null;
    public override void SetTransitionDrawOffset(Vector2 offset) => Entity.SetTransitionDrawOffset(offset);
}

// Script-created character interactions animate with the normal NPC renderer,
// but are not automatically solid or talkable. The original fake Octoroks use
// objectSetVisible82 without objectMarkSolidPosition, and followers must not
// trap Link against their delayed path.
internal sealed class CutsceneNpcRoomEntity(NpcCharacter npc)
    : RoomEntityAdapter<NpcCharacter>(npc), IVariableRoomEntity
{
    public void Update(double delta, Player player) => Entity.UpdateNpc(delta, player.Position);
    public override void SetTransitionDrawOffset(Vector2 offset) => Entity.SetTransitionDrawOffset(offset);
}

internal sealed class TimePortalRoomEntity(TimePortal portal, System.Action<TimePortal> entered)
    : RoomEntityAdapter<TimePortal>(portal), IFixedRoomEntity, ILinkContactEntity
{
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Counter);
    public void HandleLinkContact(Player player)
    {
        if (Entity.CheckLinkContact(player.Position))
            entered(Entity);
    }
    public override void SetTransitionDrawOffset(Vector2 offset) => Entity.SetTransitionDrawOffset(offset);
}

internal sealed class KeeseRoomEntity(KeeseCharacter keese) : RoomEntityAdapter<KeeseCharacter>(keese),
    IFixedRoomEntity, ILinkContactEntity, ISwordHittableRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.IsDead;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player.Position, frame.Counter);
    public void HandleLinkContact(Player player)
    {
        if (Entity.OverlapsLink(player.Position))
            player.ApplyEnemyContactDamage(Entity.Position, Entity.Record.DamageQuarters);
    }
    public bool ApplySwordHit(Rect2 hitbox, Vector2 sourcePosition, ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.IsDead || !hitbox.Intersects(Entity.CollisionBounds))
            return false;
        bool struck = Entity.TakeSwordHit();
        if (struck && Entity.IsDead)
            spawns.Add(new EnemyDeathPuffSpawn(Entity.Position + Vector2.Down * Entity.SpriteHeight,
                EnemyId: Entity.Record.Id));
        return struck;
    }
    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
    public override void SetTransitionDrawOffset(Vector2 offset) => Entity.SetTransitionDrawOffset(offset);
}

internal sealed class OctorokRoomEntity(OctorokCharacter octorok)
    : RoomEntityAdapter<OctorokCharacter>(octorok), IFixedRoomEntity, ILinkContactEntity,
        ISwordHittableRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.IsDead;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.UpdateFrame(frame.Player.Position))
            spawns.Add(new OctorokRockSpawn(Entity.Position, Entity.Angle));
    }
    public void HandleLinkContact(Player player)
    {
        if (Entity.OverlapsLink(player.Position))
            player.ApplyEnemyContactDamage(Entity.Position, Entity.Record.DamageQuarters);
    }
    public bool ApplySwordHit(Rect2 hitbox, Vector2 sourcePosition, ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.IsDead || !hitbox.Intersects(Entity.CollisionBounds))
            return false;
        bool struck = Entity.TakeSwordHit(sourcePosition);
        if (struck && Entity.IsDead && !Entity.DiedInHazard)
            spawns.Add(new EnemyDeathPuffSpawn(Entity.Position, EnemyId: Entity.Record.Id));
        return struck;
    }
    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
    public override void SetTransitionDrawOffset(Vector2 offset) => Entity.SetTransitionDrawOffset(offset);
}

internal sealed class OctorokRockRoomEntity(OctorokRockProjectile rock)
    : RoomEntityAdapter<OctorokRockProjectile>(rock), IFixedRoomEntity,
        ISwordHittableRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.Finished;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player);
    public bool ApplySwordHit(Rect2 hitbox, Vector2 sourcePosition, ICollection<RoomEntitySpawn> spawns) =>
        hitbox.Intersects(Entity.CollisionBounds) && Entity.DeflectWithSword();
    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
    public override void SetTransitionDrawOffset(Vector2 offset) => Entity.SetTransitionDrawOffset(offset);
}

internal sealed class ZolRoomEntity(ZolCharacter zol) : RoomEntityAdapter<ZolCharacter>(zol),
    IFixedRoomEntity, ILinkContactEntity, ISwordHittableRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.IsDead;
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
    public void HandleLinkContact(Player player)
    {
        if (Entity.OverlapsLink(player.Position))
            player.ApplyEnemyContactDamage(Entity.Position, Entity.Record.DamageQuarters);
    }
    public bool ApplySwordHit(Rect2 hitbox, Vector2 sourcePosition, ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.IsDead || !hitbox.Intersects(Entity.CollisionBounds))
            return false;
        bool struck = Entity.TakeSwordHit();
        if (struck && Entity.IsDead && !Entity.DiedInHazard)
            spawns.Add(new EnemyDeathPuffSpawn(Entity.Position, EnemyId: Entity.Record.Id));
        return struck;
    }
    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
    public override void SetTransitionDrawOffset(Vector2 offset) => Entity.SetTransitionDrawOffset(offset);
}

internal sealed class GelRoomEntity(GelCharacter gel) : RoomEntityAdapter<GelCharacter>(gel),
    IFixedRoomEntity, ILinkContactEntity, ISwordHittableRoomEntity, IRoomEntityLifetime,
    IPlayerRestriction
{
    public bool Finished => Entity.IsDead;
    public bool DisablesSword => Entity.IsAttached;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player.Position, frame.Player.FacingVector, frame.AnyButtonJustPressed);
    public void HandleLinkContact(Player player)
    {
        if (Entity.OverlapsLink(player.Position))
            Entity.AttachToLink(player.Position);
    }
    public bool ApplySwordHit(Rect2 hitbox, Vector2 sourcePosition, ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.IsDead || !hitbox.Intersects(Entity.CollisionBounds))
            return false;
        bool struck = Entity.TakeSwordHit();
        if (struck && Entity.IsDead && !Entity.DiedInHazard)
            spawns.Add(new EnemyDeathPuffSpawn(Entity.Position, EnemyId: Entity.Definition.Id));
        return struck;
    }
    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
    public override void SetTransitionDrawOffset(Vector2 offset) => Entity.SetTransitionDrawOffset(offset);
}

internal sealed class DeathPuffRoomEntity(
    EnemyDeathPuffEffect puff,
    ItemDropDatabase itemDrops,
    OracleRandom random) : RoomEntityAdapter<EnemyDeathPuffEffect>(puff),
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
    public override void SetTransitionDrawOffset(Vector2 offset) => Entity.SetTransitionDrawOffset(offset);
}

internal sealed class KillPuffRoomEntity(KillEnemyPuffEffect puff)
    : RoomEntityAdapter<KillEnemyPuffEffect>(puff), IFixedRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.Finished;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame();
    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
    public override void SetTransitionDrawOffset(Vector2 offset) => Entity.SetTransitionDrawOffset(offset);
}

internal sealed class ItemDropRoomEntity(ItemDropEffect drop)
    : RoomEntityAdapter<ItemDropEffect>(drop), IFixedRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.Finished;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player, frame.Counter);
    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
    public override void SetTransitionDrawOffset(Vector2 offset) => Entity.SetTransitionDrawOffset(offset);
}
