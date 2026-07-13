using Godot;
using System.Collections.Generic;

namespace oracleofages;

public sealed class RoomEntityManager
{
    private readonly Node _worldRoot;
    private readonly NpcDatabase _npcs;
    private readonly EnemyDatabase _enemies;
    private readonly OracleRandom _random = new();
    private readonly List<NpcCharacter> _npcNodes = new();
    private readonly List<NpcCharacter> _outgoingNpcNodes = new();
    private readonly List<KeeseCharacter> _keeseNodes = new();
    private readonly List<KeeseCharacter> _outgoingKeeseNodes = new();
    private readonly List<EnemyDeathPuffEffect> _deathPuffNodes = new();
    private readonly List<EnemyDeathPuffEffect> _outgoingDeathPuffNodes = new();
    private bool _screenTransitionActive;
    private double _enemyFrameAccumulator;
    private int _enemyFrameCounter;

    public List<NpcCharacter> Npcs => _npcNodes;
    public IReadOnlyList<NpcCharacter> OutgoingNpcs => _outgoingNpcNodes;
    public List<KeeseCharacter> Keese => _keeseNodes;
    public IReadOnlyList<KeeseCharacter> OutgoingKeese => _outgoingKeeseNodes;
    public IReadOnlyList<EnemyDeathPuffEffect> DeathPuffs => _deathPuffNodes;
    public IReadOnlyList<EnemyDeathPuffEffect> OutgoingDeathPuffs => _outgoingDeathPuffNodes;
    public bool ScreenTransitionActive => _screenTransitionActive;

    public RoomEntityManager(Node worldRoot, NpcDatabase npcs, EnemyDatabase enemies)
    {
        _worldRoot = worldRoot;
        _npcs = npcs;
        _enemies = enemies;
    }

    public void LoadRoom(int group, OracleRoomData room)
    {
        Clear();
        SpawnRoomNpcs(group, room);
        SpawnRoomKeese(group, room);
    }

    public void BeginScreenTransition(int group, OracleRoomData room, Vector2 incomingOffset)
    {
        ClearNodes(_outgoingNpcNodes);
        ClearNodes(_outgoingKeeseNodes);
        ClearNodes(_outgoingDeathPuffNodes);
        _outgoingNpcNodes.AddRange(_npcNodes);
        _outgoingKeeseNodes.AddRange(_keeseNodes);
        _outgoingDeathPuffNodes.AddRange(_deathPuffNodes);
        _npcNodes.Clear();
        _keeseNodes.Clear();
        _deathPuffNodes.Clear();
        _screenTransitionActive = true;
        SpawnRoomNpcs(group, room);
        SpawnRoomKeese(group, room);
        SetScreenTransitionOffsets(Vector2.Zero, incomingOffset);
    }

    public void SetScreenTransitionOffsets(Vector2 outgoingOffset, Vector2 incomingOffset)
    {
        if (!_screenTransitionActive)
            return;

        foreach (NpcCharacter npc in _outgoingNpcNodes)
            npc.SetTransitionDrawOffset(outgoingOffset);
        foreach (NpcCharacter npc in _npcNodes)
            npc.SetTransitionDrawOffset(incomingOffset);
        foreach (KeeseCharacter keese in _outgoingKeeseNodes)
            keese.SetTransitionDrawOffset(outgoingOffset);
        foreach (KeeseCharacter keese in _keeseNodes)
            keese.SetTransitionDrawOffset(incomingOffset);
        foreach (EnemyDeathPuffEffect puff in _outgoingDeathPuffNodes)
            puff.SetTransitionDrawOffset(outgoingOffset);
        foreach (EnemyDeathPuffEffect puff in _deathPuffNodes)
            puff.SetTransitionDrawOffset(incomingOffset);
    }

    public void FinishScreenTransition()
    {
        if (!_screenTransitionActive)
            return;

        ClearNodes(_outgoingNpcNodes);
        ClearNodes(_outgoingKeeseNodes);
        ClearNodes(_outgoingDeathPuffNodes);
        foreach (NpcCharacter npc in _npcNodes)
            npc.SetTransitionDrawOffset(Vector2.Zero);
        foreach (KeeseCharacter keese in _keeseNodes)
            keese.SetTransitionDrawOffset(Vector2.Zero);
        foreach (EnemyDeathPuffEffect puff in _deathPuffNodes)
            puff.SetTransitionDrawOffset(Vector2.Zero);
        _screenTransitionActive = false;
    }

    private void SpawnRoomNpcs(int group, OracleRoomData room)
    {
        foreach (NpcDatabase.NpcRecord record in _npcs.GetRoomNpcs(group, room.Id))
        {
            var npc = new NpcCharacter
            {
                Name = $"Npc_{record.Id:x2}_{record.SubId:x2}",
                ZIndex = NpcCharacter.BehindLinkZIndex
            };
            npc.Initialize(record);
            _npcNodes.Add(npc);
            _worldRoot.AddChild(npc);
        }
    }

    private void SpawnRoomKeese(int group, OracleRoomData room)
    {
        var occupiedPositions = new HashSet<int>();
        foreach (EnemyDatabase.EnemyRecord record in _enemies.GetRoomKeese(group, room.Id))
        {
            for (int instance = 0; instance < record.Count; instance++)
            {
                if (!TryChooseRandomEnemyPosition(room, record.Flags, occupiedPositions, out Vector2 position))
                    continue;

                var keese = new KeeseCharacter
                {
                    Name = $"Keese_{record.SubId:x2}_{instance}",
                    ZIndex = 10
                };
                keese.Initialize(record, room, position, _random);
                _keeseNodes.Add(keese);
                _worldRoot.AddChild(keese);
            }
        }
    }

    private bool TryChooseRandomEnemyPosition(
        OracleRoomData room,
        int flags,
        HashSet<int> occupied,
        out Vector2 position)
    {
        for (int attempt = 0; attempt < 0x3f; attempt++)
        {
            int packed = _random.NextPlacementValue();
            int tileY = packed >> 4;
            int tileX = packed & 0x0f;
            bool validBoundary = room.Group < 4
                ? tileY < OracleRoomData.ViewportHeight / OracleRoomData.MetatileSize &&
                    tileX < OracleRoomData.ViewportWidth / OracleRoomData.MetatileSize
                : tileY > 0 && tileY < room.HeightInTiles - 1 &&
                    tileX > 0 && tileX < room.WidthInTiles - 1;
            if (!validBoundary || occupied.Contains(packed))
                continue;

            position = new Vector2(
                tileX * OracleRoomData.MetatileSize + 8,
                tileY * OracleRoomData.MetatileSize + 8);
            if ((flags & 0x04) == 0 && room.IsSolid(position))
                continue;

            occupied.Add(packed);
            return true;
        }

        position = Vector2.Zero;
        return false;
    }

    public void Update(double delta, Player player)
    {
        // The original engine marks outgoing objects as enabled $02 while
        // destination objects initialize as enabled $01. Neither set resumes
        // its normal interaction updates until the scrolling transition ends.
        if (_screenTransitionActive)
            return;

        foreach (NpcCharacter npc in _npcNodes)
            npc.UpdateNpc(delta, player.Position);

        _enemyFrameAccumulator += delta * 60.0;
        while (_enemyFrameAccumulator >= 1.0)
        {
            _enemyFrameAccumulator -= 1.0;
            _enemyFrameCounter = (_enemyFrameCounter + 1) & 0xff;
            foreach (KeeseCharacter keese in _keeseNodes)
                keese.UpdateFrame(player.Position, _enemyFrameCounter);
            foreach (EnemyDeathPuffEffect puff in _deathPuffNodes)
                puff.UpdateFrame(_enemyFrameCounter);
        }

        foreach (KeeseCharacter keese in _keeseNodes)
        {
            if (keese.OverlapsLink(player.Position))
                player.ApplyEnemyContactDamage(keese.Position, keese.Record.DamageQuarters);
        }

        RemoveDeadKeese();
        RemoveFinishedDeathPuffs();
    }

    public bool BlocksLink(Vector2 linkCenter)
    {
        foreach (NpcCharacter npc in _npcNodes)
        {
            if (npc.BlocksLinkCenter(linkCenter))
                return true;
        }
        return false;
    }

    public NpcCharacter? FindTalkTarget(Player player)
    {
        foreach (NpcCharacter npc in _npcNodes)
        {
            if (npc.CanTalkTo(player))
                return npc;
        }
        return null;
    }

    public bool ApplySwordHit(Rect2 hitbox)
    {
        bool hit = false;
        foreach (KeeseCharacter keese in _keeseNodes)
        {
            if (!keese.IsDead && hitbox.Intersects(keese.CollisionBounds))
            {
                bool wasDead = keese.IsDead;
                bool struck = keese.TakeSwordHit();
                hit |= struck;
                if (struck && !wasDead && keese.IsDead)
                {
                    SpawnEnemyDeathPuff(
                        keese.Position + Vector2.Down * keese.SpriteHeight);
                }
            }
        }
        RemoveDeadKeese();
        return hit;
    }

    public EnemyDeathPuffEffect SpawnEnemyDeathPuff(
        Vector2 position,
        bool highKnockback = false)
    {
        var puff = new EnemyDeathPuffEffect
        {
            Name = "EnemyDeathPuff",
            ZIndex = 10
        };
        puff.Initialize(position, highKnockback);
        _deathPuffNodes.Add(puff);
        _worldRoot.AddChild(puff);
        return puff;
    }

    public void Clear()
    {
        ClearNodes(_outgoingNpcNodes);
        ClearNodes(_npcNodes);
        ClearNodes(_outgoingKeeseNodes);
        ClearNodes(_keeseNodes);
        ClearNodes(_outgoingDeathPuffNodes);
        ClearNodes(_deathPuffNodes);
        _screenTransitionActive = false;
        _enemyFrameAccumulator = 0.0;
    }

    private void ClearNodes(List<NpcCharacter> nodes)
    {
        foreach (NpcCharacter npc in nodes)
        {
            if (npc.GetParent() == _worldRoot)
                _worldRoot.RemoveChild(npc);
            npc.QueueFree();
        }
        nodes.Clear();
    }

    private void ClearNodes(List<KeeseCharacter> nodes)
    {
        foreach (KeeseCharacter keese in nodes)
        {
            if (keese.GetParent() == _worldRoot)
                _worldRoot.RemoveChild(keese);
            keese.QueueFree();
        }
        nodes.Clear();
    }

    private void ClearNodes(List<EnemyDeathPuffEffect> nodes)
    {
        foreach (EnemyDeathPuffEffect puff in nodes)
        {
            if (puff.GetParent() == _worldRoot)
                _worldRoot.RemoveChild(puff);
            puff.QueueFree();
        }
        nodes.Clear();
    }

    private void RemoveDeadKeese()
    {
        for (int index = _keeseNodes.Count - 1; index >= 0; index--)
        {
            KeeseCharacter keese = _keeseNodes[index];
            if (!keese.IsDead)
                continue;
            _keeseNodes.RemoveAt(index);
            if (keese.GetParent() == _worldRoot)
                _worldRoot.RemoveChild(keese);
            keese.QueueFree();
        }
    }

    private void RemoveFinishedDeathPuffs()
    {
        for (int index = _deathPuffNodes.Count - 1; index >= 0; index--)
        {
            EnemyDeathPuffEffect puff = _deathPuffNodes[index];
            if (!puff.Finished)
                continue;
            _deathPuffNodes.RemoveAt(index);
            if (puff.GetParent() == _worldRoot)
                _worldRoot.RemoveChild(puff);
            puff.QueueFree();
        }
    }
}
