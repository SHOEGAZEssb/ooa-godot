using Godot;
using System.Collections.Generic;

namespace oracleofages;

public sealed class RoomEntityManager
{
    private readonly Node _worldRoot;
    private readonly NpcDatabase _npcs;
    private readonly EnemyDatabase _enemies;
    private readonly ItemDropDatabase _itemDrops;
    private readonly OracleRandom _random;
    private readonly List<NpcCharacter> _npcNodes = new();
    private readonly List<NpcCharacter> _outgoingNpcNodes = new();
    private readonly List<KeeseCharacter> _keeseNodes = new();
    private readonly List<KeeseCharacter> _outgoingKeeseNodes = new();
    private readonly List<OctorokCharacter> _octorokNodes = new();
    private readonly List<OctorokCharacter> _outgoingOctorokNodes = new();
    private readonly List<OctorokRockProjectile> _octorokRockNodes = new();
    private readonly List<OctorokRockProjectile> _outgoingOctorokRockNodes = new();
    private readonly List<ZolCharacter> _zolNodes = new();
    private readonly List<ZolCharacter> _outgoingZolNodes = new();
    private readonly List<GelCharacter> _gelNodes = new();
    private readonly List<GelCharacter> _outgoingGelNodes = new();
    private readonly List<EnemyDeathPuffEffect> _deathPuffNodes = new();
    private readonly List<EnemyDeathPuffEffect> _outgoingDeathPuffNodes = new();
    private readonly List<KillEnemyPuffEffect> _killPuffNodes = new();
    private readonly List<KillEnemyPuffEffect> _outgoingKillPuffNodes = new();
    private readonly List<ItemDropEffect> _itemDropNodes = new();
    private readonly List<ItemDropEffect> _outgoingItemDropNodes = new();
    private OracleRoomData _roomForActiveEntities = null!;
    private bool _screenTransitionActive;
    private double _enemyFrameAccumulator;
    private int _enemyFrameCounter;

    public List<NpcCharacter> Npcs => _npcNodes;
    public IReadOnlyList<NpcCharacter> OutgoingNpcs => _outgoingNpcNodes;
    public List<KeeseCharacter> Keese => _keeseNodes;
    public IReadOnlyList<KeeseCharacter> OutgoingKeese => _outgoingKeeseNodes;
    public List<OctorokCharacter> Octoroks => _octorokNodes;
    public IReadOnlyList<OctorokCharacter> OutgoingOctoroks => _outgoingOctorokNodes;
    public IReadOnlyList<OctorokRockProjectile> OctorokRocks => _octorokRockNodes;
    public IReadOnlyList<OctorokRockProjectile> OutgoingOctorokRocks =>
        _outgoingOctorokRockNodes;
    public List<ZolCharacter> Zols => _zolNodes;
    public IReadOnlyList<ZolCharacter> OutgoingZols => _outgoingZolNodes;
    public List<GelCharacter> Gels => _gelNodes;
    public IReadOnlyList<GelCharacter> OutgoingGels => _outgoingGelNodes;
    public IReadOnlyList<EnemyDeathPuffEffect> DeathPuffs => _deathPuffNodes;
    public IReadOnlyList<EnemyDeathPuffEffect> OutgoingDeathPuffs => _outgoingDeathPuffNodes;
    public IReadOnlyList<KillEnemyPuffEffect> KillPuffs => _killPuffNodes;
    public IReadOnlyList<KillEnemyPuffEffect> OutgoingKillPuffs => _outgoingKillPuffNodes;
    public IReadOnlyList<ItemDropEffect> ItemDrops => _itemDropNodes;
    public IReadOnlyList<ItemDropEffect> OutgoingItemDrops => _outgoingItemDropNodes;
    public bool ScreenTransitionActive => _screenTransitionActive;
    public bool PlayerSwordDisabled => _gelNodes.Exists(gel => gel.IsAttached);
    public bool PlayerMovementDisabled =>
        PlayerSwordDisabled && (_enemyFrameCounter & 1) != 0;

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
        _npcs = npcs;
        _enemies = enemies;
        _itemDrops = itemDrops;
        _random = random;
    }

    public void LoadRoom(int group, OracleRoomData room)
    {
        Clear();
        _roomForActiveEntities = room;
        SpawnRoomNpcs(group, room);
        SpawnRoomKeese(group, room);
        SpawnRoomOctoroks(group, room);
        SpawnRoomZols(group, room);
        SpawnRoomGels(group, room);
    }

    public void BeginScreenTransition(int group, OracleRoomData room, Vector2 incomingOffset)
    {
        ClearNodes(_outgoingNpcNodes);
        ClearNodes(_outgoingKeeseNodes);
        ClearNodes(_outgoingOctorokNodes);
        ClearNodes(_outgoingOctorokRockNodes);
        ClearNodes(_outgoingZolNodes);
        ClearNodes(_outgoingGelNodes);
        ClearNodes(_outgoingDeathPuffNodes);
        ClearNodes(_outgoingKillPuffNodes);
        ClearNodes(_outgoingItemDropNodes);
        _outgoingNpcNodes.AddRange(_npcNodes);
        _outgoingKeeseNodes.AddRange(_keeseNodes);
        _outgoingOctorokNodes.AddRange(_octorokNodes);
        _outgoingOctorokRockNodes.AddRange(_octorokRockNodes);
        _outgoingZolNodes.AddRange(_zolNodes);
        _outgoingGelNodes.AddRange(_gelNodes);
        _outgoingDeathPuffNodes.AddRange(_deathPuffNodes);
        _outgoingKillPuffNodes.AddRange(_killPuffNodes);
        _outgoingItemDropNodes.AddRange(_itemDropNodes);
        _npcNodes.Clear();
        _keeseNodes.Clear();
        _octorokNodes.Clear();
        _octorokRockNodes.Clear();
        _zolNodes.Clear();
        _gelNodes.Clear();
        _deathPuffNodes.Clear();
        _killPuffNodes.Clear();
        _itemDropNodes.Clear();
        _screenTransitionActive = true;
        _roomForActiveEntities = room;
        SpawnRoomNpcs(group, room);
        SpawnRoomKeese(group, room);
        SpawnRoomOctoroks(group, room);
        SpawnRoomZols(group, room);
        SpawnRoomGels(group, room);
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
        foreach (OctorokCharacter octorok in _outgoingOctorokNodes)
            octorok.SetTransitionDrawOffset(outgoingOffset);
        foreach (OctorokCharacter octorok in _octorokNodes)
            octorok.SetTransitionDrawOffset(incomingOffset);
        foreach (OctorokRockProjectile rock in _outgoingOctorokRockNodes)
            rock.SetTransitionDrawOffset(outgoingOffset);
        foreach (OctorokRockProjectile rock in _octorokRockNodes)
            rock.SetTransitionDrawOffset(incomingOffset);
        foreach (ZolCharacter zol in _outgoingZolNodes)
            zol.SetTransitionDrawOffset(outgoingOffset);
        foreach (ZolCharacter zol in _zolNodes)
            zol.SetTransitionDrawOffset(incomingOffset);
        foreach (GelCharacter gel in _outgoingGelNodes)
            gel.SetTransitionDrawOffset(outgoingOffset);
        foreach (GelCharacter gel in _gelNodes)
            gel.SetTransitionDrawOffset(incomingOffset);
        foreach (EnemyDeathPuffEffect puff in _outgoingDeathPuffNodes)
            puff.SetTransitionDrawOffset(outgoingOffset);
        foreach (EnemyDeathPuffEffect puff in _deathPuffNodes)
            puff.SetTransitionDrawOffset(incomingOffset);
        foreach (KillEnemyPuffEffect puff in _outgoingKillPuffNodes)
            puff.SetTransitionDrawOffset(outgoingOffset);
        foreach (KillEnemyPuffEffect puff in _killPuffNodes)
            puff.SetTransitionDrawOffset(incomingOffset);
        foreach (ItemDropEffect drop in _outgoingItemDropNodes)
            drop.SetTransitionDrawOffset(outgoingOffset);
        foreach (ItemDropEffect drop in _itemDropNodes)
            drop.SetTransitionDrawOffset(incomingOffset);
    }

    public void FinishScreenTransition()
    {
        if (!_screenTransitionActive)
            return;

        ClearNodes(_outgoingNpcNodes);
        ClearNodes(_outgoingKeeseNodes);
        ClearNodes(_outgoingOctorokNodes);
        ClearNodes(_outgoingOctorokRockNodes);
        ClearNodes(_outgoingZolNodes);
        ClearNodes(_outgoingGelNodes);
        ClearNodes(_outgoingDeathPuffNodes);
        ClearNodes(_outgoingKillPuffNodes);
        ClearNodes(_outgoingItemDropNodes);
        foreach (NpcCharacter npc in _npcNodes)
            npc.SetTransitionDrawOffset(Vector2.Zero);
        foreach (KeeseCharacter keese in _keeseNodes)
            keese.SetTransitionDrawOffset(Vector2.Zero);
        foreach (OctorokCharacter octorok in _octorokNodes)
            octorok.SetTransitionDrawOffset(Vector2.Zero);
        foreach (OctorokRockProjectile rock in _octorokRockNodes)
            rock.SetTransitionDrawOffset(Vector2.Zero);
        foreach (ZolCharacter zol in _zolNodes)
            zol.SetTransitionDrawOffset(Vector2.Zero);
        foreach (GelCharacter gel in _gelNodes)
            gel.SetTransitionDrawOffset(Vector2.Zero);
        foreach (EnemyDeathPuffEffect puff in _deathPuffNodes)
            puff.SetTransitionDrawOffset(Vector2.Zero);
        foreach (KillEnemyPuffEffect puff in _killPuffNodes)
            puff.SetTransitionDrawOffset(Vector2.Zero);
        foreach (ItemDropEffect drop in _itemDropNodes)
            drop.SetTransitionDrawOffset(Vector2.Zero);
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

    private void SpawnRoomOctoroks(int group, OracleRoomData room)
    {
        var occupiedPositions = new HashSet<int>();
        foreach (EnemyDatabase.OctorokRecord record in _enemies.GetRoomOctoroks(group, room.Id))
        {
            for (int instance = 0; instance < record.Count; instance++)
            {
                Vector2 position;
                if (record.FixedPosition)
                {
                    position = new Vector2(record.X, record.Y);
                }
                else if (!TryChooseRandomEnemyPosition(
                    room, record.Flags, occupiedPositions, out position))
                {
                    continue;
                }

                var octorok = new OctorokCharacter
                {
                    Name = $"Octorok_{record.SubId:x2}_{instance}",
                    ZIndex = 10
                };
                octorok.Initialize(record, room, position, _random);
                _octorokNodes.Add(octorok);
                _worldRoot.AddChild(octorok);
            }
        }
    }

    private void SpawnRoomZols(int group, OracleRoomData room)
    {
        var occupiedPositions = new HashSet<int>();
        foreach (EnemyDatabase.ZolRecord record in _enemies.GetRoomZols(group, room.Id))
        {
            for (int instance = 0; instance < record.Count; instance++)
            {
                Vector2 position;
                if (record.FixedPosition)
                {
                    position = new Vector2(record.X, record.Y);
                }
                else if (!TryChooseRandomEnemyPosition(
                    room, record.Flags, occupiedPositions, out position))
                {
                    continue;
                }

                var zol = new ZolCharacter
                {
                    Name = $"Zol_{record.SubId:x2}_{instance}",
                    ZIndex = 10
                };
                zol.Initialize(record, room, position, _random);
                _zolNodes.Add(zol);
                _worldRoot.AddChild(zol);
            }
        }
    }

    private void SpawnRoomGels(int group, OracleRoomData room)
    {
        var occupiedPositions = new HashSet<int>();
        foreach (EnemyDatabase.GelRecord record in _enemies.GetRoomGels(group, room.Id))
        {
            for (int instance = 0; instance < record.Count; instance++)
            {
                Vector2 position;
                if (record.FixedPosition)
                {
                    position = new Vector2(record.X, record.Y);
                }
                else if (!TryChooseRandomEnemyPosition(
                    room, record.Flags, occupiedPositions, out position))
                {
                    continue;
                }
                SpawnGel(position, $"RoomGel_{instance}");
            }
        }
    }

    internal GelCharacter SpawnGel(Vector2 position, string name = "Gel")
    {
        var gel = new GelCharacter
        {
            Name = name,
            ZIndex = 10
        };
        gel.Initialize(_enemies.Gel, _roomForActiveEntities, position, _random);
        _gelNodes.Add(gel);
        _worldRoot.AddChild(gel);
        return gel;
    }

    internal OctorokRockProjectile SpawnOctorokRock(Vector2 position, int angle)
    {
        var rock = new OctorokRockProjectile
        {
            Name = "OctorokRock",
            ZIndex = 10
        };
        rock.Initialize(_enemies.OctorokProjectile, _roomForActiveEntities, position, angle);
        _octorokRockNodes.Add(rock);
        _worldRoot.AddChild(rock);
        return rock;
    }

    internal KillEnemyPuffEffect SpawnKillEnemyPuff(Vector2 position)
    {
        var puff = new KillEnemyPuffEffect
        {
            Name = "KillEnemyPuff",
            ZIndex = 10
        };
        puff.Initialize(position);
        _killPuffNodes.Add(puff);
        _worldRoot.AddChild(puff);
        return puff;
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

        bool anyButtonJustPressed = AnyGameButtonJustPressed();
        _enemyFrameAccumulator += delta * 60.0;
        while (_enemyFrameAccumulator >= 1.0)
        {
            _enemyFrameAccumulator -= 1.0;
            _enemyFrameCounter = (_enemyFrameCounter + 1) & 0xff;
            foreach (KeeseCharacter keese in _keeseNodes)
                keese.UpdateFrame(player.Position, _enemyFrameCounter);
            foreach (OctorokCharacter octorok in _octorokNodes)
            {
                if (octorok.UpdateFrame(player.Position))
                    SpawnOctorokRock(octorok.Position, octorok.Angle);
            }
            foreach (OctorokRockProjectile rock in _octorokRockNodes)
                rock.UpdateFrame(player);
            var zolEvents = new List<(ZolCharacter Zol, ZolCharacter.UpdateEvent Event)>();
            foreach (ZolCharacter zol in _zolNodes)
            {
                ZolCharacter.UpdateEvent updateEvent = zol.UpdateFrame(player.Position);
                if (updateEvent != ZolCharacter.UpdateEvent.None)
                    zolEvents.Add((zol, updateEvent));
            }
            foreach (GelCharacter gel in _gelNodes)
            {
                gel.UpdateFrame(
                    player.Position,
                    player.FacingVector,
                    anyButtonJustPressed);
            }
            foreach (EnemyDeathPuffEffect puff in _deathPuffNodes)
                puff.UpdateFrame(_enemyFrameCounter);
            foreach (KillEnemyPuffEffect puff in _killPuffNodes)
                puff.UpdateFrame();
            foreach (ItemDropEffect drop in _itemDropNodes)
                drop.UpdateFrame(player, _enemyFrameCounter);
            foreach ((ZolCharacter zol, ZolCharacter.UpdateEvent updateEvent) in zolEvents)
            {
                if (updateEvent == ZolCharacter.UpdateEvent.BeginSplit)
                {
                    SpawnKillEnemyPuff(zol.Position);
                }
                else
                {
                    SpawnGel(zol.Position + Vector2.Right * 4.0f, "SplitGelRight");
                    SpawnGel(zol.Position + Vector2.Left * 4.0f, "SplitGelLeft");
                }
            }
            anyButtonJustPressed = false;
        }

        foreach (KeeseCharacter keese in _keeseNodes)
        {
            if (keese.OverlapsLink(player.Position))
                player.ApplyEnemyContactDamage(keese.Position, keese.Record.DamageQuarters);
        }
        foreach (OctorokCharacter octorok in _octorokNodes)
        {
            if (octorok.OverlapsLink(player.Position))
                player.ApplyEnemyContactDamage(octorok.Position, octorok.Record.DamageQuarters);
        }
        foreach (ZolCharacter zol in _zolNodes)
        {
            if (zol.OverlapsLink(player.Position))
                player.ApplyEnemyContactDamage(zol.Position, zol.Record.DamageQuarters);
        }
        foreach (GelCharacter gel in _gelNodes)
        {
            if (!gel.OverlapsLink(player.Position))
                continue;
            gel.AttachToLink(player.Position);
        }

        RemoveDeadKeese();
        RemoveDeadOctoroks();
        RemoveDeadZols();
        RemoveDeadGels();
        RemoveFinishedOctorokRocks();
        RemoveFinishedDeathPuffs();
        RemoveFinishedKillPuffs();
        RemoveFinishedItemDrops();
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

    public bool ApplySwordHit(Rect2 hitbox, Vector2? sourcePosition = null)
    {
        bool hit = false;
        foreach (OctorokRockProjectile rock in _octorokRockNodes)
        {
            if (hitbox.Intersects(rock.CollisionBounds))
                hit |= rock.DeflectWithSword();
        }
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
                        keese.Position + Vector2.Down * keese.SpriteHeight,
                        enemyId: keese.Record.Id);
                }
            }
        }
        foreach (OctorokCharacter octorok in _octorokNodes)
        {
            if (octorok.IsDead || !hitbox.Intersects(octorok.CollisionBounds))
                continue;

            bool wasDead = octorok.IsDead;
            bool struck = octorok.TakeSwordHit(sourcePosition ?? hitbox.GetCenter());
            hit |= struck;
            if (struck && !wasDead && octorok.IsDead && !octorok.DiedInHazard)
                SpawnEnemyDeathPuff(octorok.Position, enemyId: octorok.Record.Id);
        }
        foreach (ZolCharacter zol in _zolNodes)
        {
            if (zol.IsDead || !hitbox.Intersects(zol.CollisionBounds))
                continue;

            bool wasDead = zol.IsDead;
            bool struck = zol.TakeSwordHit();
            hit |= struck;
            if (struck && !wasDead && zol.IsDead && !zol.DiedInHazard)
                SpawnEnemyDeathPuff(zol.Position, enemyId: zol.Record.Id);
        }
        foreach (GelCharacter gel in _gelNodes)
        {
            if (gel.IsDead || !hitbox.Intersects(gel.CollisionBounds))
                continue;

            bool wasDead = gel.IsDead;
            bool struck = gel.TakeSwordHit();
            hit |= struck;
            if (struck && !wasDead && gel.IsDead && !gel.DiedInHazard)
                SpawnEnemyDeathPuff(gel.Position, enemyId: gel.Definition.Id);
        }
        RemoveDeadKeese();
        RemoveDeadOctoroks();
        RemoveDeadZols();
        RemoveDeadGels();
        return hit;
    }

    public EnemyDeathPuffEffect SpawnEnemyDeathPuff(
        Vector2 position,
        bool highKnockback = false,
        int enemyId = -1)
    {
        var puff = new EnemyDeathPuffEffect
        {
            Name = "EnemyDeathPuff",
            ZIndex = 10
        };
        puff.Initialize(position, highKnockback, enemyId);
        _deathPuffNodes.Add(puff);
        _worldRoot.AddChild(puff);
        return puff;
    }

    internal ItemDropEffect SpawnItemDrop(int subId, Vector2 position)
    {
        var drop = new ItemDropEffect
        {
            Name = $"ItemDrop_{subId:x2}",
            ZIndex = 10
        };
        drop.Initialize(subId, position, _roomForActiveEntities,
            _itemDrops.GetVisual(subId));
        _itemDropNodes.Add(drop);
        _worldRoot.AddChild(drop);
        return drop;
    }

    public void Clear()
    {
        ClearNodes(_outgoingNpcNodes);
        ClearNodes(_npcNodes);
        ClearNodes(_outgoingKeeseNodes);
        ClearNodes(_keeseNodes);
        ClearNodes(_outgoingOctorokNodes);
        ClearNodes(_octorokNodes);
        ClearNodes(_outgoingOctorokRockNodes);
        ClearNodes(_octorokRockNodes);
        ClearNodes(_outgoingZolNodes);
        ClearNodes(_zolNodes);
        ClearNodes(_outgoingGelNodes);
        ClearNodes(_gelNodes);
        ClearNodes(_outgoingDeathPuffNodes);
        ClearNodes(_deathPuffNodes);
        ClearNodes(_outgoingKillPuffNodes);
        ClearNodes(_killPuffNodes);
        ClearNodes(_outgoingItemDropNodes);
        ClearNodes(_itemDropNodes);
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

    private void ClearNodes(List<OctorokCharacter> nodes)
    {
        foreach (OctorokCharacter octorok in nodes)
        {
            if (octorok.GetParent() == _worldRoot)
                _worldRoot.RemoveChild(octorok);
            octorok.QueueFree();
        }
        nodes.Clear();
    }

    private void ClearNodes(List<OctorokRockProjectile> nodes)
    {
        foreach (OctorokRockProjectile rock in nodes)
        {
            if (rock.GetParent() == _worldRoot)
                _worldRoot.RemoveChild(rock);
            rock.QueueFree();
        }
        nodes.Clear();
    }

    private void ClearNodes(List<ZolCharacter> nodes)
    {
        foreach (ZolCharacter zol in nodes)
        {
            if (zol.GetParent() == _worldRoot)
                _worldRoot.RemoveChild(zol);
            zol.QueueFree();
        }
        nodes.Clear();
    }

    private void ClearNodes(List<GelCharacter> nodes)
    {
        foreach (GelCharacter gel in nodes)
        {
            if (gel.GetParent() == _worldRoot)
                _worldRoot.RemoveChild(gel);
            gel.QueueFree();
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

    private void ClearNodes(List<KillEnemyPuffEffect> nodes)
    {
        foreach (KillEnemyPuffEffect puff in nodes)
        {
            if (puff.GetParent() == _worldRoot)
                _worldRoot.RemoveChild(puff);
            puff.QueueFree();
        }
        nodes.Clear();
    }

    private void ClearNodes(List<ItemDropEffect> nodes)
    {
        foreach (ItemDropEffect drop in nodes)
        {
            if (drop.GetParent() == _worldRoot)
                _worldRoot.RemoveChild(drop);
            drop.QueueFree();
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

    private void RemoveDeadOctoroks()
    {
        for (int index = _octorokNodes.Count - 1; index >= 0; index--)
        {
            OctorokCharacter octorok = _octorokNodes[index];
            if (!octorok.IsDead)
                continue;
            _octorokNodes.RemoveAt(index);
            if (octorok.GetParent() == _worldRoot)
                _worldRoot.RemoveChild(octorok);
            octorok.QueueFree();
        }
    }

    private void RemoveDeadZols()
    {
        for (int index = _zolNodes.Count - 1; index >= 0; index--)
        {
            ZolCharacter zol = _zolNodes[index];
            if (!zol.IsDead)
                continue;
            _zolNodes.RemoveAt(index);
            if (zol.GetParent() == _worldRoot)
                _worldRoot.RemoveChild(zol);
            zol.QueueFree();
        }
    }

    private void RemoveDeadGels()
    {
        for (int index = _gelNodes.Count - 1; index >= 0; index--)
        {
            GelCharacter gel = _gelNodes[index];
            if (!gel.IsDead)
                continue;
            _gelNodes.RemoveAt(index);
            if (gel.GetParent() == _worldRoot)
                _worldRoot.RemoveChild(gel);
            gel.QueueFree();
        }
    }

    private void RemoveFinishedOctorokRocks()
    {
        for (int index = _octorokRockNodes.Count - 1; index >= 0; index--)
        {
            OctorokRockProjectile rock = _octorokRockNodes[index];
            if (!rock.Finished)
                continue;
            _octorokRockNodes.RemoveAt(index);
            if (rock.GetParent() == _worldRoot)
                _worldRoot.RemoveChild(rock);
            rock.QueueFree();
        }
    }

    private void RemoveFinishedDeathPuffs()
    {
        for (int index = _deathPuffNodes.Count - 1; index >= 0; index--)
        {
            EnemyDeathPuffEffect puff = _deathPuffNodes[index];
            if (!puff.Finished)
                continue;
            int? dropSubId = _itemDrops.DecideDrop(puff.EnemyId, _random);
            if (dropSubId.HasValue)
                SpawnItemDrop(dropSubId.Value, puff.Position);
            _deathPuffNodes.RemoveAt(index);
            if (puff.GetParent() == _worldRoot)
                _worldRoot.RemoveChild(puff);
            puff.QueueFree();
        }
    }

    private void RemoveFinishedKillPuffs()
    {
        for (int index = _killPuffNodes.Count - 1; index >= 0; index--)
        {
            KillEnemyPuffEffect puff = _killPuffNodes[index];
            if (!puff.Finished)
                continue;
            _killPuffNodes.RemoveAt(index);
            if (puff.GetParent() == _worldRoot)
                _worldRoot.RemoveChild(puff);
            puff.QueueFree();
        }
    }

    private void RemoveFinishedItemDrops()
    {
        for (int index = _itemDropNodes.Count - 1; index >= 0; index--)
        {
            ItemDropEffect drop = _itemDropNodes[index];
            if (!drop.Finished)
                continue;
            _itemDropNodes.RemoveAt(index);
            if (drop.GetParent() == _worldRoot)
                _worldRoot.RemoveChild(drop);
            drop.QueueFree();
        }
    }

    private static bool AnyGameButtonJustPressed()
    {
        return Input.IsActionJustPressed("attack") ||
            Input.IsActionJustPressed("item") ||
            Input.IsActionJustPressed("move_up") ||
            Input.IsActionJustPressed("move_right") ||
            Input.IsActionJustPressed("move_down") ||
            Input.IsActionJustPressed("move_left");
    }
}
