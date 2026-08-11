using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed class CombatController
{
    private readonly Node _worldRoot;
    private readonly RoomSession _rooms;
    private readonly RoomView _roomView;
    private readonly RoomEntityManager _entities;
    private readonly BreakableTileDatabase _breakables;
    private readonly OracleSaveData _saveData;
    private readonly OracleSoundEngine _sound;
    private readonly Func<long> _animationTick;
    private readonly LinkItemDatabase _linkItems;
    private readonly List<ClinkEffect> _clinkEffects = new();
    private ICombatEffectObserver? _effectObserver;

    public CombatController(
        Node worldRoot,
        RoomSession rooms,
        RoomView roomView,
        RoomEntityManager entities,
        BreakableTileDatabase breakables,
        OracleSaveData saveData,
        OracleSoundEngine sound,
        Func<long> animationTick)
    {
        _worldRoot = worldRoot;
        _rooms = rooms;
        _roomView = roomView;
        _entities = entities;
        _breakables = breakables;
        _saveData = saveData;
        _sound = sound;
        _animationTick = animationTick;
        _linkItems = LinkItemDatabase.Shared;
    }

    public bool ApplySwordHit(Player player, Rect2 hitbox)
    {
        return _entities.ApplySwordHit(
            hitbox,
            player.Position,
            player.SwordDamage,
            player.SwordKnockbackStrength,
            collectItemDrops: true,
            attackerKnockback: response =>
                player.QueueSwordCollisionKnockback(
                    response.SourcePosition,
                    response.Frames),
            swordState: player.SwordState,
            swordLevel: player.Inventory.SwordLevel,
            itemZ: player.MeleeItemZ,
            expertPunch: player.IsUsingExpertPunch);
    }

    public bool ApplySwordTileHit(Player player, int direction, bool swordPoke)
    {
        int source = player.Inventory.SwordLevel <= 1
            ? BreakableTileDatabase.SourceSwordLevel1
            : BreakableTileDatabase.SourceSwordLevel2;
        return ApplyTileHit(player, direction, source, swordPoke);
    }

    public bool ApplyExpertsRingTileHit(Player player, int direction) =>
        ApplyTileHit(
            player, direction, BreakableTileDatabase.SourceExpertsRing,
            swordPoke: true);

    public bool ApplyLandedTileHit(Vector2 linkPosition)
    {
        OracleRoomData room = _rooms.CurrentRoom;
        Vector2 point = linkPosition + new Vector2(0, 5);
        byte tile = room.GetMetatile(point);
        if (!_breakables.TryGet(
                room.ActiveCollisions,
                tile,
                out BreakableTileRecord record) ||
            !record.AllowsSource(BreakableTileDatabase.SourceLanded))
        {
            return false;
        }

        int packedPosition = room.GetPackedPosition(point);
        Vector2 tileCenter = new(
            (packedPosition & 0x0f) * OracleRoomData.MetatileSize + 8,
            (packedPosition >> 4) * OracleRoomData.MetatileSize + 8);
        byte replacement = record.ReplacementFor(room, tileCenter);
        bool changed = record.Replacement == 0 ||
            room.ReplaceMetatile(
                tileCenter,
                tile,
                replacement,
                _animationTick());
        if (!changed)
            return false;

        record.ApplyPersistentEffects(
            _saveData,
            _rooms.ActiveGroup,
            room.Id,
            direction => _rooms.TryGetNeighbor(direction, out int neighbor)
                ? neighbor
                : null);
        if ((record.Effect & 0x40) != 0)
            _sound.PlaySound(OracleSoundEngine.SndSolvePuzzle);
        if (record.Drop != 0)
            _entities.SpawnBreakableDrop(record.Drop, tileCenter);

        SpawnBreakEffect(tileCenter, record.Effect);
        _roomView.QueueRedraw();
        return true;
    }

    private bool ApplyTileHit(
        Player player, int direction, int breakableSource, bool swordPoke)
    {
        OracleRoomData room = _rooms.CurrentRoom;
        Vector2 point =
            player.Position + _linkItems.SwordTileOffset(direction);
        byte tile = room.GetMetatile(point);
        if (_breakables.TryGet(room.ActiveCollisions, tile, out BreakableTileRecord record) &&
            record.AllowsSource(breakableSource))
        {
            bool changed = record.Replacement == 0 ||
                room.ReplaceMetatile(point, tile, (byte)record.Replacement, _animationTick());
            if (!changed)
                return false;

            SpawnBreakEffect(point, record.Effect);
            _roomView.QueueRedraw();
            return true;
        }

        int collisionSet = Math.Clamp(room.ActiveCollisions, 0, 5);
        if (_linkItems.IsBombableClinkTile(collisionSet, tile))
        {
            SpawnClinkEffect(point, flickers: false);
            _sound.PlaySound(OracleSoundEngine.SndClink2);
            return true;
        }
        if (!swordPoke ||
            _linkItems.IsSilentClinkTile(collisionSet, tile) ||
            room.GetTerrainInfo(point).Collision != 0x0f)
        {
            return false;
        }

        SpawnClinkEffect(point, flickers: true);
        _sound.PlaySound(OracleSoundEngine.SndClink);
        return true;
    }

    internal void SetEffectObserver(ICombatEffectObserver? observer) =>
        _effectObserver = observer;

    internal void AdvanceApplicationUpdate()
    {
        for (int index = _clinkEffects.Count - 1; index >= 0; index--)
        {
            ClinkEffect effect = _clinkEffects[index];
            effect.AdvanceApplicationUpdate();
            if (effect.Finished)
                _clinkEffects.RemoveAt(index);
        }
    }

    private void SpawnClinkEffect(Vector2 position, bool flickers)
    {
        var effect = new ClinkEffect
        {
            Name = "Clink",
            ZIndex = 10
        };
        effect.Initialize(position, flickers);
        _worldRoot.AddChild(effect);
        effect.SetPhysicsProcess(false);
        _clinkEffects.Add(effect);
        _effectObserver?.OnClinkEffectSpawned(effect);
    }

    internal void SpawnBreakEffect(Vector2 point, int effect)
    {
        // INTERAC_GRASSDEBRIS ($00), INTERAC_REDGRASSDEBRIS ($01), and
        // INTERAC_ROCKDEBRIS ($06/$0c) own their imported animation,
        // palette, timing, and sound through the room-entity path.
        int interaction = effect & 0x0f;
        bool flickers = (effect & 0x10) != 0;
        if (interaction is 0x06 or 0x0c)
        {
            int rockTileX = Mathf.FloorToInt(
                point.X / OracleRoomData.MetatileSize);
            int rockTileY = Mathf.FloorToInt(
                point.Y / OracleRoomData.MetatileSize);
            _entities.Spawn<RockDebrisEffect>(new RockDebrisSpawn(
                new Vector2(
                    rockTileX * OracleRoomData.MetatileSize +
                        OracleRoomData.MetatileSize / 2.0f,
                    rockTileY * OracleRoomData.MetatileSize +
                        OracleRoomData.MetatileSize / 2.0f),
                interaction));
            return;
        }
        if (interaction is not (0x00 or 0x01))
            return;

        int tileX = Mathf.FloorToInt(point.X / OracleRoomData.MetatileSize);
        int tileY = Mathf.FloorToInt(point.Y / OracleRoomData.MetatileSize);
        _entities.Spawn<GrassDebrisEffect>(new GrassDebrisSpawn(
            new Vector2(
                tileX * OracleRoomData.MetatileSize +
                    OracleRoomData.MetatileSize / 2.0f,
                tileY * OracleRoomData.MetatileSize +
                    OracleRoomData.MetatileSize / 2.0f),
            interaction,
            flickers,
            (_rooms.CurrentRoom.TilesetFlags & 0x40) != 0));
    }
}
