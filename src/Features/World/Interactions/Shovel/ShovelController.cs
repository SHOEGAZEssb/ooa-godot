using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Resolves ITEM_SHOVEL ($15) against the imported breakable-tile table.
/// Link owns the parent-item timing; this controller performs the child
/// item's single state-0 tile attempt.
/// </summary>
public sealed class ShovelController
{
    private readonly RoomSession _rooms;
    private readonly BreakableTileDatabase _breakables;
    private readonly RoomView _roomView;
    private readonly RoomEntityManager _entities;
    private readonly OracleSaveData _saveData;
    private readonly Action<int> _playSound;
    private readonly Func<long> _animationTick;
    private bool _childCreatedThisUpdate;
    internal int ChildCounter { get; private set; }
    internal bool ChildActive => ChildCounter != 0;
    internal Vector2 ChildPosition { get; private set; }

    public ShovelController(
        RoomSession rooms,
        BreakableTileDatabase breakables,
        RoomView roomView,
        RoomEntityManager entities,
        OracleSaveData saveData,
        Action<int> playSound,
        Func<long> animationTick)
    {
        _rooms = rooms;
        _breakables = breakables;
        _roomView = roomView;
        _entities = entities;
        _saveData = saveData;
        _playSound = playSound;
        _animationTick = animationTick;
        entities.Shovel = this;
    }

    internal bool CreateChild(Vector2 point, Vector2I direction)
    {
        // shovelParent creates ITEM$15 separately from its parent. State 0
        // digs once; four eligible item updates then retire the child even
        // if clearAllParentItems has already removed the parent animation.
        ChildPosition = point;
        ChildCounter = 4;
        _childCreatedThisUpdate = true;
        return TryDig(point, direction);
    }

    internal void UpdateChild(bool frozen)
    {
        if (_childCreatedThisUpdate) _childCreatedThisUpdate = false;
        else if (!frozen && ChildActive) ChildCounter--;
    }

    internal void ClearChild()
    {
        ChildCounter = 0;
        _childCreatedThisUpdate = false;
    }

    public bool TryDig(Vector2 point, Vector2I direction)
    {
        const float shovelRadius = 3.0f;
        _entities.ApplyShovelHit(
            new Rect2(
                point - Vector2.One * shovelRadius,
                Vector2.One * shovelRadius * 2.0f),
            point);

        OracleRoomData room = _rooms.CurrentRoom;
        byte tile = room.GetMetatile(point);
        if (!_breakables.TryGet(
                room.ActiveCollisions, tile,
                out BreakableTileRecord record) ||
            !record.AllowsSource(BreakableTileDatabase.SourceShovel))
        {
            _playSound(SoundId.SndClink);
            return false;
        }
        if ((record.Effect & 0x1f) != 0x0a)
        {
            throw new InvalidOperationException(
                $"Unsupported shovel break effect ${record.Effect:x2} for " +
                $"collision set ${room.ActiveCollisions:x2}, tile ${tile:x2}.");
        }

        if (_breakables.TryBreak(
                room,
                BreakableTileDatabase.SourceShovel,
                point,
                _saveData,
                _rooms.ActiveGroup,
                _animationTick,
                linkedRoomNeighbor: null,
                out BreakableTileBreak result) !=
            BreakableTileBreakStatus.Broken)
        {
            _playSound(SoundId.SndClink);
            return false;
        }

        _entities.NotifyTileDug(result.PackedPosition);
        result.ApplyCommonEffects(
            _playSound,
            (drop, position) =>
                _entities.SpawnBreakableDrop(drop, position, direction));

        _entities.Spawn<ShovelDebrisEffect>(
            new ShovelDebrisSpawn(result.TileCenter, direction));
        _roomView.QueueRedraw();
        _saveData.AddGashaMaturity(1);
        _playSound(SoundId.SndDig);
        return true;
    }
}
