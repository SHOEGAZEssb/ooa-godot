using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Owns the active and outgoing room-entity sets. Per-type behavior is exposed
/// through small capability interfaces and constructed by RoomEntityFactory.
/// </summary>
public sealed class RoomEntityManager
{
    public event Action<int, OracleRoomData>? RoomEntitiesLoaded;
    public event Action<TimePortal>? TimePortalEntered;
    internal event Action<GroundTreasurePickup, Player>? GroundTreasureCollected;
    public event Action<int>? SoundRequested;
    private readonly Node _worldRoot;
    private readonly RoomEntityFactory _factory;
    private readonly OracleRandom _random;
    private readonly OracleSaveData? _saveData;
    private readonly OracleRuntimeState _runtimeState;
    private readonly NpcVisibilityRuleDatabase _npcVisibility = new();
    private readonly NpcDialogueRuleDatabase _npcDialogue = new();
    private readonly NpcPositionRuleDatabase _npcPositions = new();
    private readonly List<IRoomEntity> _activeEntities = new();
    private readonly List<IRoomEntity> _outgoingEntities = new();
    private readonly List<RoomEntitySpawn> _pendingSpawns = new();
    private OracleRoomData _roomForActiveEntities = null!;
    private bool _screenTransitionActive;
    private double _enemyFrameAccumulator;
    private int _enemyFrameCounter;

    internal Func<bool> GameButtonJustPressedSource { get; set; } =
        ReadGameButtonJustPressed;
    internal Func<bool> GroundTreasureCollectionAllowed { get; set; } =
        static () => true;

    public bool ScreenTransitionActive => _screenTransitionActive;
    public OracleRuntimeState RuntimeState => _runtimeState;
    internal int FrameCounter => _enemyFrameCounter;
    internal int RandomCalls => _random.Calls;
    internal byte NextRandomValue() => _random.Next().Value;
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

    public RoomEntityManager(
        Node worldRoot,
        NpcDatabase npcs,
        EnemyDatabase enemies,
        OracleSaveData? saveData = null,
        OracleRuntimeState? runtimeState = null)
        : this(worldRoot, npcs, enemies, new ItemDropDatabase(),
            new TimePortalDatabase(), new OracleRandom(), saveData, runtimeState)
    { }

    internal RoomEntityManager(
        Node worldRoot,
        NpcDatabase npcs,
        EnemyDatabase enemies,
        ItemDropDatabase itemDrops,
        TimePortalDatabase timePortals,
        OracleRandom random,
        OracleSaveData? saveData = null,
        OracleRuntimeState? runtimeState = null)
    {
        _worldRoot = worldRoot;
        _random = random;
        _saveData = saveData;
        _runtimeState = runtimeState ?? new OracleRuntimeState();
        _factory = new RoomEntityFactory(
            npcs, enemies, itemDrops, timePortals, random,
            _saveData, _runtimeState, OnTimePortalEntered,
            () => GroundTreasureCollectionAllowed(),
            OnGroundTreasureCollected, OnSoundRequested);
        if (_saveData is not null)
            _saveData.Changed += RefreshNpcState;
        _runtimeState.Changed += RefreshNpcState;
    }

    public List<T> Entities<T>() where T : Node2D => SelectNodes<T>(_activeEntities);
    public List<T> OutgoingEntities<T>() where T : Node2D => SelectNodes<T>(_outgoingEntities);

    public void LoadRoom(int group, OracleRoomData room)
    {
        LoadRoom(group, room, EnemyPlacementContext.Unrestricted);
    }

    internal void LoadRoom(
        int group,
        OracleRoomData room,
        EnemyPlacementContext placementContext)
    {
        Clear();
        _roomForActiveEntities = room;
        AddRoomEntities(group, room, placementContext);
    }

    /// <summary>
    /// Mirrors disableLcdAndLoadRoom followed by parseGivenObjectData: change
    /// the room backing the entity set without parsing its ordinary object list.
    /// The caller may retain time portals explicitly present in the cutscene set.
    /// </summary>
    public void LoadCutsceneRoom(int group, OracleRoomData room, bool includeTimePortals)
    {
        Clear();
        _roomForActiveEntities = room;
        if (!includeTimePortals)
            return;
        foreach (IRoomEntity portal in _factory.CreateTimePortals(group, room))
            AddEntity(portal);
    }

    public void BeginScreenTransition(int group, OracleRoomData room, Vector2 incomingOffset)
    {
        BeginScreenTransition(
            group, room, incomingOffset, EnemyPlacementContext.Unrestricted);
    }

    internal void BeginScreenTransition(
        int group,
        OracleRoomData room,
        Vector2 incomingOffset,
        Vector2I scrollDirection)
    {
        BeginScreenTransition(
            group, room, incomingOffset,
            EnemyPlacementContext.Scrolling(scrollDirection));
    }

    private void BeginScreenTransition(
        int group,
        OracleRoomData room,
        Vector2 incomingOffset,
        EnemyPlacementContext placementContext)
    {
        ClearEntities(_outgoingEntities);
        _outgoingEntities.AddRange(_activeEntities);
        _activeEntities.Clear();
        _screenTransitionActive = true;
        _roomForActiveEntities = room;
        AddRoomEntities(group, room, placementContext);
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

        bool anyButtonJustPressed = GameButtonJustPressedSource();
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

    internal bool BeginNpcTalk(NpcCharacter npc)
    {
        foreach (IRoomEntity entity in _activeEntities)
        {
            if (entity is INpcTalkLifecycle lifecycle &&
                ReferenceEquals(lifecycle.TalkNpc, npc))
            {
                lifecycle.OnNpcTalkStarted();
                return true;
            }
        }
        return false;
    }

    internal void EndNpcTalk(NpcCharacter npc)
    {
        foreach (IRoomEntity entity in _activeEntities)
        {
            if (entity is INpcTalkLifecycle lifecycle &&
                ReferenceEquals(lifecycle.TalkNpc, npc))
            {
                lifecycle.OnNpcTalkEnded();
                return;
            }
        }
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

    public void Clear()
    {
        ClearEntities(_outgoingEntities);
        ClearEntities(_activeEntities);
        _pendingSpawns.Clear();
        _screenTransitionActive = false;
        _enemyFrameAccumulator = 0.0;
    }

    private void AddRoomEntities(
        int group,
        OracleRoomData room,
        EnemyPlacementContext placementContext)
    {
        // parseObjectData clears wEnemyPlacement, then rebuilds w4RandomBuffer.
        // This consumes 256 values from the game-wide RNG on every room parse.
        _random.BeginRoomParse();
        foreach (IRoomEntity entity in _factory.CreateRoomEntities(
            group, room, placementContext))
            AddEntity(entity);
        RefreshNpcState(_activeEntities);
        RoomEntitiesLoaded?.Invoke(group, room);
    }

    private void RefreshNpcState()
    {
        RefreshNpcState(_outgoingEntities);
        RefreshNpcState(_activeEntities);
    }

    private void RefreshNpcState(IEnumerable<IRoomEntity> entities)
    {
        if (_saveData is null)
            return;
        foreach (IRoomEntity entity in entities)
        {
            if (entity is IRoomSaveStateEntity stateEntity)
                stateEntity.RefreshSaveState();
            if (entity is NpcRoomEntity && entity.Node is NpcCharacter npc)
            {
                npc.SetFlagVisible(_npcVisibility.ShouldShow(
                    npc.Record, _saveData, _runtimeState));
                if (_npcDialogue.TryResolve(npc.Record, _saveData, out var dialogue))
                    npc.SetDialogue(dialogue.TextId, dialogue.Message, npc.Record.CanFace);
                if (_npcPositions.TryResolve(
                    npc.Record, _saveData, out Vector2 position))
                {
                    npc.SetStatePosition(position);
                }
            }
        }
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

    private void OnTimePortalEntered(TimePortal portal) => TimePortalEntered?.Invoke(portal);
    private void OnGroundTreasureCollected(
        GroundTreasurePickup treasure,
        Player player) => GroundTreasureCollected?.Invoke(treasure, player);
    private void OnSoundRequested(int sound) => SoundRequested?.Invoke(sound);

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

    private static bool ReadGameButtonJustPressed() =>
        Input.IsActionJustPressed("attack") ||
        Input.IsActionJustPressed("item") ||
        Input.IsActionJustPressed("move_up") ||
        Input.IsActionJustPressed("move_right") ||
        Input.IsActionJustPressed("move_down") ||
        Input.IsActionJustPressed("move_left");
}
