using Godot;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Owns the active and outgoing room-entity sets. Per-type behavior is exposed
/// through small capability interfaces and constructed by RoomEntityFactory.
/// </summary>
public sealed partial class RoomEntityManager
{
    private readonly Node _worldRoot;
    private readonly RoomEntityFactory _factory;
    private readonly List<IRoomEntity> _activeEntities = new();
    private readonly List<IRoomEntity> _outgoingEntities = new();
    private readonly List<RoomEntitySpawn> _pendingSpawns = new();
    private OracleRoomData _roomForActiveEntities = null!;
    private bool _screenTransitionActive;
    private double _enemyFrameAccumulator;
    private int _enemyFrameCounter;

    public bool ScreenTransitionActive => _screenTransitionActive;
    public bool PlayerSwordDisabled
    {
        get
        {
            foreach (IRoomEntity entity in _activeEntities)
            {
                if (entity is IPlayerRestriction { DisablesSword: true })
                    return true;
            }
            return false;
        }
    }
    public bool PlayerMovementDisabled => PlayerSwordDisabled && (_enemyFrameCounter & 1) != 0;

    public RoomEntityManager(Node worldRoot, NpcDatabase npcs, EnemyDatabase enemies)
        : this(worldRoot, npcs, enemies, new ItemDropDatabase(), new OracleRandom())
    {
    }

    internal RoomEntityManager(
        Node worldRoot,
        NpcDatabase npcs,
        EnemyDatabase enemies,
        ItemDropDatabase itemDrops,
        OracleRandom random)
    {
        _worldRoot = worldRoot;
        _factory = new RoomEntityFactory(npcs, enemies, itemDrops, random);
    }

    public List<T> Entities<T>() where T : Node2D => SelectNodes<T>(_activeEntities);
    public List<T> OutgoingEntities<T>() where T : Node2D => SelectNodes<T>(_outgoingEntities);

    public void LoadRoom(int group, OracleRoomData room)
    {
        Clear();
        _roomForActiveEntities = room;
        AddRoomEntities(group, room);
    }

    public void BeginScreenTransition(int group, OracleRoomData room, Vector2 incomingOffset)
    {
        ClearEntities(_outgoingEntities);
        _outgoingEntities.AddRange(_activeEntities);
        _activeEntities.Clear();
        _screenTransitionActive = true;
        _roomForActiveEntities = room;
        AddRoomEntities(group, room);
        SetScreenTransitionOffsets(Vector2.Zero, incomingOffset);
    }

    public void SetScreenTransitionOffsets(Vector2 outgoingOffset, Vector2 incomingOffset)
    {
        if (!_screenTransitionActive)
            return;
        foreach (IRoomEntity entity in _outgoingEntities)
            entity.SetTransitionDrawOffset(outgoingOffset);
        foreach (IRoomEntity entity in _activeEntities)
            entity.SetTransitionDrawOffset(incomingOffset);
    }

    public void FinishScreenTransition()
    {
        if (!_screenTransitionActive)
            return;
        ClearEntities(_outgoingEntities);
        foreach (IRoomEntity entity in _activeEntities)
            entity.SetTransitionDrawOffset(Vector2.Zero);
        _screenTransitionActive = false;
    }

    public void Update(double delta, Player player)
    {
        // The original engine freezes both enabled $02 outgoing objects and
        // enabled $01 destination objects until scrolling has completed.
        if (_screenTransitionActive)
            return;

        foreach (IRoomEntity entity in _activeEntities.ToArray())
        {
            if (entity is IVariableRoomEntity variableEntity)
                variableEntity.Update(delta, player);
        }

        bool anyButtonJustPressed = AnyGameButtonJustPressed();
        _enemyFrameAccumulator += delta * 60.0;
        while (_enemyFrameAccumulator >= 1.0)
        {
            _enemyFrameAccumulator -= 1.0;
            _enemyFrameCounter = (_enemyFrameCounter + 1) & 0xff;
            var frame = new RoomEntityFrame(player, _enemyFrameCounter, anyButtonJustPressed);
            foreach (IRoomEntity entity in _activeEntities.ToArray())
            {
                if (entity is IFixedRoomEntity fixedEntity)
                    fixedEntity.UpdateFrame(frame, _pendingSpawns);
                ProcessSpawns(frame);
            }
            anyButtonJustPressed = false;
        }

        foreach (IRoomEntity entity in _activeEntities.ToArray())
        {
            if (entity is ILinkContactEntity contactEntity)
                contactEntity.HandleLinkContact(player);
        }
        RemoveFinishedEntities();
    }

    public bool BlocksLink(Vector2 linkCenter)
    {
        foreach (IRoomEntity entity in _activeEntities)
        {
            if (entity is IRoomBlocker blocker && blocker.BlocksLink(linkCenter))
                return true;
        }
        return false;
    }

    public NpcCharacter? FindTalkTarget(Player player)
    {
        foreach (IRoomEntity entity in _activeEntities)
        {
            if (entity is ITalkTarget talkTarget && talkTarget.FindTalkTarget(player) is { } target)
                return target;
        }
        return null;
    }

    public bool ApplySwordHit(Rect2 hitbox, Vector2? sourcePosition = null)
    {
        bool hit = false;
        Vector2 source = sourcePosition ?? hitbox.GetCenter();
        foreach (IRoomEntity entity in _activeEntities.ToArray())
        {
            if (entity is ISwordHittableRoomEntity swordHittable)
                hit |= swordHittable.ApplySwordHit(hitbox, source, _pendingSpawns);
            ProcessSpawns();
        }
        RemoveFinishedEntities();
        return hit;
    }

    internal T Spawn<T>(RoomEntitySpawn spawn) where T : Node2D
    {
        IRoomEntity entity = AddEntity(_factory.Create(spawn, _roomForActiveEntities));
        return (T)entity.Node;
    }

    public EnemyDeathPuffEffect SpawnEnemyDeathPuff(
        Vector2 position,
        bool highKnockback = false,
        int enemyId = -1) =>
        Spawn<EnemyDeathPuffEffect>(new EnemyDeathPuffSpawn(position, highKnockback, enemyId));

    public void Clear()
    {
        ClearEntities(_outgoingEntities);
        ClearEntities(_activeEntities);
        _pendingSpawns.Clear();
        _screenTransitionActive = false;
        _enemyFrameAccumulator = 0.0;
    }

    private void AddRoomEntities(int group, OracleRoomData room)
    {
        foreach (IRoomEntity entity in _factory.CreateRoomEntities(group, room))
            AddEntity(entity);
    }

    private IRoomEntity AddEntity(IRoomEntity entity)
    {
        _activeEntities.Add(entity);
        _worldRoot.AddChild(entity.Node);
        return entity;
    }

    private void ProcessSpawns(RoomEntityFrame? frame = null)
    {
        while (_pendingSpawns.Count > 0)
        {
            RoomEntitySpawn spawn = _pendingSpawns[0];
            _pendingSpawns.RemoveAt(0);
            IRoomEntity entity = AddEntity(_factory.Create(spawn, _roomForActiveEntities));
            if (spawn.UpdateThisFrame && frame.HasValue && entity is IFixedRoomEntity fixedEntity)
                fixedEntity.UpdateFrame(frame.Value, _pendingSpawns);
        }
    }

    private void RemoveFinishedEntities()
    {
        for (int index = _activeEntities.Count - 1; index >= 0; index--)
        {
            IRoomEntity entity = _activeEntities[index];
            if (entity is not IRoomEntityLifetime { Finished: true } lifetime)
                continue;
            lifetime.OnFinished(_pendingSpawns);
            _activeEntities.RemoveAt(index);
            FreeEntity(entity);
        }
        ProcessSpawns();
    }

    private void ClearEntities(List<IRoomEntity> entities)
    {
        foreach (IRoomEntity entity in entities)
            FreeEntity(entity);
        entities.Clear();
    }

    private void FreeEntity(IRoomEntity entity)
    {
        if (entity.Node.GetParent() == _worldRoot)
            _worldRoot.RemoveChild(entity.Node);
        entity.Node.QueueFree();
    }

    private static List<T> SelectNodes<T>(IEnumerable<IRoomEntity> entities) where T : Node2D
    {
        var result = new List<T>();
        foreach (IRoomEntity entity in entities)
        {
            if (entity.Node is T node)
                result.Add(node);
        }
        return result;
    }

    private static bool AnyGameButtonJustPressed() =>
        Input.IsActionJustPressed("attack") ||
        Input.IsActionJustPressed("item") ||
        Input.IsActionJustPressed("move_up") ||
        Input.IsActionJustPressed("move_right") ||
        Input.IsActionJustPressed("move_down") ||
        Input.IsActionJustPressed("move_left");
}
