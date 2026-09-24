using Godot;
using System;

namespace oracleofages;

public sealed class CombatController
{
    private readonly RoomSession _rooms;
    private readonly RoomView _roomView;
    private readonly RoomEntityManager _entities;
    private readonly BreakableTileDatabase _breakables;
    private readonly OracleSaveData _saveData;
    private readonly OracleSoundEngine _sound;
    private readonly Func<long> _animationTick;
    private readonly LinkItemDatabase _linkItems;
    private ICombatEffectObserver? _effectObserver;

    public CombatController(
        RoomSession rooms,
        RoomView roomView,
        RoomEntityManager entities,
        BreakableTileDatabase breakables,
        OracleSaveData saveData,
        OracleSoundEngine sound,
        Func<long> animationTick)
    {
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
            expertPunch: player.IsUsingExpertPunch,
            deferredContact: player.QueueSwordEnemyContact,
            meleeActive: () => player.IsAttacking || player.IsUsingExpertPunch);
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
        if (_breakables.TryBreak(
                room,
                BreakableTileDatabase.SourceLanded,
                point,
                _saveData,
                _rooms.ActiveGroup,
                _animationTick,
                LinkedRoomNeighbor,
                out BreakableTileBreak result) !=
            BreakableTileBreakStatus.Broken)
        {
            return false;
        }

        result.ApplyCommonEffects(
            _sound.PlaySound, _entities.SpawnBreakableDrop);
        SpawnBreakEffect(result.TileCenter, result.Record.Effect);
        _roomView.QueueRedraw();
        return true;
    }

    private bool ApplyTileHit(
        Player player, int direction, int breakableSource, bool swordPoke)
    {
        OracleRoomData room = _rooms.CurrentRoom;
        Vector2 point =
            player.Position + _linkItems.SwordTileOffset(direction);
        BreakableTileBreakStatus breakStatus = _breakables.TryBreak(
            room,
            breakableSource,
            point,
            _saveData,
            _rooms.ActiveGroup,
            _animationTick,
            LinkedRoomNeighbor,
            out BreakableTileBreak result);
        if (breakStatus == BreakableTileBreakStatus.Broken)
        {
            result.ApplyCommonEffects(
                _sound.PlaySound, _entities.SpawnBreakableDrop);
            SpawnBreakEffect(result.TileCenter, result.Record.Effect);
            _roomView.QueueRedraw();
            return true;
        }
        if (breakStatus == BreakableTileBreakStatus.Unchanged)
            return false;

        byte tile = room.GetMetatile(point);
        int collisionSet = Math.Clamp(room.ActiveCollisions, 0, 5);
        if (_linkItems.IsBombableClinkTile(collisionSet, tile))
        {
            _sound.PlaySound(OracleSoundEngine.SndClink2);
            SpawnClinkEffect(point, flickers: false);
            return true;
        }
        if (!swordPoke ||
            _linkItems.IsSilentClinkTile(collisionSet, tile) ||
            room.GetTerrainInfo(point).Collision != 0x0f)
        {
            return false;
        }

        SpawnClinkEffect(point, flickers: true);
        return true;
    }

    private int? LinkedRoomNeighbor(Vector2I direction) =>
        _rooms.TryGetNeighbor(direction, out int neighbor) ? neighbor : null;

    internal void SetEffectObserver(ICombatEffectObserver? observer) =>
        _effectObserver = observer;

    private void SpawnClinkEffect(Vector2 position, bool flickers)
    {
        // commonCode2.s @createClink returns on a full interaction pool.
        if (!_entities.InteractionSlotAvailable) return;
        var effect = _entities.Spawn<ClinkEffect>(new SwordWallClinkSpawn(position, flickers));
        _effectObserver?.OnClinkEffectSpawned(effect);
    }

    internal void SpawnBreakEffect(Vector2 point, int effect)
    {
        if (BreakableTileEffectSpawn.Create(
                _rooms.CurrentRoom, point, effect) is { } spawn)
        {
            _entities.Spawn(spawn);
        }
    }
}
